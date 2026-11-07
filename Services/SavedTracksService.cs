using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using KahoiiMusic.Models;

namespace KahoiiMusic.Services
{
    public class SavedTracksService
    {
        private static readonly Lazy<SavedTracksService> _instance = new Lazy<SavedTracksService>(() => new SavedTracksService());
        public static SavedTracksService Instance => _instance.Value;

        private readonly object _lock = new object();
        private Process _activeDownloadProc = null;
        private readonly object _procLock = new object();

        public void CancelAllDownloads()
        {
            lock (_procLock)
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
        private List<PlaylistItem> _savedTracks = new List<PlaylistItem>();
        private bool _isLoaded = false;
        private int _activeSavingCount = 0;
        public bool IsSavingTrack => _activeSavingCount > 0;

        public event EventHandler SavedTracksChanged;

        private SavedTracksService()
        {
            AppPaths.EnsureDirectoriesExist();
            CleanupLegacyDegradedAudio();
            LoadFromFile();
        }

        private void CleanupLegacyDegradedAudio()
        {
            try
            {
                if (Directory.Exists(AppPaths.SavedAudioDir))
                {
                    foreach (var f in Directory.GetFiles(AppPaths.SavedAudioDir, "*.mp4"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
                if (Directory.Exists(AppPaths.TempAudioDir))
                {
                    foreach (var f in Directory.GetFiles(AppPaths.TempAudioDir, "*.mp4"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }
        }

        public List<PlaylistItem> GetSavedTracks()
        {
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                return new List<PlaylistItem>(_savedTracks);
            }
        }

        
        public void UpdateTitleAndAuthor(string videoId, string newTitle, string newAuthor)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return;
            bool changed = false;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                var match = _savedTracks.Find(x => string.Equals(x.Id, videoId, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    if (!string.IsNullOrWhiteSpace(newTitle) && match.Title != newTitle && newTitle != "None" && newTitle != "NA" && !newTitle.StartsWith("Bài "))
                    {
                        match.Title = newTitle;
                        changed = true;
                    }
                    if (!string.IsNullOrWhiteSpace(newAuthor) && match.Author != newAuthor)
                    {
                        match.Author = newAuthor;
                        changed = true;
                    }
                    if (changed) SaveToFile();
                }
            }
            if (changed) SavedTracksChanged?.Invoke(this, EventArgs.Empty);
        }

        public string GetTrackTitle(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return null;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                var match = _savedTracks.Find(x => string.Equals(x.Id, videoId, StringComparison.OrdinalIgnoreCase));
                if (match != null && !string.IsNullOrWhiteSpace(match.Title) && match.Title != "None" && match.Title != "NA")
                {
                    return match.Title;
                }
            }
            return null;
        }
        public string GetTrackAuthor(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return null;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                var match = _savedTracks.Find(x => string.Equals(x.Id, videoId, StringComparison.OrdinalIgnoreCase));
                if (match != null && !string.IsNullOrWhiteSpace(match.Author))
                {
                    return match.Author;
                }
            }
            return null;
        }


        public bool IsTrackSaved(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return false;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                bool inList = _savedTracks.Exists(t => string.Equals(t.Id, videoId, StringComparison.OrdinalIgnoreCase));
                if (!inList) return false;

                // Verify file still exists on disk
                return FindSavedAudioFile(videoId) != null;
            }
        }

        public bool IsInSavedList(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return false;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                return _savedTracks.Exists(t => string.Equals(t.Id, videoId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void MergeSavedTracks(List<PlaylistItem> cloudTracks)
        {
            if (cloudTracks == null || cloudTracks.Count == 0) return;
            bool changed = false;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                foreach (var track in cloudTracks)
                {
                    if (string.IsNullOrWhiteSpace(track.Id)) continue;
                    if (!_savedTracks.Exists(t => string.Equals(t.Id, track.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        _savedTracks.Add(track);
                        changed = true;
                    }
                }
                if (changed)
                {
                    SaveToFile();
                }
            }
            if (changed)
            {
                SavedTracksChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string FindSavedAudioFile(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return null;
            try
            {
                string legacyMp4 = Path.Combine(AppPaths.SavedAudioDir, videoId + ".mp4");
                if (File.Exists(legacyMp4))
                {
                    try { File.Delete(legacyMp4); } catch { }
                }

                string[] exts = new string[] { ".webm", ".opus", ".m4a", ".mp3", ".aac" };
                foreach (string ext in exts)
                {
                    string path = Path.Combine(AppPaths.SavedAudioDir, videoId + ext);
                    if (File.Exists(path) && new FileInfo(path).Length >= 100000)
                    {
                        return path;
                    }
                }
            }
            catch { }
            return null;
        }

        public string FindSavedThumbFile(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return null;
            try
            {
                string path = Path.Combine(AppPaths.SavedThumbDir, videoId + ".jpg");
                if (File.Exists(path) && new FileInfo(path).Length > 500)
                {
                    return path;
                }
            }
            catch { }
            return null;
        }

        public void SaveTrackAsync(PlaylistItem item, Action<bool, string> callback = null)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                callback?.Invoke(false, "Thông tin bài hát không hợp lệ.");
                return;
            }

            // Kiểm tra trùng: nếu bài hát đã có sẵn trong danh sách và file audio tồn tại
            if (IsTrackSaved(item.Id))
            {
                callback?.Invoke(true, string.Format("Bài hát '{0}' đã có sẵn trong mục Đã tải!", item.Title ?? item.Id));
                return;
            }

            Interlocked.Increment(ref _activeSavingCount);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    AppPaths.EnsureDirectoriesExist();
                    string videoId = item.Id;

                    // 1. Check if audio already exists in KahoiiTemp/audio/
                    string existingTempAudio = null;
                    string[] exts = new string[] { ".webm", ".opus", ".m4a", ".mp3", ".aac" };
                    foreach (string ext in exts)
                    {
                        string p = Path.Combine(AppPaths.TempAudioDir, videoId + ext);
                        if (File.Exists(p) && new FileInfo(p).Length >= 100000)
                        {
                            existingTempAudio = p;
                            break;
                        }
                    }

                    string targetExt = existingTempAudio != null ? Path.GetExtension(existingTempAudio) : ".webm";
                    string destinationAudio = Path.Combine(AppPaths.SavedAudioDir, videoId + targetExt);

                    if (existingTempAudio != null && File.Exists(existingTempAudio))
                    {
                        // Copy from KahoiiTemp directly with sharing to avoid file locks (instant 0ms)
                        bool copied = false;
                        for (int attempt = 0; attempt < 3; attempt++)
                        {
                            try
                            {
                                using (var src = new FileStream(existingTempAudio, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                using (var dst = new FileStream(destinationAudio, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    src.CopyTo(dst);
                                }
                                copied = true;
                                break;
                            }
                            catch
                            {
                                Thread.Sleep(50);
                            }
                        }
                        if (!copied)
                        {
                            try { File.Copy(existingTempAudio, destinationAudio, true); } catch { }
                        }
                    }
                    else
                    {
                        // Download directly to Saved/audio via yt-dlp
                        string ytdlpPath = AudioPlayerService.FindYtDlp();
                        if (string.IsNullOrEmpty(ytdlpPath))
                        {
                            callback?.Invoke(false, "Không tìm thấy công cụ tải yt-dlp.");
                            return;
                        }

                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = ytdlpPath;
                        psi.WorkingDirectory = AppPaths.SavedAudioDir;
                        psi.Arguments = string.Format("-f \"bestaudio[ext=webm]/bestaudio[ext=m4a]/bestaudio/best\" -N 4 --no-playlist --no-progress --no-mtime --no-warnings --no-check-certificates --retries 3 --fragment-retries 3 --socket-timeout 20 --force-ipv4 -o \"{0}\\%(id)s.%(ext)s\" \"https://www.youtube.com/watch?v={1}\"", AppPaths.SavedAudioDir, videoId);
                        psi.CreateNoWindow = true;
                        psi.UseShellExecute = false;

                        Process proc = Process.Start(psi);
                        lock (_procLock) { _activeDownloadProc = proc; }
                        try
                        {
                            proc?.WaitForExit(120000);
                        }
                        finally
                        {
                            lock (_procLock) { if (_activeDownloadProc == proc) _activeDownloadProc = null; }
                            try { proc?.Dispose(); } catch { }
                        }
                    }

                    // 2. Ensure thumbnail is in Saved/thumb/
                    string destThumb = Path.Combine(AppPaths.SavedThumbDir, videoId + ".jpg");
                    string srcTempThumb = Path.Combine(AppPaths.TempThumbDir, videoId + ".jpg");
                    bool thumbSaved = false;

                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            byte[] bytes = wc.DownloadData("https://img.youtube.com/vi/" + videoId + "/hqdefault.jpg");
                            if (bytes != null && bytes.Length > 1000)
                            {
                                File.WriteAllBytes(destThumb, bytes);
                                thumbSaved = true;
                            }
                        }
                    }
                    catch { }

                    if (!thumbSaved && File.Exists(srcTempThumb))
                    {
                        try
                        {
                            File.Copy(srcTempThumb, destThumb, true);
                            thumbSaved = true;
                        }
                        catch { }
                    }

                    if (!thumbSaved)
                    {
                        try
                        {
                            using (WebClient wc = new WebClient())
                            {
                                byte[] bytes = wc.DownloadData("https://img.youtube.com/vi/" + videoId + "/default.jpg");
                                File.WriteAllBytes(destThumb, bytes);
                            }
                        }
                        catch { }
                    }

                    // 3. Verify audio exists in Saved/audio/
                    if (FindSavedAudioFile(videoId) == null)
                    {
                        try { if (File.Exists(destThumb)) File.Delete(destThumb); } catch { }
                        callback?.Invoke(false, "Không thể lưu tệp âm thanh.");
                        return;
                    }

                    // 4. Update saved metadata
                    lock (_lock)
                    {
                        _savedTracks.RemoveAll(t => string.Equals(t.Id, videoId, StringComparison.OrdinalIgnoreCase));
                        _savedTracks.Insert(0, item);
                        SaveToFile();
                    }

                    SavedTracksChanged?.Invoke(this, EventArgs.Empty);
                    callback?.Invoke(true, "Đã lưu bài hát vào mục Đã tải!");
                }
                catch (Exception ex)
                {
                    callback?.Invoke(false, "Lỗi khi lưu bài: " + ex.Message);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeSavingCount);
                    MemoryOptimizer.TrimMemoryAsync(2000, aggressive: true);
                }
            });
        }

        public bool DeleteSavedTrack(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return false;

            lock (_lock)
            {
                int removed = _savedTracks.RemoveAll(t => string.Equals(t.Id, videoId, StringComparison.OrdinalIgnoreCase));
                SaveToFile();

                // Delete audio file in Saved/audio/
                string audioFile = FindSavedAudioFile(videoId);
                if (audioFile != null && File.Exists(audioFile))
                {
                    try { File.Delete(audioFile); } catch { }
                }

                // Delete thumb file in Saved/thumb/
                string thumbFile = Path.Combine(AppPaths.SavedThumbDir, videoId + ".jpg");
                if (File.Exists(thumbFile))
                {
                    try { File.Delete(thumbFile); } catch { }
                }

                SavedTracksChanged?.Invoke(this, EventArgs.Empty);
                return removed > 0;
            }
        }

        public void ClearAllSaved(string exceptVideoId = null)
        {
            lock (_lock)
            {
                try
                {
                    if (Directory.Exists(AppPaths.SavedAudioDir))
                    {
                        foreach (var f in Directory.GetFiles(AppPaths.SavedAudioDir))
                        {
                            if (!string.IsNullOrWhiteSpace(exceptVideoId) && Path.GetFileNameWithoutExtension(f).Equals(exceptVideoId, StringComparison.OrdinalIgnoreCase))
                                continue;
                            try { File.Delete(f); } catch { }
                        }
                    }

                    if (Directory.Exists(AppPaths.SavedThumbDir))
                    {
                        foreach (var f in Directory.GetFiles(AppPaths.SavedThumbDir))
                        {
                            if (!string.IsNullOrWhiteSpace(exceptVideoId) && Path.GetFileNameWithoutExtension(f).Equals(exceptVideoId, StringComparison.OrdinalIgnoreCase))
                                continue;
                            try { File.Delete(f); } catch { }
                        }
                    }
                }
                catch { }

                _savedTracks.Clear();
                SaveToFile();
            }
            SavedTracksChanged?.Invoke(this, EventArgs.Empty);
        }

        private void LoadFromFile()
        {
            lock (_lock)
            {
                _savedTracks.Clear();
                _isLoaded = true;

                if (!File.Exists(AppPaths.SavedJsonFile)) return;

                var missingAudio = new List<PlaylistItem>();

                try
                {
                    string json = File.ReadAllText(AppPaths.SavedJsonFile, Encoding.UTF8);
                    var objMatches = Regex.Matches(json, @"\{([^{}]+)\}");
                    foreach (Match om in objMatches)
                    {
                        string block = om.Groups[1].Value;
                        var mId = Regex.Match(block, @"""id""\s*:\s*""([^""]*)""");
                        if (!mId.Success || string.IsNullOrEmpty(mId.Groups[1].Value)) continue;

                        var mTitle = Regex.Match(block, @"""title""\s*:\s*""((?:\\.|[^""])*)""");
                        var mDur = Regex.Match(block, @"""duration""\s*:\s*(\d+)");
                        var mAuthor = Regex.Match(block, @"""(?:author|artist)""\s*:\s*""((?:\\.|[^""])*)""");

                        string id = mId.Groups[1].Value;
                        string title = mTitle.Success ? mTitle.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\") : "Bài hát";
                        int duration = 0;
                        if (mDur.Success) int.TryParse(mDur.Groups[1].Value, out duration);
                        string author = mAuthor.Success ? mAuthor.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\") : null;
                        if (string.IsNullOrWhiteSpace(author))
                        {
                            author = AudioPlayerService.ExtractAuthorFromTitle(title);
                        }

                        var item = new PlaylistItem
                        {
                            Id = id,
                            Title = title,
                            Author = author ?? string.Empty,
                            DurationSeconds = duration
                        };
                        _savedTracks.Add(item);

                        // If high-quality audio file doesn't exist yet, queue background download
                        if (FindSavedAudioFile(id) == null)
                        {
                            missingAudio.Add(item);
                        }
                    }
                }
                catch { }

                if (missingAudio.Count > 0)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        foreach (var track in missingAudio)
                        {
                            SaveTrackAsync(track);
                        }
                    });
                }
            }
        }

        private void SaveToFile()
        {
            try
            {
                AppPaths.EnsureDirectoriesExist();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < _savedTracks.Count; i++)
                {
                    PlaylistItem item = _savedTracks[i];
                    string escapedTitle = (item.Title ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
                    string escapedAuthor = (item.Author ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
                    string comma = (i < _savedTracks.Count - 1) ? "," : "";
                    sb.AppendLine(string.Format("  {{ \"id\": \"{0}\", \"title\": \"{1}\", \"duration\": {2}, \"author\": \"{3}\" }}{4}",
                        item.Id, escapedTitle, item.DurationSeconds, escapedAuthor, comma));
                }
                sb.AppendLine("]");
                File.WriteAllText(AppPaths.SavedJsonFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
