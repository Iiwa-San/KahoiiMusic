using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using KahoiiMusic.Models;

namespace KahoiiMusic.Services
{
    public enum ArtworkDisplayMode
    {
        VinylRecord = 0,
        RoundedCard = 1
    }

    public static class HistoryService
    {
        private static readonly object _lock = new object();
        private static readonly string DataFolder;
        private static readonly string HistoryFile;
        private static readonly string SettingsFile;
        private static readonly string DeletedHistoryFile;

        private static List<PlaylistItem> _history = new List<PlaylistItem>();
        private static HashSet<string> _deletedHistoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _autoCleanTempOnExit = false;
        private static int _lyricsDelayOffsetMs = 0; // Default: 0ms true audio synchronization
        private static int _lyricsRomanizationMode = 1; // Default: Dual mode (Song ngữ)
        private static int _artworkDisplayMode = 0; // 0 = VinylRecord, 1 = RoundedCard
        private static bool _initialized = false;

        public static int LyricsDelayOffsetMs
        {
            get { lock (_lock) { return _lyricsDelayOffsetMs; } }
            set
            {
                lock (_lock)
                {
                    if (_lyricsDelayOffsetMs != value)
                    {
                        _lyricsDelayOffsetMs = value;
                        SaveSettings();
                    }
                }
                SettingsChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static int LyricsRomanizationMode
        {
            get { lock (_lock) { return _lyricsRomanizationMode; } }
            set
            {
                lock (_lock)
                {
                    if (_lyricsRomanizationMode != value)
                    {
                        _lyricsRomanizationMode = value;
                        SaveSettings();
                    }
                }
                SettingsChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static ArtworkDisplayMode ArtworkMode
        {
            get { lock (_lock) { return (ArtworkDisplayMode)_artworkDisplayMode; } }
            set
            {
                lock (_lock)
                {
                    int val = (int)value;
                    if (_artworkDisplayMode != val)
                    {
                        _artworkDisplayMode = val;
                        SaveSettings();
                    }
                }
                SettingsChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static event EventHandler HistoryChanged;
        public static event EventHandler SettingsChanged;

        static HistoryService()
        {
            try
            {
                // Self-contained in Saved/ folder next to executable (portable, zero C-drive pollution)
                DataFolder = AppPaths.SavedDir;
                if (!Directory.Exists(DataFolder))
                {
                    Directory.CreateDirectory(DataFolder);
                }

                // Check for migration from legacy %AppData%
                string legacyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KahoiiMusic");
                string legacyHistory = Path.Combine(legacyDir, "history.json");
                string legacySettings = Path.Combine(legacyDir, "settings.json");

                string targetHistory = Path.Combine(DataFolder, "history.json");
                string targetSettings = Path.Combine(DataFolder, "settings.json");

                if (!File.Exists(targetHistory) && File.Exists(legacyHistory))
                {
                    try { File.Copy(legacyHistory, targetHistory, true); } catch { }
                }
                if (!File.Exists(targetSettings) && File.Exists(legacySettings))
                {
                    try { File.Copy(legacySettings, targetSettings, true); } catch { }
                }
            }
            catch
            {
                DataFolder = AppDomain.CurrentDomain.BaseDirectory;
            }

            HistoryFile = Path.Combine(DataFolder, "history.json");
            SettingsFile = Path.Combine(DataFolder, "settings.json");
            DeletedHistoryFile = Path.Combine(DataFolder, "deleted_history.json");

            FirebaseAuthService.Instance.AuthStateChanged += (s, e) =>
            {
                lock (_lock)
                {
                    if (FirebaseAuthService.Instance.IsLoggedIn)
                    {
                        LoadDeletedHistory();
                        LoadHistory();
                    }
                    else
                    {
                        _history.Clear();
                        _deletedHistoryIds.Clear();
                    }
                }
                HistoryChanged?.Invoke(null, EventArgs.Empty);
            };

            Init();
        }

        private static void Init()
        {
            if (_initialized) return;
            lock (_lock)
            {
                _initialized = true;
                LoadSettings();
                LoadDeletedHistory();
                if (FirebaseAuthService.Instance.IsLoggedIn)
                {
                    LoadHistory();
                }
                else
                {
                    _history.Clear();
                }
            }
        }

        public static List<string> GetDeletedHistoryIds()
        {
            lock (_lock)
            {
                return new List<string>(_deletedHistoryIds);
            }
        }

        public static bool AutoCleanTempOnExit
        {
            get
            {
                lock (_lock) { return _autoCleanTempOnExit; }
            }
            set
            {
                lock (_lock)
                {
                    if (_autoCleanTempOnExit != value)
                    {
                        _autoCleanTempOnExit = value;
                        SaveSettings();
                    }
                }
                SettingsChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static List<PlaylistItem> GetHistory()
        {
            if (!FirebaseAuthService.Instance.IsLoggedIn)
            {
                return new List<PlaylistItem>();
            }
            lock (_lock)
            {
                return new List<PlaylistItem>(_history);
            }
        }

        public static string GetTrackTitle(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            lock (_lock)
            {
                var match = _history.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                if (match != null && !string.IsNullOrWhiteSpace(match.Title) && match.Title != "None" && match.Title != "NA" && !match.Title.StartsWith("Bài "))
                {
                    return match.Title;
                }
            }
            return null;
        }
        public static string GetTrackAuthor(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            lock (_lock)
            {
                var match = _history.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                if (match != null && !string.IsNullOrWhiteSpace(match.Author))
                {
                    return match.Author;
                }
            }
            return null;
        }

        public static int GetTrackDuration(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return 0;
            lock (_lock)
            {
                var match = _history.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                return match != null ? match.DurationSeconds : 0;
            }
        }


        public static void AddTrack(PlaylistItem item)
        {
            if (!FirebaseAuthService.Instance.IsLoggedIn) return;
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;

            bool changed = false;
            lock (_lock)
            {
                if (_deletedHistoryIds.Contains(item.Id))
                {
                    _deletedHistoryIds.Remove(item.Id);
                    SaveDeletedHistory();
                }

                int existingIdx = _history.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                if (existingIdx >= 0)
                {
                    PlaylistItem existing = _history[existingIdx];
                    if (!string.IsNullOrWhiteSpace(item.Title) && item.Title != "None" && item.Title != "NA" && !item.Title.StartsWith("Bài "))
                    {
                        existing.Title = item.Title;
                    }
                    if (!string.IsNullOrWhiteSpace(item.Author))
                    {
                        existing.Author = item.Author;
                    }
                    if (item.DurationSeconds > 0)
                    {
                        existing.DurationSeconds = item.DurationSeconds;
                    }
                    // Move to top
                    _history.RemoveAt(existingIdx);
                    _history.Insert(0, existing);
                    changed = true;
                }
                else
                {
                    _history.Insert(0, new PlaylistItem
                    {
                        Id = item.Id,
                        Title = item.Title,
                        DurationSeconds = item.DurationSeconds
                    });
                    changed = true;
                }

                if (_history.Count > 500)
                {
                    _history.RemoveRange(500, _history.Count - 500);
                }

                if (changed)
                {
                    SaveHistory();
                }
            }

            if (changed)
            {
                HistoryChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void UpdateTrackInfoOnly(PlaylistItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;
            bool changed = false;
            lock (_lock)
            {
                var match = _history.Find(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    if (!string.IsNullOrWhiteSpace(item.Title) && item.Title != "None" && item.Title != "NA" && !item.Title.StartsWith("Bài ") && match.Title != item.Title)
                    {
                        match.Title = item.Title;
                        changed = true;
                    }
                    if (item.DurationSeconds > 0 && match.DurationSeconds != item.DurationSeconds)
                    {
                        match.DurationSeconds = item.DurationSeconds;
                        changed = true;
                    }
                    if (changed)
                    {
                        SaveHistory();
                    }
                }
            }
            if (changed)
            {
                HistoryChanged?.Invoke(null, EventArgs.Empty);
            }
        }

                public static void UpdateTitleAndAuthor(string id, string newTitle, string newAuthor)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            bool changed = false;
            lock (_lock)
            {
                var match = _history.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
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
                    if (changed)
                    {
                        SaveHistory();
                    }
                }
            }

            if (changed)
            {
                HistoryChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void UpdateTitle(string id, string newTitle)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newTitle)) return;
            if (newTitle == "None" || newTitle == "NA" || newTitle.StartsWith("Bài ")) return;

            bool changed = false;
            lock (_lock)
            {
                var match = _history.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                if (match != null && match.Title != newTitle)
                {
                    match.Title = newTitle;
                    changed = true;
                    SaveHistory();
                }
            }

            if (changed)
            {
                HistoryChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static bool ContainsTrack(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            lock (_lock)
            {
                return _history.Exists(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static HashSet<string> GetHistoryIdSet()
        {
            lock (_lock)
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in _history)
                {
                    if (!string.IsNullOrWhiteSpace(item.Id))
                    {
                        set.Add(item.Id);
                    }
                }
                return set;
            }
        }

        public static void RemoveTrack(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            bool changed = false;
            lock (_lock)
            {
                if (!_deletedHistoryIds.Contains(id))
                {
                    _deletedHistoryIds.Add(id);
                    TrimDeletedHistoryIds();
                    SaveDeletedHistory();
                }

                int idx = _history.FindIndex(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    _history.RemoveAt(idx);
                    changed = true;
                    SaveHistory();
                }
            }

            if (changed)
            {
                DeleteCachedAudioForTrack(id);
                HistoryChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void ClearHistory(string exceptVideoId = null)
        {
            lock (_lock)
            {
                foreach (var item in _history)
                {
                    if (item != null && !string.IsNullOrWhiteSpace(item.Id) && (exceptVideoId == null || !string.Equals(item.Id, exceptVideoId, StringComparison.OrdinalIgnoreCase)))
                    {
                        _deletedHistoryIds.Add(item.Id);
                    }
                }
                TrimDeletedHistoryIds();
                SaveDeletedHistory();

                if (string.IsNullOrWhiteSpace(exceptVideoId))
                {
                    _history.Clear();
                }
                else
                {
                    _history.RemoveAll(x => !string.Equals(x.Id, exceptVideoId, StringComparison.OrdinalIgnoreCase));
                }
                SaveHistory();
            }
            CleanNonHistoryTempFiles(true);
            HistoryChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void MergeHistory(List<PlaylistItem> cloudHistory, List<string> cloudDeletedIds = null)
        {
            if (!FirebaseAuthService.Instance.IsLoggedIn) return;

            bool changed = false;
            lock (_lock)
            {
                if (cloudDeletedIds != null && cloudDeletedIds.Count > 0)
                {
                    foreach (var id in cloudDeletedIds)
                    {
                        if (!string.IsNullOrWhiteSpace(id) && _deletedHistoryIds.Add(id))
                        {
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        TrimDeletedHistoryIds();
                        SaveDeletedHistory();
                    }
                }

                // Purge any local history tracks that are marked as deleted
                if (_deletedHistoryIds.Count > 0)
                {
                    int removed = _history.RemoveAll(x => _deletedHistoryIds.Contains(x.Id));
                    if (removed > 0)
                    {
                        changed = true;
                    }
                }

                // Merge incoming cloud tracks (skip if deleted or already present)
                if (cloudHistory != null && cloudHistory.Count > 0)
                {
                    foreach (var track in cloudHistory)
                    {
                        if (track == null || string.IsNullOrWhiteSpace(track.Id)) continue;
                        if (_deletedHistoryIds.Contains(track.Id)) continue;

                        if (!_history.Exists(x => string.Equals(x.Id, track.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            _history.Add(track);
                            changed = true;
                        }
                    }
                }

                if (_history.Count > 500)
                {
                    _history.RemoveRange(500, _history.Count - 500);
                    changed = true;
                }

                if (changed)
                {
                    SaveHistory();
                }
            }

            if (changed)
            {
                HistoryChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void DeleteCachedAudioForTrack(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            try
            {
                ThumbnailManager.DeleteThumb(id);

                if (AudioPlayerService.Instance != null && AudioPlayerService.Instance.GetProtectedTrackIdSet().Contains(id)) return;
                if (SavedTracksService.Instance.IsTrackSaved(id)) return;

                string[] possiblePaths = new string[]
                {
                    AppPaths.TempAudioDir,
                    AppPaths.TempThumbDir
                };
                foreach (string dir in possiblePaths)
                {
                    if (Directory.Exists(dir))
                    {
                        foreach (string file in Directory.GetFiles(dir))
                        {
                            string name = Path.GetFileNameWithoutExtension(file);
                            if (string.Equals(name, id, StringComparison.OrdinalIgnoreCase))
                            {
                                for (int r = 0; r < 3; r++)
                                {
                                    try { File.Delete(file); break; } catch { System.Threading.Thread.Sleep(20); }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public static void CleanNonHistoryTempFiles(bool forceCleanAll = false)
        {
            try
            {
                HashSet<string> historyIds = GetHistoryIdSet();
                bool cleanAll = forceCleanAll || AutoCleanTempOnExit;

                ThumbnailManager.CleanNonHistoryDiskThumbs(historyIds, cleanAll);

                // Audio files are NOT protected by history. Clean non-playlist, non-saved temp audio files:
                if (AudioPlayerService.Instance != null)
                {
                    AudioPlayerService.Instance.CleanUnusedTempAudio();
                }

                if (cleanAll)
                {
                    try
                    {
                        if (Directory.Exists(AppPaths.TempAudioDir))
                        {
                            foreach (string file in Directory.GetFiles(AppPaths.TempAudioDir))
                            {
                                string fileName = Path.GetFileName(file);
                                if (string.Equals(fileName, "yt-dlp.exe", StringComparison.OrdinalIgnoreCase)) continue;
                                for (int r = 0; r < 3; r++)
                                {
                                    try { File.Delete(file); break; } catch { System.Threading.Thread.Sleep(20); }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string text = File.ReadAllText(SettingsFile, Encoding.UTF8);
                    Match m = Regex.Match(text, "\"autoCleanTempOnExit\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        _autoCleanTempOnExit = bool.Parse(m.Groups[1].Value);
                    }

                    Match mDelay = Regex.Match(text, "\"lyricsDelayOffsetMs\"\\s*:\\s*(-?\\d+)", RegexOptions.IgnoreCase);
                    if (mDelay.Success)
                    {
                        int.TryParse(mDelay.Groups[1].Value, out _lyricsDelayOffsetMs);
                    }

                    Match mRom = Regex.Match(text, "\"lyricsRomanizationMode\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
                    if (mRom.Success)
                    {
                        int.TryParse(mRom.Groups[1].Value, out _lyricsRomanizationMode);
                    }

                    Match mArt = Regex.Match(text, "\"artworkDisplayMode\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
                    if (mArt.Success)
                    {
                        int.TryParse(mArt.Groups[1].Value, out _artworkDisplayMode);
                    }
                    return;
                }
            }
            catch { }
            _autoCleanTempOnExit = true; // Default: ON (clean unused temp on exit)
            _lyricsDelayOffsetMs = 0;
            _lyricsRomanizationMode = 1;
            _artworkDisplayMode = 0;
        }

        private static void SaveSettings()
        {
            try
            {
                string json = string.Format("{{\r\n  \"autoCleanTempOnExit\": {0},\r\n  \"lyricsDelayOffsetMs\": {1},\r\n  \"lyricsRomanizationMode\": {2},\r\n  \"artworkDisplayMode\": {3}\r\n}}",
                    _autoCleanTempOnExit ? "true" : "false",
                    _lyricsDelayOffsetMs,
                    _lyricsRomanizationMode,
                    _artworkDisplayMode);
                File.WriteAllText(SettingsFile, json, Encoding.UTF8);
            }
            catch { }
        }

        private static void LoadHistory()
        {
            try
            {
                _history.Clear();
                if (File.Exists(HistoryFile))
                {
                    string json = File.ReadAllText(HistoryFile, Encoding.UTF8);
                    var objMatches = Regex.Matches(json, @"\{([^{}]+)\}");
                    foreach (Match om in objMatches)
                    {
                        string block = om.Groups[1].Value;
                        var mId = Regex.Match(block, @"""id""\s*:\s*""([^""]*)""");
                        if (!mId.Success || string.IsNullOrEmpty(mId.Groups[1].Value)) continue;

                        string id = mId.Groups[1].Value;
                        if (_deletedHistoryIds.Contains(id)) continue;

                        var mTitle = Regex.Match(block, @"""title""\s*:\s*""((?:\\.|[^""])*)""");
                        var mDur = Regex.Match(block, @"""duration""\s*:\s*(\d+)");
                        var mAuthor = Regex.Match(block, @"""(?:author|artist)""\s*:\s*""((?:\\.|[^""])*)""");

                        string title = mTitle.Success ? Regex.Unescape(mTitle.Groups[1].Value) : "Bài hát";
                        int duration = 0;
                        if (mDur.Success) int.TryParse(mDur.Groups[1].Value, out duration);
                        string author = mAuthor.Success ? Regex.Unescape(mAuthor.Groups[1].Value) : null;
                        if (string.IsNullOrWhiteSpace(author))
                        {
                            author = AudioPlayerService.ExtractAuthorFromTitle(title);
                        }

                        _history.Add(new PlaylistItem
                        {
                            Id = id,
                            Title = title,
                            Author = author ?? string.Empty,
                            DurationSeconds = duration
                        });
                    }
                }
            }
            catch { }
        }

        private static void LoadDeletedHistory()
        {
            try
            {
                _deletedHistoryIds.Clear();
                if (File.Exists(DeletedHistoryFile))
                {
                    string json = File.ReadAllText(DeletedHistoryFile, Encoding.UTF8);
                    var matches = Regex.Matches(json, @"""([^""]+)""");
                    foreach (Match m in matches)
                    {
                        string val = m.Groups[1].Value;
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            _deletedHistoryIds.Add(val);
                        }
                    }
                }
            }
            catch { }
        }

        private static void SaveDeletedHistory()
        {
            if (!FirebaseAuthService.Instance.IsLoggedIn) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[");
                int count = 0;
                foreach (var id in _deletedHistoryIds)
                {
                    if (count > 0) sb.AppendLine(",");
                    sb.AppendFormat("  \"{0}\"", EscapeJson(id));
                    count++;
                }
                sb.AppendLine();
                sb.AppendLine("]");
                File.WriteAllText(DeletedHistoryFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static void TrimDeletedHistoryIds()
        {
            if (_deletedHistoryIds.Count > 500)
            {
                var list = new List<string>(_deletedHistoryIds);
                _deletedHistoryIds = new HashSet<string>(list.GetRange(list.Count - 500, 500), StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void SaveHistory()
        {
            if (!FirebaseAuthService.Instance.IsLoggedIn) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < _history.Count; i++)
                {
                    PlaylistItem item = _history[i];
                    string escId = EscapeJson(item.Id);
                    string escTitle = EscapeJson(item.Title ?? "");
                    string escAuthor = EscapeJson(item.Author ?? "");
                    sb.AppendFormat("  {{\"id\": \"{0}\", \"title\": \"{1}\", \"duration\": {2}, \"author\": \"{3}\"}}", escId, escTitle, item.DurationSeconds, escAuthor);
                    if (i < _history.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("]");
                File.WriteAllText(HistoryFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}

