using System;

using System.Collections.Generic;

using System.Diagnostics;

using System.IO;

using System.Net;

using System.Reflection;

using System.Runtime.InteropServices;

using System.Text.RegularExpressions;

using System.Threading;

using NAudio.Wave;

using KahoiiMusic.Models;

namespace KahoiiMusic.Services

{

    public class TrackLoadedEventArgs : EventArgs

    {

        public string VideoId { get; set; }

        public string Title { get; set; }

        public TimeSpan Duration { get; set; }

    }

    public class AudioPlayerService : IDisposable

    {

        private MediaFoundationReader _audioReader;

        private ManagedAudioStream _managedStream;

        private WaveOutEvent _outputDevice;

        private SynchronizationContext _syncContext;

        private string _cacheDirectory;

        private bool _isLoading;

        private string _currentVideoId;

        private string _currentTitle = string.Empty;

        private TimeSpan _totalDuration = TimeSpan.Zero;

        private float _volume = 1.0f;

        private bool _isLooping = false;

        private object _lock = new object();

        private Process _activeDownloadProc;

        private readonly object _downloadLock = new object();

        private Process _preloadProc;

        private string _preloadingVideoId;

        private readonly object _preloadLock = new object();

        private CancellationTokenSource _preloadCts;

        private readonly object _preloadQueueLock = new object();

        private const int PreloadBatchSize = 20;

        private const long MaxCacheSizeBytes = 3L * 1024 * 1024 * 1024; // 3 GB (supports multiple multi-hour tracks >200MB each)

        private const int MaxCacheFileCount = 100;

        private List<PlaylistItem> _playlist = new List<PlaylistItem>();

        private int _currentPlaylistIndex = -1;

        private string _currentPlaylistId = null;

        public event EventHandler<string> StatusMessageChanged;

        public event EventHandler<string> TitleChanged;

        public event EventHandler<string> ThumbnailUrlReady;

        public event EventHandler<TrackLoadedEventArgs> TrackLoaded;

        public event EventHandler PlaybackStateChanged;

        public event EventHandler PlaybackEnded;

        public event EventHandler PlaybackLooped;

        public event EventHandler<List<PlaylistItem>> PlaylistLoaded;

        public event EventHandler<int> PlaylistIndexChanged;

        public List<PlaylistItem> Playlist

        {

            get { return _playlist; }

        }

        public string CurrentPlaylistId

        {

            get { return _currentPlaylistId; }

        }

        public int CurrentPlaylistIndex

        {

            get { return _currentPlaylistIndex; }

        }

        public bool HasPlaylist

        {

            get { return _playlist != null && _playlist.Count > 0; }

        }

        public bool CanPlayNext

        {

            get { return HasPlaylist && (_currentPlaylistIndex + 1 < _playlist.Count || _isLooping); }

        }

        public bool IsLooping

        {

            get { return _isLooping; }

            set

            {

                _isLooping = value;

                lock (_lock)

                {

                    if (_managedStream != null)

                    {

                        _managedStream.IsLooping = value;

                    }

                }

            }

        }

        internal void NotifyLoopRestart()

        {

            _syncContext.Post(delegate

            {

                if (PlaybackLooped != null)

                {

                    PlaybackLooped(this, EventArgs.Empty);

                }

            }, null);

        }

        internal void RaiseTitleChanged(string title)

        {

            _syncContext.Post(delegate

            {

                if (TitleChanged != null)

                {

                    TitleChanged(this, title ?? string.Empty);

                }

            }, null);

        }

        public AudioPlayerService()

        {

            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

            try

            {

                AppPaths.EnsureDirectoriesExist();
                _cacheDirectory = AppPaths.TempAudioDir;
            }
            catch
            {
                _cacheDirectory = AppPaths.TempAudioDir;

                if (!Directory.Exists(_cacheDirectory))

                {

                    Directory.CreateDirectory(_cacheDirectory);

                }

            }

            // Clean any stale leftover audio cache from previous crashed sessions

            ClearAudioCache();

        }

        public void MaintainCache(string currentActiveFile = null)

        {

            try

            {

                if (string.IsNullOrEmpty(_cacheDirectory) || !Directory.Exists(_cacheDirectory)) return;

                DirectoryInfo di = new DirectoryInfo(_cacheDirectory);

                var audioFiles = new List<FileInfo>();

                long totalSize = 0;

                foreach (var fi in di.GetFiles())

                {

                    string ext = fi.Extension.ToLower();

                    if (ext == ".webm" || ext == ".m4a" || ext == ".mp3" || ext == ".opus" || ext == ".aac")

                    {

                        audioFiles.Add(fi);

                        totalSize += fi.Length;

                    }

                    else if (ext == ".part" || ext == ".ytdl" || ext == ".tmp" || ext == ".temp")

                    {

                        // Delete partial files older than 5 minutes

                        if (DateTime.Now - fi.LastWriteTime > TimeSpan.FromMinutes(5))

                        {

                            try { fi.Delete(); } catch { }

                        }

                    }

                }

                // If within cache budget, keep everything for instant replay!

                if (totalSize <= MaxCacheSizeBytes && audioFiles.Count <= MaxCacheFileCount) return;

                // Prioritize cleaning non-history files first, then oldest modified (LRU)

                var historyIds = HistoryService.GetHistoryIdSet();

                audioFiles.Sort((a, b) =>

                {

                    string idA = Path.GetFileNameWithoutExtension(a.Name);

                    string idB = Path.GetFileNameWithoutExtension(b.Name);

                    bool inHistA = historyIds.Contains(idA);

                    bool inHistB = historyIds.Contains(idB);

                    if (inHistA != inHistB)

                    {

                        return inHistA ? 1 : -1; // non-history files cleaned first

                    }

                    return a.LastWriteTime.CompareTo(b.LastWriteTime);

                });

                foreach (var fi in audioFiles)

                {

                    if (totalSize <= (long)(MaxCacheSizeBytes * 0.75) && audioFiles.Count <= MaxCacheFileCount - 5) break;

                    // Never delete active playing track or upcoming preloaded track

                    if (!string.IsNullOrEmpty(currentActiveFile) && fi.FullName.Equals(currentActiveFile, StringComparison.OrdinalIgnoreCase))

                    {

                        continue;

                    }

                    if (!string.IsNullOrEmpty(_preloadingVideoId) && fi.Name.StartsWith(_preloadingVideoId, StringComparison.OrdinalIgnoreCase))

                    {

                        continue;

                    }

                    try

                    {

                        totalSize -= fi.Length;

                        fi.Delete();

                    }

                    catch { }

                }

            }

            catch { }

        }

        public void CleanOldCache(string keepFile = null)

        {

            MaintainCache(keepFile);

        }

        public void ClearAudioCache()

        {

            try

            {

                HistoryService.CleanNonHistoryTempFiles();

            }

            catch { }

        }

        private void CancelActiveDownload()

        {

            lock (_downloadLock)

            {

                if (_activeDownloadProc != null)

                {

                    try

                    {

                        if (!_activeDownloadProc.HasExited)

                        {

                            _activeDownloadProc.Kill();

                        }

                    }

                    catch { }

                    _activeDownloadProc = null;

                }

            }

        }

        private void CancelPreload()

        {

            lock (_preloadQueueLock)

            {

                if (_preloadCts != null)

                {

                    try { _preloadCts.Cancel(); } catch { }

                    _preloadCts = null;

                }

            }

            CancelPreloadProcess();

        }

