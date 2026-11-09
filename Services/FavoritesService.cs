using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using KahoiiMusic.Models;

namespace KahoiiMusic.Services
{
    public static class FavoritesService
    {
        private static readonly object _lock = new object();
        private static readonly string FavoritesFile;
        private static readonly List<PlaylistItem> _favorites = new List<PlaylistItem>();
        private static bool _initialized = false;

        public static event EventHandler FavoritesChanged;

        static FavoritesService()
        {
            try
            {
                AppPaths.EnsureDirectoriesExist();
                FavoritesFile = AppPaths.FavoritesJsonFile;

                // Check for migration from legacy %AppData%
                string legacyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KahoiiMusic");
                string legacyFavorites = Path.Combine(legacyDir, "favorites.json");
                if (!File.Exists(FavoritesFile) && File.Exists(legacyFavorites))
                {
                    try { File.Copy(legacyFavorites, FavoritesFile, true); } catch { }
                }
            }
            catch
            {
                FavoritesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorites.json");
            }

            Init();
        }

        private static void Init()
        {
            if (_initialized) return;
            lock (_lock)
            {
                _initialized = true;
                LoadFavorites();
            }
        }

        public static int Count
        {
            get
            {
                lock (_lock) { return _favorites.Count; }
            }
        }

        public static List<PlaylistItem> GetFavorites()
        {
            lock (_lock)
            {
                return new List<PlaylistItem>(_favorites);
            }
        }

        public static bool IsFavorite(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            lock (_lock)
            {
                return _favorites.Exists(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static bool ToggleFavorite(PlaylistItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return false;

            bool isNowFavorite = false;
            lock (_lock)
            {
                int index = _favorites.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    _favorites.RemoveAt(index);
                    isNowFavorite = false;
                }
                else
                {
                    string safeTitle = (!string.IsNullOrWhiteSpace(item.Title) && item.Title != "None" && item.Title != "NA") ? item.Title : "Bài hát";
                    string safeAuthor = !string.IsNullOrWhiteSpace(item.Author) ? item.Author : AudioPlayerService.ExtractAuthorFromTitle(safeTitle);

                    _favorites.Insert(0, new PlaylistItem
                    {
                        Id = item.Id,
                        Title = safeTitle,
                        Author = safeAuthor ?? string.Empty,
                        DurationSeconds = item.DurationSeconds
                    });
                    isNowFavorite = true;
                }

                SaveFavorites();
            }

            FavoritesChanged?.Invoke(null, EventArgs.Empty);
            return isNowFavorite;
        }

        public static void AddTrack(PlaylistItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;

            bool changed = false;
            lock (_lock)
            {
                int existingIdx = _favorites.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                if (existingIdx >= 0)
                {
                    var existing = _favorites[existingIdx];
                    if (!string.IsNullOrWhiteSpace(item.Title) && item.Title != "None" && item.Title != "NA")
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
                    _favorites.RemoveAt(existingIdx);
                    _favorites.Insert(0, existing);
                    changed = true;
                }
                else
                {
                    string safeTitle = (!string.IsNullOrWhiteSpace(item.Title) && item.Title != "None" && item.Title != "NA") ? item.Title : "Bài hát";
                    string safeAuthor = !string.IsNullOrWhiteSpace(item.Author) ? item.Author : AudioPlayerService.ExtractAuthorFromTitle(safeTitle);

                    _favorites.Insert(0, new PlaylistItem
                    {
                        Id = item.Id,
                        Title = safeTitle,
                        Author = safeAuthor ?? string.Empty,
                        DurationSeconds = item.DurationSeconds
                    });
                    changed = true;
                }

                if (changed)
                {
                    SaveFavorites();
                }
            }

            if (changed)
            {
                FavoritesChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void RemoveFavorite(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            bool removed = false;
            lock (_lock)
            {
                int countBefore = _favorites.Count;
                _favorites.RemoveAll(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                removed = (_favorites.Count != countBefore);
                if (removed)
                {
                    SaveFavorites();
                }
            }

            if (removed)
            {
                FavoritesChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void ClearFavorites()
        {
            lock (_lock)
            {
                _favorites.Clear();
                SaveFavorites();
            }

            FavoritesChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void MergeFavorites(List<PlaylistItem> cloudFavorites)
        {
            if (cloudFavorites == null || cloudFavorites.Count == 0) return;

            bool changed = false;
            lock (_lock)
            {
                foreach (var track in cloudFavorites)
                {
                    if (track == null || string.IsNullOrWhiteSpace(track.Id)) continue;
                    if (!_favorites.Exists(x => string.Equals(x.Id, track.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        string safeTitle = (!string.IsNullOrWhiteSpace(track.Title) && track.Title != "None" && track.Title != "NA") ? track.Title : "Bài hát";
                        string safeAuthor = !string.IsNullOrWhiteSpace(track.Author) ? track.Author : AudioPlayerService.ExtractAuthorFromTitle(safeTitle);

                        _favorites.Add(new PlaylistItem
                        {
                            Id = track.Id,
                            Title = safeTitle,
                            Author = safeAuthor ?? string.Empty,
                            DurationSeconds = track.DurationSeconds
                        });
                        changed = true;
                    }
                }

                if (changed)
                {
                    SaveFavorites();
                }
            }

            if (changed)
            {
                FavoritesChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void ReloadFromFile()
        {
            lock (_lock)
            {
                LoadFavorites();
            }
            FavoritesChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void LoadFavorites()
        {
            try
            {
                _favorites.Clear();
                if (File.Exists(FavoritesFile))
                {
                    string json = File.ReadAllText(FavoritesFile, Encoding.UTF8);
                    var objMatches = Regex.Matches(json, @"\{([^{}]+)\}");
                    foreach (Match om in objMatches)
                    {
                        string block = om.Groups[1].Value;
                        var mId = Regex.Match(block, @"""id""\s*:\s*""([^""]*)""");
                        if (!mId.Success || string.IsNullOrEmpty(mId.Groups[1].Value)) continue;

                        var mTitle = Regex.Match(block, @"""title""\s*:\s*""((?:\\.|[^""])*)""");
                        var mDur = Regex.Match(block, @"""duration(?:Seconds)?""\s*:\s*(\d+)");
                        var mAuthor = Regex.Match(block, @"""(?:author|artist)""\s*:\s*""((?:\\.|[^""])*)""");

                        string id = mId.Groups[1].Value;
                        string title = mTitle.Success ? Regex.Unescape(mTitle.Groups[1].Value) : "Bài hát";
                        int duration = 0;
                        if (mDur.Success) int.TryParse(mDur.Groups[1].Value, out duration);
                        string author = mAuthor.Success ? Regex.Unescape(mAuthor.Groups[1].Value) : null;
                        if (string.IsNullOrWhiteSpace(author))
                        {
                            author = AudioPlayerService.ExtractAuthorFromTitle(title);
                        }

                        _favorites.Add(new PlaylistItem
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

        private static void SaveFavorites()
        {
            try
            {
                AppPaths.EnsureDirectoriesExist();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < _favorites.Count; i++)
                {
                    PlaylistItem item = _favorites[i];
                    string escId = EscapeJson(item.Id);
                    string escTitle = EscapeJson(item.Title ?? "");
                    string escAuthor = EscapeJson(item.Author ?? "");
                    sb.AppendFormat("  {{\"id\": \"{0}\", \"title\": \"{1}\", \"duration\": {2}, \"author\": \"{3}\"}}",
                        escId, escTitle, item.DurationSeconds, escAuthor);
                    if (i < _favorites.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("]");
                File.WriteAllText(FavoritesFile, sb.ToString(), Encoding.UTF8);
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
