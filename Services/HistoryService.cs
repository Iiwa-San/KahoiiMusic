using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using KahoiiMusic.Models;

namespace KahoiiMusic.Services
{
    public static class HistoryService
    {
        private static readonly object _lock = new object();
        private static readonly string DataFolder;
        private static readonly string HistoryFile;
        private static readonly string SettingsFile;

        private static List<PlaylistItem> _history = new List<PlaylistItem>();
        private static bool _autoCleanTempOnExit = false;
        private static bool _initialized = false;

        public static event EventHandler HistoryChanged;
        public static event EventHandler SettingsChanged;

        static HistoryService()
        {
            try
            {
                DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KahoiiMusic");
                if (!Directory.Exists(DataFolder))
                {
                    Directory.CreateDirectory(DataFolder);
                }
            }
            catch
            {
                DataFolder = AppDomain.CurrentDomain.BaseDirectory;
            }

            HistoryFile = Path.Combine(DataFolder, "history.json");
            SettingsFile = Path.Combine(DataFolder, "settings.json");

            Init();
        }

        private static void Init()
        {
            if (_initialized) return;
            lock (_lock)
            {
                _initialized = true;
                LoadSettings();
                LoadHistory();
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

        public static void AddTrack(PlaylistItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;

            bool changed = false;
            lock (_lock)
            {
                int existingIdx = _history.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                if (existingIdx >= 0)
                {
                    PlaylistItem existing = _history[existingIdx];
                    if (!string.IsNullOrWhiteSpace(item.Title) && item.Title != "None" && item.Title != "NA" && !item.Title.StartsWith("Bài "))
                    {
                        existing.Title = item.Title;
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

        public static void ClearHistory()
        {
            lock (_lock)
            {
                _history.Clear();
                SaveHistory();
            }
            CleanNonHistoryTempFiles(true);
            HistoryChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void DeleteCachedAudioForTrack(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            try
            {
                ThumbnailManager.DeleteThumb(id);

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

                string[] possiblePaths = new string[]
                {
                    AppPaths.TempAudioDir,
                    AppPaths.TempThumbDir
                };

                foreach (string dir in possiblePaths)
                {
                    if (!Directory.Exists(dir)) continue;

                    foreach (string file in Directory.GetFiles(dir))
                    {
                        string fileName = Path.GetFileName(file);
                        if (string.Equals(fileName, "yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string fileId = Path.GetFileNameWithoutExtension(file);

                        // If cleanAll is true OR file is NOT in history -> DELETE
                        if (cleanAll || !historyIds.Contains(fileId))
                        {
                            for (int retry = 0; retry < 3; retry++)
                            {
                                try
                                {
                                    File.Delete(file);
                                    break;
                                }
                                catch
                                {
                                    System.Threading.Thread.Sleep(25);
                                }
                            }
                        }
                    }
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
                        return;
                    }
                }
            }
            catch { }
            _autoCleanTempOnExit = false; // Default: OFF (preserve temp)
        }

        private static void SaveSettings()
        {
            try
            {
                string json = string.Format("{{\r\n  \"autoCleanTempOnExit\": {0}\r\n}}", _autoCleanTempOnExit ? "true" : "false");
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
                    MatchCollection matches = Regex.Matches(json, "\\{\\s*\"id\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*\"title\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"\\s*,\\s*\"duration\"\\s*:\\s*(\\d+)\\s*\\}");
                    foreach (Match m in matches)
                    {
                        if (m.Success && m.Groups.Count >= 4)
                        {
                            string id = m.Groups[1].Value;
                            string rawTitle = m.Groups[2].Value;
                            string title = Regex.Unescape(rawTitle);
                            int duration = 0;
                            int.TryParse(m.Groups[3].Value, out duration);

                            if (!string.IsNullOrEmpty(id))
                            {
                                _history.Add(new PlaylistItem
                                {
                                    Id = id,
                                    Title = title,
                                    DurationSeconds = duration
                                });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void SaveHistory()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < _history.Count; i++)
                {
                    PlaylistItem item = _history[i];
                    string escId = EscapeJson(item.Id);
                    string escTitle = EscapeJson(item.Title ?? "");
                    sb.AppendFormat("  {{\"id\": \"{0}\", \"title\": \"{1}\", \"duration\": {2}}}", escId, escTitle, item.DurationSeconds);
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