        private void CancelPreloadProcess()

        {

            lock (_preloadLock)

            {

                if (_preloadProc != null)

                {

                    try

                    {

                        if (!_preloadProc.HasExited)

                        {

                            _preloadProc.Kill();

                        }

                    }

                    catch { }

                    _preloadProc = null;

                    _preloadingVideoId = null;

                }

            }

        }

        private void SchedulePreloadPlaylistAhead()

        {

            lock (_preloadQueueLock)

            {

                if (_preloadCts != null)

                {

                    try { _preloadCts.Cancel(); } catch { }

                    _preloadCts = null;

                }

                _preloadCts = new CancellationTokenSource();

                var token = _preloadCts.Token;

                ThreadPool.QueueUserWorkItem(delegate

                {

                    try

                    {

                        // Wait 3s so current track starts playing smoothly before background downloads begin

                        Thread.Sleep(3000);

                        if (token.IsCancellationRequested) return;

                        List<string> targetIds = new List<string>();

                        lock (_lock)

                        {

                            if (_playlist != null && _playlist.Count > 0 && _currentPlaylistIndex >= 0)

                            {

                                int totalToQueue = Math.Min(PreloadBatchSize, _playlist.Count - 1);

                                for (int i = 1; i <= totalToQueue; i++)

                                {

                                    int targetIdx = (_currentPlaylistIndex + i);

                                    if (targetIdx >= _playlist.Count)

                                    {

                                        if (_isLooping)

                                        {

                                            targetIdx = targetIdx % _playlist.Count;

                                        }

                                        else

                                        {

                                            break;

                                        }

                                    }

                                    if (targetIdx >= 0 && targetIdx < _playlist.Count)

                                    {

                                        string id = _playlist[targetIdx].Id;

                                        if (!string.IsNullOrEmpty(id) && !targetIds.Contains(id))

                                        {

                                            targetIds.Add(id);

                                        }

                                    }

                                }

                            }

                        }

                        if (targetIds.Count == 0) return;

                        foreach (string vid in targetIds)

                        {

                            if (token.IsCancellationRequested) break;

                            // Skip if already in cache

                            if (!string.IsNullOrEmpty(FindCachedAudioFile(vid))) continue;

                            // Preload track

                            bool success = PreloadSingleTrackAudio(vid, token);

                            if (!success || token.IsCancellationRequested) break;

                            // Small delay between downloads so network has breathing room

                            Thread.Sleep(300);

                        }

                    }

                    catch { }

                });

            }

        }

        private bool PreloadSingleTrackAudio(string videoId, CancellationToken token)

        {

            if (string.IsNullOrEmpty(videoId) || token.IsCancellationRequested) return false;

            lock (_preloadLock)

            {

                if (token.IsCancellationRequested) return false;

                CancelPreloadProcess();

                string ytdlpPath = FindYtDlp();

                if (string.IsNullOrEmpty(ytdlpPath)) return false;

                _preloadingVideoId = videoId;

                try

                {

                    ProcessStartInfo psi = new ProcessStartInfo();

                    psi.FileName = ytdlpPath;

                    psi.WorkingDirectory = _cacheDirectory;

                    psi.Arguments = string.Format("-f \"ba[ext=webm]/ba[ext=m4a]/ba/b[height<=480]\" -N 4 --no-playlist --no-progress --no-part --no-mtime --no-warnings --no-check-certificates -o \"{0}\\%(id)s.%(ext)s\" \"https://www.youtube.com/watch?v={1}\"", _cacheDirectory, videoId);

                    psi.CreateNoWindow = true;

                    psi.UseShellExecute = false;

                    psi.RedirectStandardOutput = false;

                    psi.RedirectStandardError = false;

                    _preloadProc = Process.Start(psi);

                    if (_preloadProc != null)

                    {

                        try { _preloadProc.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }

                        

                        while (!_preloadProc.WaitForExit(500))

                        {

                            if (token.IsCancellationRequested)

                            {

                                try { _preloadProc.Kill(); } catch { }

                                return false;

                            }

                        }

                    }

                    return !string.IsNullOrEmpty(FindCachedAudioFile(videoId));

                }

                catch

                {

                    return false;

                }

                finally

                {

                    _preloadProc = null;

                    _preloadingVideoId = null;

                }

            }

        }

        public bool HasTrack

        {

            get

            {

                lock (_lock)

                {

                    return (_managedStream != null || _audioReader != null) && _totalDuration > TimeSpan.Zero;

                }

            }

        }

        public bool IsPlaying

        {

            get

            {

                lock (_lock)

                {

                    return _outputDevice != null && _outputDevice.PlaybackState == PlaybackState.Playing;

                }

            }

        }

        public float Volume

        {

            get { return _volume; }

            set

            {

                _volume = Math.Max(0.0f, Math.Min(1.0f, value));

                lock (_lock)

                {

                    if (_managedStream != null)

                    {

                        _managedStream.Volume = _volume;

                    }

                }

            }

        }

        public bool IsLoading

        {

            get { return _isLoading; }

        }

        public string CurrentTitle

        {

            get { return _currentTitle; }

        }

        public string CurrentVideoId

        {

            get { return _currentVideoId; }

        }

        public TimeSpan Position

        {

            get

            {

                lock (_lock)

                {

                    try

                    {

                        WaveStream stream = (WaveStream)_managedStream ?? _audioReader;

                        if (stream != null)

                        {

                            TimeSpan pos = stream.CurrentTime;

                            if (_totalDuration > TimeSpan.Zero && pos > _totalDuration)

                            {

                                return _totalDuration;

                            }

                            return pos;

                        }

                    }

                    catch { }

                    return TimeSpan.Zero;

                }

            }

            set

            {

                lock (_lock)

                {

                    try

                    {

                        TimeSpan target = value;

                        if (target < TimeSpan.Zero) target = TimeSpan.Zero;

                        if (_totalDuration > TimeSpan.Zero)

                        {

                            TimeSpan safeMax = _totalDuration > TimeSpan.FromSeconds(1)

                                ? _totalDuration - TimeSpan.FromMilliseconds(300)

                                : _totalDuration;

                            if (target > safeMax) target = safeMax;

                        }

                        if (_managedStream != null)

                        {

                            _managedStream.CurrentTime = target;

                            _managedStream.ResetSilence();

                        }

                        else if (_audioReader != null)

                        {

                            _audioReader.CurrentTime = target;

                        }

                    }

                    catch { }

                }

            }

        }

        public TimeSpan TotalDuration

        {

            get { return _totalDuration; }

        }

        public float Progress

        {

            get

            {

                TimeSpan total = TotalDuration;

                if (total <= TimeSpan.Zero) return 0f;

                float p = (float)(Position.TotalSeconds / total.TotalSeconds);

                return Math.Max(0f, Math.Min(1f, p));

            }

        }

        public void Play()

        {

            lock (_lock)

            {

                try

                {

                    WaveStream stream = (WaveStream)_managedStream ?? _audioReader;

                    if (_outputDevice != null && stream != null)

                    {

                        // If track has reached the end or near the end, rewind to start so it replays seamlessly

                        if (_totalDuration > TimeSpan.Zero && stream.CurrentTime >= _totalDuration - TimeSpan.FromMilliseconds(500))

                        {

                            stream.CurrentTime = TimeSpan.Zero;

                            if (_managedStream != null) _managedStream.ResetSilence();

                        }

                        _outputDevice.Play();

                        RaisePlaybackStateChanged();

                        string display = !string.IsNullOrEmpty(_currentTitle) ? _currentTitle : "Đang phát nhạc";

                        RaiseStatus(display);

                    }

                }

                catch { }

            }

        }

        public void Pause()

        {

            lock (_lock)

            {

                try

                {

                    if (_outputDevice != null)

                    {

                        _outputDevice.Pause();

                        RaisePlaybackStateChanged();

                    }

                }

                catch { }

            }

        }

        public void StopAndClear()

        {

            CancelActiveDownload();

            CancelPreload();

            lock (_lock)

            {

                CleanupCurrentTrack();

                _currentVideoId = null;

                _currentTitle = string.Empty;

                _totalDuration = TimeSpan.Zero;

            }

            MaintainCache();

            RaisePlaybackStateChanged();

            RaiseTitleChanged(string.Empty);

            RaiseStatus(null);

            MemoryOptimizer.TrimMemoryAsync(100);

        }

        public void TogglePlayPause()

        {

            if (IsPlaying) Pause();

            else Play();

        }

        public void SeekNormalized(float progress)

        {

            TimeSpan total = TotalDuration;

            if (total > TimeSpan.Zero)

            {

                float clamped = Math.Max(0f, Math.Min(1f, progress));

                TimeSpan target = TimeSpan.FromSeconds(total.TotalSeconds * clamped);

                Position = target;

            }

        }

        public void PlayNext()

        {

            if (!HasPlaylist) return;

            int next = _currentPlaylistIndex + 1;

            if (next >= _playlist.Count)

            {

                if (_isLooping)

                {

                    next = 0;

                }

                else

                {

                    RaiseStatus("Đã hết danh sách phát.");

                    return;

                }

            }

            PlayTrackAtIndex(next);

        }

        public void PlayPrevious()

        {

            if (Position.TotalSeconds > 3.0)

            {

                Position = TimeSpan.Zero;

                return;

            }

            if (!HasPlaylist)

            {

                Position = TimeSpan.Zero;

                return;

            }

            int prev = _currentPlaylistIndex - 1;

            if (prev < 0)

            {

                prev = _playlist.Count - 1;

            }

            PlayTrackAtIndex(prev);

        }

        public void ClearPlaylist()

        {

            _playlist = new List<PlaylistItem>();

            _currentPlaylistId = null;

            _currentPlaylistIndex = -1;

            _syncContext.Post(delegate

            {

                if (PlaylistLoaded != null)

                {

                    PlaylistLoaded(this, _playlist);

                }

                if (PlaylistIndexChanged != null)

                {

                    PlaylistIndexChanged(this, -1);

                }

            }, null);

        }

        public void AddToPlaylist(PlaylistItem item)

        {

            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;

            lock (_lock)

            {

                if (_playlist == null) _playlist = new List<PlaylistItem>();

                if (!_playlist.Exists(x => x.Id == item.Id))

                {

                    _playlist.Add(new PlaylistItem

                    {

                        Id = item.Id,

                        Title = item.Title,

                        DurationSeconds = item.DurationSeconds

                    });

                }

            }

            _syncContext.Post(delegate

            {

                if (PlaylistLoaded != null)

                {

                    PlaylistLoaded(this, _playlist);

                }

            }, null);

        }

        public void RemoveFromPlaylist(int index)

        {

            lock (_lock)

            {

                if (_playlist == null || index < 0 || index >= _playlist.Count) return;

                _playlist.RemoveAt(index);

                if (_currentPlaylistIndex == index)

                {

                    _currentPlaylistIndex = -1;

                }

                else if (_currentPlaylistIndex > index)

                {

                    _currentPlaylistIndex--;

                }

            }

            _syncContext.Post(delegate

            {

                if (PlaylistLoaded != null)

                {

                    PlaylistLoaded(this, _playlist);

                }

                if (PlaylistIndexChanged != null)

                {

                    PlaylistIndexChanged(this, _currentPlaylistIndex);

                }

            }, null);

        }

        public void PlayTrackAtIndex(int index)

        {

            var pl = _playlist;

            if (pl == null || index < 0 || index >= pl.Count) return;

            _currentPlaylistIndex = index;

            _syncContext.Post(delegate

            {

                if (PlaylistIndexChanged != null)

                {

                    PlaylistIndexChanged(this, _currentPlaylistIndex);

                }

            }, null);

            PlaylistItem item = pl[index];

            if (!string.IsNullOrEmpty(item.Title))

            {

                _currentTitle = item.Title;

                RaiseTitleChanged(_currentTitle);

            }

            LoadAndPlay(item.Url);

        }

        public void LoadPlaylistAsync(string playlistUrl, string currentVideoId = null, bool playFirstIfNoVideo = false)

        {

            ThreadPool.QueueUserWorkItem(delegate

            {

                try

                {

                    string ytdlpPath = FindYtDlp();

                    if (string.IsNullOrEmpty(ytdlpPath)) return;

                    string listId = ExtractListId(playlistUrl);

                    string targetUrl = playlistUrl.Trim();

                    // If it's a standard user playlist (starts with PL) or album (OLAK), direct playlist link is cleanest

                    if (!string.IsNullOrEmpty(listId) && (listId.StartsWith("PL", StringComparison.OrdinalIgnoreCase) || listId.StartsWith("OLAK", StringComparison.OrdinalIgnoreCase)))

                    {

                        targetUrl = "https://www.youtube.com/playlist?list=" + listId;

                    }

                    else if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))

                    {

                        targetUrl = "https://" + targetUrl;

                    }

                    ProcessStartInfo psi = new ProcessStartInfo();

                    psi.FileName = ytdlpPath;

                    psi.Arguments = string.Format("--encoding utf-8 --flat-playlist --print \"%(id)s\t%(title)s\t%(duration)s\" \"{0}\"", targetUrl);

                    psi.CreateNoWindow = true;

                    psi.UseShellExecute = false;

                    psi.RedirectStandardOutput = true;

                    psi.RedirectStandardError = true;

                    psi.StandardOutputEncoding = System.Text.Encoding.UTF8;

                    psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                    List<PlaylistItem> list = new List<PlaylistItem>();

                    string errOutput = string.Empty;

                    bool hasStartedFirst = false;

                    using (Process proc = Process.Start(psi))

                    {

                        var errTask = System.Threading.Tasks.Task.Run(() => proc.StandardError.ReadToEnd());

                        string line;

                        while ((line = proc.StandardOutput.ReadLine()) != null)

                        {

                            line = line.Trim();

                            if (string.IsNullOrEmpty(line)) continue;

                            string[] parts = line.Split('\t');

                            if (parts.Length >= 2)

                            {

                                string id = parts[0].Trim();

                                string title = parts[1].Trim();

                                int dur = 0;

                                if (parts.Length >= 3)

                                {

                                    int.TryParse(parts[2].Trim(), out dur);

                                }

                                if (id.Length == 11)

                                {

                                    var item = new PlaylistItem { Id = id, Title = title, DurationSeconds = dur };

                                    list.Add(item);

                                    // Fast-start: As soon as the first track is found, begin playing it immediately!

                                    if (playFirstIfNoVideo && string.IsNullOrEmpty(currentVideoId) && !hasStartedFirst && list.Count == 1)

                                    {

                                        hasStartedFirst = true;

                                        _playlist = new List<PlaylistItem>(list);

                                        _currentPlaylistId = listId;

                                        _currentPlaylistIndex = 0;

                                        _currentTitle = title;

                                        RaiseTitleChanged(_currentTitle);

                                        _syncContext.Post(delegate

                                        {

                                            if (PlaylistLoaded != null) PlaylistLoaded(this, _playlist);

                                            if (PlaylistIndexChanged != null) PlaylistIndexChanged(this, 0);

                                        }, null);

                                        LoadAndPlay(item.Url);

                                    }

                                }

                            }

                        }

                        if (!proc.WaitForExit(90000))

                        {

                            try { proc.Kill(); } catch { }

                        }

                        try { errOutput = errTask.Result; } catch { }

                    }

                    bool isPrivate = (!string.IsNullOrEmpty(errOutput)) &&

                                     (errOutput.Contains("playlist does not exist") ||

                                      errOutput.Contains("Unable to recognize playlist") ||

                                      errOutput.Contains("This playlist is private"));

                    if (isPrivate && list.Count <= 1)

                    {

                        RaiseStatus("Playlist đang 'Riêng tư' trên YouTube. Hãy đổi sang 'Không công khai' để nghe đủ bài!");

                    }

                    if (list.Count > 0)

                    {

                        _playlist = list;

                        _currentPlaylistId = listId;

                        int foundIdx = -1;

                        if (!string.IsNullOrEmpty(currentVideoId))

                        {

                            foundIdx = list.FindIndex(x => x.Id == currentVideoId);

                        }

                        _currentPlaylistIndex = foundIdx;

                        if (foundIdx >= 0 && !string.IsNullOrWhiteSpace(list[foundIdx].Title))

                        {

                            string validTitle = list[foundIdx].Title;

                            if (_currentVideoId == currentVideoId)

                            {

                                _currentTitle = validTitle;

                                RaiseTitleChanged(_currentTitle);

                            }

                            try { HistoryService.UpdateTitle(currentVideoId, validTitle); } catch { }

                        }

                        // Sync any known titles in this playlist to history if those tracks are in history

                        try

                        {

                            foreach (var pItem in list)

                            {

                                if (!string.IsNullOrWhiteSpace(pItem.Title) && pItem.Title != "None" && pItem.Title != "NA")

                                {

                                    if (HistoryService.ContainsTrack(pItem.Id))

                                    {

                                        HistoryService.UpdateTitle(pItem.Id, pItem.Title);

                                    }

                                }

                            }

                        }

                        catch { }

                        _syncContext.Post(delegate

                        {

                            if (PlaylistLoaded != null)

                            {

                                PlaylistLoaded(this, _playlist);

                            }

                            if (PlaylistIndexChanged != null)

                            {

                                PlaylistIndexChanged(this, _currentPlaylistIndex);

                            }

                        }, null);

                        // Asynchronously resolve missing titles via oEmbed

                        var missingTitleItems = list.FindAll(x => string.IsNullOrWhiteSpace(x.Title) || x.Title.Trim() == "None" || x.Title.Trim() == "NA");

                        if (missingTitleItems.Count > 0)

                        {

                            ThreadPool.QueueUserWorkItem(delegate

                            {

                                bool anyUpdated = false;

                                foreach (var item in missingTitleItems)

                                {

                                    try

                                    {

                                        using (WebClient wc = new WebClient())

                                        {

                                            wc.Encoding = System.Text.Encoding.UTF8;

                                            string oembedUrl = string.Format("https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={0}&format=json", item.Id);

                                            string json = wc.DownloadString(oembedUrl);

                    Match m = Regex.Match(json, "\"title\":\"((?:\\\\.|[^\"])*)\"");

                                            if (m.Success)

                                            {

                                                item.Title = Regex.Unescape(m.Groups[1].Value);

                                                anyUpdated = true;

                                                try { HistoryService.UpdateTitle(item.Id, item.Title); } catch { }

                                                if (_currentVideoId == item.Id)

                                                {

                                                    _currentTitle = item.Title;

                                                    RaiseTitleChanged(_currentTitle);

                                                }

                                            }

                                        }

                                    }

                                    catch { }

                                }

                                if (anyUpdated)

                                {

                                    _syncContext.Post(delegate

                                    {

                                        if (PlaylistLoaded != null)

                                        {

                                            PlaylistLoaded(this, _playlist);

                                        }

                                    }, null);

                                }

                            });

                        }

                        if (playFirstIfNoVideo && string.IsNullOrEmpty(currentVideoId) && !hasStartedFirst)

                        {

                            PlayTrackAtIndex(0);

                        }

                        else

                        {

                            SchedulePreloadPlaylistAhead();

                        }

                    }

                    else

                    {

                        _playlist = new List<PlaylistItem>();

                        _currentPlaylistId = null;

                        _currentPlaylistIndex = -1;

                        _syncContext.Post(delegate

                        {

                            if (PlaylistLoaded != null)

                            {

                                PlaylistLoaded(this, _playlist);

                            }

                            if (PlaylistIndexChanged != null)

                            {

                                PlaylistIndexChanged(this, -1);

                            }

                        }, null);

                        RaiseStatus("Không tìm thấy bài hát nào trong playlist hoặc link không hợp lệ.");

                    }

                }

                catch { }

            });

        }

        public void LoadAndPlay(string input)

        {

            if (string.IsNullOrWhiteSpace(input)) return;

            input = input.Trim();

            _isLoading = true;

            RaiseStatus("Đang tìm bài hát...");

            ThreadPool.QueueUserWorkItem(delegate

            {

                try

                {

                    string videoId = ExtractVideoId(input);

                    string listId = ExtractListId(input);

                    string ytdlpPath = FindYtDlp();

                    if (string.IsNullOrEmpty(ytdlpPath))

                    {

                        _isLoading = false;

                        RaiseStatus("Lỗi: Không tìm thấy yt-dlp.");

                        return;

                    }

                    if (!string.IsNullOrEmpty(listId))

                    {

                        if (!string.Equals(listId, _currentPlaylistId, StringComparison.OrdinalIgnoreCase))

                        {

                            LoadPlaylistAsync(input, videoId, string.IsNullOrEmpty(videoId));

                            if (string.IsNullOrEmpty(videoId))

                            {

                                return;

                            }

                        }

                    }

                    if (!string.IsNullOrEmpty(videoId))

                    {

                        bool isNewTrack = !string.Equals(_currentVideoId, videoId, StringComparison.OrdinalIgnoreCase);

                        _currentVideoId = videoId;

                        string resolvedTitle = null;

                        if (_playlist != null && _playlist.Count > 0)

                        {

                            var pl = _playlist;

                            int matchIdx = pl.FindIndex(x => x.Id == videoId);

                            if (matchIdx >= 0 && matchIdx < pl.Count)

                            {

                                _currentPlaylistIndex = matchIdx;

                                if (!string.IsNullOrWhiteSpace(pl[matchIdx].Title) && pl[matchIdx].Title != "None" && pl[matchIdx].Title != "NA")

                                {

                                    resolvedTitle = pl[matchIdx].Title;

                                }

                                _syncContext.Post(delegate

                                {

                                    if (PlaylistIndexChanged != null)

                                    {

                                        PlaylistIndexChanged(this, _currentPlaylistIndex);

                                    }

                                }, null);

                            }

                            else if (string.IsNullOrEmpty(listId))

                            {

                                _currentPlaylistIndex = -1;

                                _syncContext.Post(delegate

                                {

                                    if (PlaylistIndexChanged != null)

                                    {

                                        PlaylistIndexChanged(this, -1);

                                    }

                                }, null);

                            }

                        }

                        // If not found in playlist, check HistoryService and SavedTracksService

                        if (string.IsNullOrWhiteSpace(resolvedTitle))

                        {

                            resolvedTitle = HistoryService.GetTrackTitle(videoId);

                        }

                        if (string.IsNullOrWhiteSpace(resolvedTitle))

                        {

                            resolvedTitle = SavedTracksService.Instance.GetTrackTitle(videoId);

                        }

                        // CRITICAL: Never retain previous track's title when playing a new track!

                        if (isNewTrack)

                        {

                            _currentTitle = resolvedTitle ?? string.Empty;

                            if (!string.IsNullOrWhiteSpace(_currentTitle))

                            {

                                RaiseTitleChanged(_currentTitle);

                            }

                        }

                        else if (!string.IsNullOrWhiteSpace(resolvedTitle))

                        {

                            _currentTitle = resolvedTitle;

                            RaiseTitleChanged(_currentTitle);

                        }

                        // Immediate thumbnail display (instant visual feedback)

                        string thumb = string.Format("https://img.youtube.com/vi/{0}/hqdefault.jpg", videoId);

                        RaiseThumbnail(thumb);

                        // If title is not yet known or placeholder, fetch asynchronously via oEmbed

                        if (string.IsNullOrWhiteSpace(_currentTitle) || _currentTitle == "None" || _currentTitle == "NA")

                        {

                            ThreadPool.QueueUserWorkItem(delegate { FetchVideoTitle(videoId); });

                        }

                    }

                    // Cancel any previous user-initiated download so 100% bandwidth goes to this song

                    CancelActiveDownload();

                    // Check if already cached

                    string audioPath = FindCachedAudioFile(videoId);

                    // If not in cache, check if currently being preloaded

                    if (string.IsNullOrEmpty(audioPath))

                    {

                        bool preloadingSame = false;

                        lock (_preloadLock)

                        {

                            if (!string.IsNullOrEmpty(videoId) && _preloadingVideoId == videoId && _preloadProc != null)

                            {

                                preloadingSame = true;

                            }

                        }

                        if (preloadingSame)

                        {

                            RaiseStatus("Đang nạp âm thanh...");

                            try

                            {

                                if (_preloadProc != null)

                                {

                                    _preloadProc.WaitForExit(120000); // 2 minutes for large tracks

                                }

                            }

                            catch { }

                            audioPath = FindCachedAudioFile(videoId);

                        }

                        else

                        {

                            // Cancel preloading of any different track to free up full bandwidth

                            CancelPreload();

                        }

                    }

                    if (string.IsNullOrEmpty(audioPath))

                    {

                        RaiseStatus("Đang tải âm thanh từ YouTube...");

                        string targetArg = !string.IsNullOrEmpty(videoId)

                            ? string.Format("https://www.youtube.com/watch?v={0}", videoId)

                            : string.Format("ytsearch1:\"{0}\"", input);

                        ProcessStartInfo psi = new ProcessStartInfo();

                        psi.FileName = ytdlpPath;

                        psi.WorkingDirectory = _cacheDirectory;

                        psi.Arguments = string.Format("-f \"ba[ext=webm]/ba[ext=m4a]/ba/b[height<=480]\" -N 4 --no-playlist --no-progress --no-part --no-mtime --no-warnings --no-check-certificates -o \"{0}\\%(id)s.%(ext)s\" \"{1}\"", _cacheDirectory, targetArg);

                        psi.CreateNoWindow = true;

                        psi.UseShellExecute = false;

                        psi.RedirectStandardOutput = false;

                        psi.RedirectStandardError = false;

                        lock (_downloadLock)

                        {

                            _activeDownloadProc = Process.Start(psi);

                        }

                        if (_activeDownloadProc != null)

                        {

                            if (!_activeDownloadProc.WaitForExit(180000)) // 3 minutes for large tracks >200MB

                            {

                                try { _activeDownloadProc.Kill(); } catch { }

                            }

                            lock (_downloadLock)

                            {

                                _activeDownloadProc = null;

                            }

                        }

                        // Search for the downloaded file

                        audioPath = FindCachedAudioFile(videoId);

                        // If videoId was empty (search query), search for the most recent file in cache

                        if (string.IsNullOrEmpty(audioPath))

                        {

                            audioPath = FindMostRecentAudioFile();

                            if (!string.IsNullOrEmpty(audioPath))

                            {

                                string fname = Path.GetFileNameWithoutExtension(audioPath);

                                if (fname.Length == 11)

                                {

                                    _currentVideoId = fname;

                                    ThreadPool.QueueUserWorkItem(delegate { FetchVideoTitle(fname); });

                                    string thumb = string.Format("https://img.youtube.com/vi/{0}/hqdefault.jpg", fname);

                                    RaiseThumbnail(thumb);

                                }

                            }

                        }

                        if (string.IsNullOrEmpty(audioPath))

                        {

                            _isLoading = false;

                            RaiseStatus("Không thể tải audio từ YouTube.");

                            return;

                        }

                    }

                    _syncContext.Post(delegate

                    {

                        try

                        {

                            lock (_lock)

                            {

                                CleanupCurrentTrack();

                                _audioReader = new MediaFoundationReader(audioPath);

                                _totalDuration = _audioReader.TotalTime;

                                var clusters = WebMClusterParser.ParseClusters(audioPath);

                                _managedStream = new ManagedAudioStream(_audioReader, this, clusters);

                                _outputDevice = new WaveOutEvent();

                                _outputDevice.DeviceNumber = -1; // Default Windows playback device (Bluetooth / Headphones)

                                _outputDevice.DesiredLatency = 100; // Low latency for tight UI sync

                                _outputDevice.NumberOfBuffers = 3;

                                _outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;

                                _outputDevice.Init(_managedStream);

                                _outputDevice.Play();

                            }

                            // Maintain cache limits (LRU up to 300MB) keeping recently used tracks

                            ThreadPool.QueueUserWorkItem(delegate

                            {

                                MaintainCache(audioPath);

                            });

                            // Schedule preloading up to 20 tracks ahead in the background for zero-latency switching

                            SchedulePreloadPlaylistAhead();

                            _isLoading = false;

                            string titleForHistory = _currentTitle;

                            if (string.IsNullOrWhiteSpace(titleForHistory) || titleForHistory == "None" || titleForHistory == "NA")

                            {

                                if (_playlist != null)

                                {

                                    var match = _playlist.Find(x => x.Id == _currentVideoId);

                                    if (match != null && !string.IsNullOrWhiteSpace(match.Title) && match.Title != "None" && match.Title != "NA")

                                    {

                                        titleForHistory = match.Title;

                                        _currentTitle = match.Title;

                                    }

                                }

                            }

                            if (string.IsNullOrWhiteSpace(titleForHistory) || titleForHistory == "None" || titleForHistory == "NA")

                            {

                                string histTitle = HistoryService.GetTrackTitle(_currentVideoId);

                                if (!string.IsNullOrWhiteSpace(histTitle))

                                {

                                    titleForHistory = histTitle;

                                    _currentTitle = histTitle;

                                }

                            }

                            string display = !string.IsNullOrEmpty(_currentTitle) ? _currentTitle : "Đang phát nhạc";

                            RaiseStatus(display);

                            RaisePlaybackStateChanged();

                            if (TrackLoaded != null)

                            {

                                TrackLoaded(this, new TrackLoadedEventArgs

                                {

                                    VideoId = _currentVideoId,

                                    Title = _currentTitle,

                                    Duration = _totalDuration

                                });

                            }

                            try

                            {

                                HistoryService.AddTrack(new PlaylistItem

                                {

                                    Id = _currentVideoId,

                                    Title = titleForHistory,

                                    DurationSeconds = (int)_totalDuration.TotalSeconds

                                });

                            }

                            catch { }

                            // Trim unreferenced memory & working set after track initialization

                            MemoryOptimizer.TrimMemoryAsync(800);

                        }

                        catch (Exception ex)

                        {

                            _isLoading = false;

                            RaiseStatus("Lỗi phát audio: " + ex.Message);

                        }

                    }, null);

                }

                catch (Exception ex)

                {

                    _isLoading = false;

                    RaiseStatus("Lỗi: " + ex.Message);

                }

            });

        }

        private string FindCachedAudioFile(string videoId)
        {
            if (string.IsNullOrEmpty(videoId)) return null;

            try
            {
                string[] prioritizedExts = new string[] { ".webm", ".opus", ".mp3", ".m4a", ".aac" };

                // 1. Check Saved/audio first (Highest priority: Offline music)
                foreach (string ext in prioritizedExts)
                {
                    string savedPath = Path.Combine(AppPaths.SavedAudioDir, videoId + ext);
                    if (File.Exists(savedPath) && new FileInfo(savedPath).Length > 50000)
                    {
                        return savedPath;
                    }
                }

                // 2. Check KahoiiTemp/audio
                if (!string.IsNullOrEmpty(_cacheDirectory) && Directory.Exists(_cacheDirectory))
                {
                    foreach (string ext in prioritizedExts)
                    {
                        string candidate = Path.Combine(_cacheDirectory, videoId + ext);
                        if (File.Exists(candidate))
                        {
                            FileInfo fi = new FileInfo(candidate);
                            if (fi.Length > 50000)
                            {
                                try { File.SetLastWriteTime(candidate, DateTime.Now); } catch { }
                                return candidate;
                            }
                        }
                    }
                }
            }
            catch { }

            return null;

        }

        private string FindMostRecentAudioFile()

        {

            if (string.IsNullOrEmpty(_cacheDirectory) || !Directory.Exists(_cacheDirectory)) return null;

            string bestFile = null;

            DateTime bestTime = DateTime.MinValue;

            try

            {

                string[] files = Directory.GetFiles(_cacheDirectory);

                foreach (string file in files)

                {

                    string ext = Path.GetExtension(file).ToLower();

                    if (ext == ".webm" || ext == ".m4a" || ext == ".mp3" || ext == ".opus" || ext == ".aac" || ext == ".ogg")

                    {

                        FileInfo fi = new FileInfo(file);

                        if (fi.Length > 1000 && fi.LastWriteTime > bestTime)

                        {

                            bestTime = fi.LastWriteTime;

                            bestFile = file;

                        }

                    }

                }

            }

            catch { }

            return bestFile;

        }

        private void OutputDevice_PlaybackStopped(object sender, StoppedEventArgs e)

        {

            _syncContext.Post(delegate

            {

                bool naturallyEnded = false;

                lock (_lock)

                {

                    WaveStream stream = (WaveStream)_managedStream ?? _audioReader;

                    if (stream != null && _totalDuration > TimeSpan.Zero)

                    {

                        if (stream.CurrentTime >= _totalDuration - TimeSpan.FromSeconds(1.5))

                        {

                            naturallyEnded = true;

                            try

                            {

                                stream.CurrentTime = TimeSpan.Zero;

                                if (_managedStream != null) _managedStream.ResetSilence();

                            }

                            catch { }

                        }

                    }

                }

                RaisePlaybackStateChanged();

                if (naturallyEnded)
                {
                    if (SleepTimerService.Instance.IsEndOfTrack)
                    {
                        SleepTimerService.Instance.Stop();
                        Pause();
                        return;
                    }

                    if (!_isLooping && HasPlaylist && CanPlayNext)

                    {

                        PlayNext();

                        return;

                    }

                    if (PlaybackEnded != null)

                    {

                        PlaybackEnded(this, EventArgs.Empty);

                    }

                }

            }, null);

        }

        private void CleanupCurrentTrack()

        {

            if (_outputDevice != null)

            {

                try

                {

                    _outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;

                    _outputDevice.Stop();

                    _outputDevice.Dispose();

                }

                catch { }

                _outputDevice = null;

            }

            if (_managedStream != null)

            {

                try

                {

                    _managedStream.Dispose();

                }

                catch { }

                _managedStream = null;

            }

            if (_audioReader != null)

            {

                try

                {

                    _audioReader.Dispose();

                }

                catch { }

                _audioReader = null;

            }

        }

        private void FetchVideoTitle(string videoId)

        {

            if (string.IsNullOrWhiteSpace(videoId)) return;

            try

            {

                using (WebClient wc = new WebClient())

                {

                    wc.Encoding = System.Text.Encoding.UTF8;

                    string oembedUrl = string.Format("https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={0}&format=json", videoId);

                    string json = wc.DownloadString(oembedUrl);

                    Match m = Regex.Match(json, "\"title\":\"((?:\\\\.|[^\"])*)\"");

                    if (m.Success)

                    {

                        string fetchedTitle = Regex.Unescape(m.Groups[1].Value);

                        if (!string.IsNullOrWhiteSpace(fetchedTitle))

                        {

                            try { HistoryService.UpdateTitle(videoId, fetchedTitle); } catch { }

                            if (_playlist != null)

                            {

                                var matchItem = _playlist.Find(x => x.Id == videoId);

                                if (matchItem != null && matchItem.Title != fetchedTitle)

                                {

                                    matchItem.Title = fetchedTitle;

                                    _syncContext.Post(delegate

                                    {

                                        if (PlaylistLoaded != null)

                                        {

                                            PlaylistLoaded(this, _playlist);

                                        }

                                    }, null);

                                }

                            }

                            if (string.Equals(_currentVideoId, videoId, StringComparison.OrdinalIgnoreCase))

                            {

                                _currentTitle = fetchedTitle;

                                RaiseTitleChanged(_currentTitle);

                                if (!_isLoading)

                                {

                                    RaiseStatus(_currentTitle);

                                }

                            }

                        }

                    }

                }

            }

            catch { }

        }

        public void EnsureTrackTitleResolved(string videoId)

        {

            if (string.IsNullOrWhiteSpace(videoId)) return;

            ThreadPool.QueueUserWorkItem(delegate

            {

                FetchVideoTitle(videoId);

            });

        }

        public static string FindYtDlp()

        {

            // 1. Local directory next to executable

            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");

            if (File.Exists(localPath) && new FileInfo(localPath).Length > 1000000) return localPath;

            // 2. Cached in AppData directory

            string cachedYtDlp = Path.Combine(AppPaths.TempDir, "yt-dlp.exe");

            if (File.Exists(cachedYtDlp) && new FileInfo(cachedYtDlp).Length > 1000000)

            {

                return cachedYtDlp;

            }

            // 3. Project directory or Python Scripts path

            string projPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\yt-dlp.exe");

            if (File.Exists(projPath) && new FileInfo(projPath).Length > 1000000) return Path.GetFullPath(projPath);

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string pyPath = Path.Combine(userProfile, @"AppData\Roaming\Python\Python314\Scripts\yt-dlp.exe");

            if (File.Exists(pyPath)) return pyPath;

            // 4. Extract from Embedded Resource

            try

            {

                Assembly asm = Assembly.GetExecutingAssembly();

                foreach (string resName in asm.GetManifestResourceNames())

                {

                    if (resName.EndsWith("yt-dlp.exe", StringComparison.OrdinalIgnoreCase))

                    {

                        if (!Directory.Exists(AppPaths.TempDir))

                        {

                            Directory.CreateDirectory(AppPaths.TempDir);

                        }

                        using (Stream stream = asm.GetManifestResourceStream(resName))

                        using (FileStream fs = new FileStream(cachedYtDlp, FileMode.Create, FileAccess.Write, FileShare.None))

                        {

                            byte[] buffer = new byte[65536];

                            int read;

                            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)

                            {

                                fs.Write(buffer, 0, read);

                            }

                        }

                        return cachedYtDlp;

                    }

                }

            }

            catch { }

            return "yt-dlp";

        }

        public static string ExtractVideoId(string url)

        {

            if (string.IsNullOrWhiteSpace(url)) return null;

            url = url.Trim();

            if (url.Length == 11 && Regex.IsMatch(url, @"^[a-zA-Z0-9_-]{11}$"))

            {

                return url;

            }

            Match mShort = Regex.Match(url, @"youtu\.be\/([a-zA-Z0-9_-]{11})");

            if (mShort.Success)

            {

                return mShort.Groups[1].Value;

            }

            Match mLong = Regex.Match(url, @"(?:v=|embed\/|shorts\/|live\/)([a-zA-Z0-9_-]{11})");

            if (mLong.Success)

            {

                return mLong.Groups[1].Value;

            }

            Match mV = Regex.Match(url, @"[?&]v=([a-zA-Z0-9_-]{11})");

            if (mV.Success)

            {

                return mV.Groups[1].Value;

            }

            return null;

        }

        public static string ExtractListId(string url)

        {

            if (string.IsNullOrWhiteSpace(url)) return null;

            url = url.Trim();

            Match m = Regex.Match(url, @"[?&]list=([a-zA-Z0-9_-]+)");

            if (m.Success)

            {

                return m.Groups[1].Value;

            }

            return null;

        }

        private void RaiseThumbnail(string thumbUrl)

        {

            _syncContext.Post(delegate

            {

                if (ThumbnailUrlReady != null)

                {

                    ThumbnailUrlReady(this, thumbUrl);

                }

            }, null);

        }

        private void RaiseStatus(string message)

        {

            _syncContext.Post(delegate

            {

                if (StatusMessageChanged != null)

                {

                    StatusMessageChanged(this, message);

                }

            }, null);

        }

        private void RaisePlaybackStateChanged()

        {

            _syncContext.Post(delegate

            {

                if (PlaybackStateChanged != null)

                {

                    PlaybackStateChanged(this, EventArgs.Empty);

                }

            }, null);

        }

       

        public void Dispose()

        {

            CancelActiveDownload();

            CancelPreload();

            lock (_lock)

            {

                CleanupCurrentTrack();

            }

            try

            {

                GC.Collect();

                GC.WaitForPendingFinalizers();

            }

            catch { }

            // Clear all cached audio files when exiting the app as requested

            ClearAudioCache();

        }

    }

    public class ManagedAudioStream : WaveStream

    {

        private readonly WaveStream _source;

        private readonly AudioPlayerService _service;

        private readonly List<double> _clusters;

        private readonly object _syncLock = new object();

        private int _consecutiveSilentBytes = 0;

        private bool _isLooping = false;

        private long _prerollBytesToDiscard = 0;

        private TimeSpan _playbackBaseTime = TimeSpan.Zero;

        private long _bytesReadSinceSeek = 0;

        private readonly byte[] _discardBuffer = new byte[4096];

        private volatile float _volume = 1.0f;

        public ManagedAudioStream(WaveStream source, AudioPlayerService service, List<double> clusters = null)

        {

            _source = source;

            _service = service;

            _clusters = clusters;

            _isLooping = service.IsLooping;

            _volume = service.Volume;

        }

        public float Volume

        {

            get { return _volume; }

            set { _volume = Math.Max(0.0f, Math.Min(1.0f, value)); }

        }

        public bool IsLooping

        {

            get { return _isLooping; }

            set { _isLooping = value; }

        }

        public void ResetSilence()

        {

            _consecutiveSilentBytes = 0;

        }

        public override WaveFormat WaveFormat { get { return _source.WaveFormat; } }

        public override long Length { get { return _source.Length; } }

        public override long Position

        {

            get

            {

                lock (_syncLock)

                {

                    return (long)(CurrentTime.TotalSeconds * WaveFormat.AverageBytesPerSecond);

                }

            }

            set

            {

                CurrentTime = TimeSpan.FromSeconds((double)value / WaveFormat.AverageBytesPerSecond);

            }

        }

        public override TimeSpan TotalTime { get { return _service.TotalDuration; } }

        public override TimeSpan CurrentTime

        {

            get

            {

                lock (_syncLock)

                {

                    double playedSec = (double)_bytesReadSinceSeek / WaveFormat.AverageBytesPerSecond;

                    TimeSpan pos = _playbackBaseTime + TimeSpan.FromSeconds(playedSec);

                    if (_service.TotalDuration > TimeSpan.Zero && pos > _service.TotalDuration)

                    {

                        return _service.TotalDuration;

                    }

                    return pos;

                }

            }

            set

            {

                lock (_syncLock)

                {

                    try

                    {

                        TimeSpan total = _service.TotalDuration;

                        TimeSpan target = value;

                        if (target < TimeSpan.Zero) target = TimeSpan.Zero;

                        // Clamp away from EOF to prevent MF_E_INVALID_POSITION (COMException)

                        if (total > TimeSpan.Zero)

                        {

                            TimeSpan safeMax = total > TimeSpan.FromSeconds(1)

                                ? total - TimeSpan.FromMilliseconds(300)

                                : total;

                            if (target > safeMax) target = safeMax;

                        }

                        // WebM cluster keyframe alignment:

                        // Windows Media Foundation seeks to the nearest prior cluster keyframe.

                        // We find that keyframe and discard the preroll bytes so sound starts

                        // exactly at the target seek position with 0 drift and 0 overrun.

                        double targetSec = target.TotalSeconds;

                        double clusterTime = 0;

                        if (_clusters != null && _clusters.Count > 0)

                        {

                            for (int i = _clusters.Count - 1; i >= 0; i--)

                            {

                                if (_clusters[i] <= targetSec - 0.005)

                                {

                                    clusterTime = _clusters[i];

                                    break;

                                }

                            }

                        }

                        double prerollSec = Math.Max(0, targetSec - clusterTime);

                        _prerollBytesToDiscard = (long)(prerollSec * WaveFormat.AverageBytesPerSecond);

                        _prerollBytesToDiscard -= (_prerollBytesToDiscard % WaveFormat.BlockAlign);

                        _source.CurrentTime = target;

                        _playbackBaseTime = target;

                        _bytesReadSinceSeek = 0;

                        _consecutiveSilentBytes = 0;

                    }

                    catch (COMException) { }

                    catch { }

                }

            }

        }

        public override int Read(byte[] buffer, int offset, int count)

        {

            lock (_syncLock)

            {

                TimeSpan total = _service.TotalDuration;

                // 1. Discard preroll bytes so physical sound begins precisely at the seek target

                while (_prerollBytesToDiscard > 0)

                {

                    int toDiscard = (int)Math.Min(_discardBuffer.Length, _prerollBytesToDiscard);

                    int d = 0;

                    try

                    {

                        d = _source.Read(_discardBuffer, 0, toDiscard);

                    }

                    catch (COMException)

                    {

                        if (_isLooping) return LoopRestart(buffer, offset, count);

                        return 0;

                    }

                    catch

                    {

                        return 0;

                    }

                    if (d <= 0)

                    {

                        _prerollBytesToDiscard = 0;

                        break;

                    }

                    _prerollBytesToDiscard -= d;

                }

                int read = 0;

                try

                {

                    read = _source.Read(buffer, offset, count);

                }

                catch (COMException)

                {

                    if (_isLooping)

                    {

                        return LoopRestart(buffer, offset, count);

                    }

                    return 0;

                }

                catch

                {

                    return 0;

                }

                if (read <= 0)

                {

                    if (_isLooping)

                    {

                        return LoopRestart(buffer, offset, count);

                    }

                    return 0;

                }

                _bytesReadSinceSeek += read;

                // 2. Trailing silence detection near expected end of track (500ms before total)

                if (total > TimeSpan.Zero)

                {

                    TimeSpan current = CurrentTime;

                    if (current >= total - TimeSpan.FromMilliseconds(500))

                    {

                        bool isSilent = true;

                        for (int i = offset; i < offset + read - 1; i += 2)

                        {

                            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));

                            if (sample > 30 || sample < -30)

                            {

                                isSilent = false;

                                break;

                            }

                        }

                        if (isSilent)

                        {

                            _consecutiveSilentBytes += read;

                        }

                        else

                        {

                            _consecutiveSilentBytes = 0;

                        }

                        int thresholdBytes = Math.Max(4000, WaveFormat.AverageBytesPerSecond / 2); // 500ms

                        if (_consecutiveSilentBytes >= thresholdBytes)

                        {

                            if (_isLooping)

                            {

                                return LoopRestart(buffer, offset, count);

                            }

                            return 0; // Cut off trailing silence, ending track cleanly

                        }

                    }

                }

                if (read > 0)

                {

                    ApplyVolume(buffer, offset, read);

                }

                return read;

            }

        }

        [StructLayout(LayoutKind.Explicit)]

        private struct FloatUnion

        {

            [FieldOffset(0)] public float FloatValue;

            [FieldOffset(0)] public byte B0;

            [FieldOffset(1)] public byte B1;

            [FieldOffset(2)] public byte B2;

            [FieldOffset(3)] public byte B3;

        }

        private void ApplyVolume(byte[] buffer, int offset, int bytesRead)

        {

            float vol = _volume;

            if (vol >= 0.999f || bytesRead <= 0) return;

            if (vol <= 0.001f)

            {

                Array.Clear(buffer, offset, bytesRead);

                return;

            }

            if (WaveFormat.BitsPerSample == 16)

            {

                int end = offset + bytesRead;

                for (int i = offset; i < end - 1; i += 2)

                {

                    short sample = (short)(buffer[i] | (buffer[i + 1] << 8));

                    int scaled = (int)(sample * vol);

                    if (scaled > 32767) scaled = 32767;

                    else if (scaled < -32768) scaled = -32768;

                    buffer[i] = (byte)(scaled & 0xFF);

                    buffer[i + 1] = (byte)((scaled >> 8) & 0xFF);

                }

            }

            else if (WaveFormat.BitsPerSample == 32)

            {

                FloatUnion u = default(FloatUnion);

                int end = offset + bytesRead;

                for (int i = offset; i < end - 3; i += 4)

                {

                    u.B0 = buffer[i];

                    u.B1 = buffer[i + 1];

                    u.B2 = buffer[i + 2];

                    u.B3 = buffer[i + 3];

                    u.FloatValue *= vol;

                    buffer[i] = u.B0;

                    buffer[i + 1] = u.B1;

                    buffer[i + 2] = u.B2;

                    buffer[i + 3] = u.B3;

                }

            }

        }

        private int LoopRestart(byte[] buffer, int offset, int count)

        {

            _consecutiveSilentBytes = 0;

            _prerollBytesToDiscard = 0;

            _playbackBaseTime = TimeSpan.Zero;

            _bytesReadSinceSeek = 0;

            try

            {

                _source.CurrentTime = TimeSpan.Zero;

            }

            catch

            {

                try { _source.Position = 0; } catch { }

            }

            int loopRead = 0;

            try

            {

                loopRead = _source.Read(buffer, offset, count);

                if (loopRead > 0)

                {

                    _bytesReadSinceSeek += loopRead;

                    ApplyVolume(buffer, offset, loopRead);

                }

            }

            catch { }

            _service.NotifyLoopRestart();

            return loopRead;

        }

        protected override void Dispose(bool disposing)

        {

            if (disposing)

            {

                lock (_syncLock)

                {

                    try { _source.Dispose(); } catch { }

                }

            }

            base.Dispose(disposing);

        }

    }

    public static class WebMClusterParser

    {

        public static List<double> ParseClusters(string filePath)

        {

            var clusterTimes = new List<double>();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return clusterTimes;

            try

            {

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))

                {

                    // Check EBML header: 1A 45 DF A3

                    byte[] header = new byte[4];

                    if (fs.Read(header, 0, 4) < 4) return clusterTimes;

                    if (header[0] != 0x1A || header[1] != 0x45 || header[2] != 0xDF || header[3] != 0xA3)

                    {

                        return clusterTimes; // Not an EBML/WebM container

                    }

                    fs.Position = 0;

                    byte[] buf = new byte[65536];

                    int overlap = 0;

                    int bytesRead;

                    while ((bytesRead = fs.Read(buf, overlap, buf.Length - overlap)) > 0)

                    {

                        int totalInBuf = overlap + bytesRead;

                        for (int i = 0; i <= totalInBuf - 4; i++)

                        {

                            // EBML ID for Cluster: 1F 43 B6 75

                            if (buf[i] == 0x1F && buf[i + 1] == 0x43 && buf[i + 2] == 0xB6 && buf[i + 3] == 0x75)

                            {

                                for (int j = i + 4; j < i + 35 && j + 2 < totalInBuf; j++)

                                {

                                    // Timecode element ID: 0xE7

                                    if (buf[j] == 0xE7)

                                    {

                                        int len = buf[j + 1] & 0x07;

                                        if (len > 0 && len <= 8 && j + 2 + len <= totalInBuf)

                                        {

                                            long timecode = 0;

                                            for (int k = 0; k < len; k++)

                                            {

                                               timecode = (timecode << 8) | buf[j + 2 + k];

                                            }

                                            clusterTimes.Add(timecode / 1000.0);

                                            i = j + 2 + len;

                                            break;

                                        }

                                    }

                                }

                            }

                        }

                        overlap = Math.Min(40, totalInBuf);

                        Buffer.BlockCopy(buf, totalInBuf - overlap, buf, 0, overlap);

                    }

                }

            }

            catch { }

            return clusterTimes;

        }

    }

}

