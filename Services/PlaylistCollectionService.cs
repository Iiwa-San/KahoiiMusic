using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using KahoiiMusic.Models;

namespace KahoiiMusic.Services
{
    public class PlaylistCollectionService
    {
        private static readonly Lazy<PlaylistCollectionService> _instance =
            new Lazy<PlaylistCollectionService>(() => new PlaylistCollectionService());

        public static PlaylistCollectionService Instance => _instance.Value;

        private readonly object _lock = new object();
        private List<UserPlaylist> _playlists = new List<UserPlaylist>();
        private bool _isLoaded = false;

        public event EventHandler PlaylistsChanged;

        private PlaylistCollectionService()
        {
            AppPaths.EnsureDirectoriesExist();
            LoadFromFile();
        }

        public List<UserPlaylist> GetPlaylists()
        {
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                return new List<UserPlaylist>(_playlists);
            }
        }

        public UserPlaylist GetPlaylist(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                return _playlists.Find(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            }
        }

        public UserPlaylist CreatePlaylist(string name)
        {
            return SavePlaylist(name, new List<PlaylistItem>());
        }

        public UserPlaylist SavePlaylist(string name, List<PlaylistItem> items)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Playlist " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            }

            var playlist = new UserPlaylist(name, items);
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                _playlists.Insert(0, playlist);
                SaveToFile();
            }

            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return playlist;
        }

        public void ClearAllPlaylists()
        {
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                _playlists.Clear();
                SaveToFile();
            }
            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool DeletePlaylist(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                int removed = _playlists.RemoveAll(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    SaveToFile();
                    PlaylistsChanged?.Invoke(this, EventArgs.Empty);
                    return true;
                }
            }
            return false;
        }

        public bool RenamePlaylist(string id, string newName)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newName)) return false;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                var match = _playlists.Find(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    match.Name = newName.Trim();
                    SaveToFile();
                    PlaylistsChanged?.Invoke(this, EventArgs.Empty);
                    return true;
                }
            }
            return false;
        }

        public bool AddTrackToPlaylist(string playlistId, PlaylistItem item)
        {
            if (string.IsNullOrWhiteSpace(playlistId) || item == null || string.IsNullOrWhiteSpace(item.Id)) return false;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                var match = _playlists.Find(p => string.Equals(p.Id, playlistId, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    if (match.Items == null) match.Items = new List<PlaylistItem>();
                    // Only add if not already in playlist
                    if (!match.Items.Exists(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        match.Items.Add(item);
                        SaveToFile();
                        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
                        return true;
                    }
                }
            }
            return false;
        }

        public bool RemoveTrackFromPlaylist(string playlistId, string videoId)
        {
            if (string.IsNullOrWhiteSpace(playlistId) || string.IsNullOrWhiteSpace(videoId)) return false;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                var match = _playlists.Find(p => string.Equals(p.Id, playlistId, StringComparison.OrdinalIgnoreCase));
                if (match != null && match.Items != null)
                {
                    int removed = match.Items.RemoveAll(x => string.Equals(x.Id, videoId, StringComparison.OrdinalIgnoreCase));
                    if (removed > 0)
                    {
                        SaveToFile();
                        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
                        return true;
                    }
                }
            }
            return false;
        }

        private void LoadFromFile()
        {
            lock (_lock)
            {
                _playlists.Clear();
                _isLoaded = true;

                if (!File.Exists(AppPaths.PlaylistsJsonFile)) return;

                try
                {
                    string json = File.ReadAllText(AppPaths.PlaylistsJsonFile, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(json)) return;

                    int i = 0;
                    int len = json.Length;
                    // Find opening array bracket '['
                    while (i < len && json[i] != '[') i++;
                    if (i >= len) return;
                    i++; // skip '['

                    var itemRegex = new Regex(@"\{\s*""id""\s*:\s*""(?<id>[^""]*)""\s*,\s*""title""\s*:\s*""(?<title>(?:\\.|[^""])*)""\s*,\s*""duration""\s*:\s*(?<dur>\d+)\s*\}", RegexOptions.Singleline);

                    while (i < len)
                    {
                        // Find next playlist object start '{'
                        while (i < len && json[i] != '{' && json[i] != ']') i++;
                        if (i >= len || json[i] == ']') break;

                        int objStart = i;
                        int depth = 0;
                        bool inString = false;
                        bool escaped = false;

                        // Scan balanced object boundaries correctly handling strings and escapes
                        while (i < len)
                        {
                            char c = json[i];
                            if (escaped)
                            {
                                escaped = false;
                            }
                            else if (c == '\\')
                            {
                                if (inString) escaped = true;
                            }
                            else if (c == '"')
                            {
                                inString = !inString;
                            }
                            else if (!inString)
                            {
                                if (c == '{') depth++;
                                else if (c == '}')
                                {
                                    depth--;
                                    if (depth == 0) { i++; break; }
                                }
                            }
                            i++;
                        }

                        string plBlock = json.Substring(objStart, i - objStart);

                        // Extract id, name, created from this playlist block
                        var mId = Regex.Match(plBlock, @"""id""\s*:\s*""([^""]*)""");
                        var mName = Regex.Match(plBlock, @"""name""\s*:\s*""((?:\\.|[^""])*)""");
                        var mCreated = Regex.Match(plBlock, @"""created""\s*:\s*""([^""]*)""");

                        string id = mId.Success ? mId.Groups[1].Value : Guid.NewGuid().ToString("N");
                        string name = mName.Success ? UnescapeJson(mName.Groups[1].Value) : "Playlist";
                        DateTime created = DateTime.Now;
                        if (mCreated.Success)
                        {
                            string cVal = mCreated.Groups[1].Value;
                            if (long.TryParse(cVal, out long ms))
                            {
                                try { created = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime; } catch { }
                            }
                            else
                            {
                                DateTime.TryParse(cVal, out created);
                            }
                        }

                        var pl = new UserPlaylist
                        {
                            Id = id,
                            Name = name,
                            CreatedAt = created,
                            Items = new List<PlaylistItem>()
                        };

                        // Extract items array block with balanced bracket scanning
                        int itemsIdx = plBlock.IndexOf("\"items\"");
                        if (itemsIdx >= 0)
                        {
                            int bStart = plBlock.IndexOf('[', itemsIdx);
                            if (bStart >= 0)
                            {
                                int bEnd = bStart + 1;
                                int bDepth = 1;
                                bool bInStr = false;
                                bool bEsc = false;
                                while (bEnd < plBlock.Length && bDepth > 0)
                                {
                                    char c = plBlock[bEnd];
                                    if (bEsc) bEsc = false;
                                    else if (c == '\\') { if (bInStr) bEsc = true; }
                                    else if (c == '"') bInStr = !bInStr;
                                    else if (!bInStr)
                                    {
                                        if (c == '[') bDepth++;
                                        else if (c == ']') bDepth--;
                                    }
                                    bEnd++;
                                }

                                string itemsBlock = plBlock.Substring(bStart, bEnd - bStart);
                                var itemMatches = itemRegex.Matches(itemsBlock);
                                foreach (Match im in itemMatches)
                                {
                                    string songId = im.Groups["id"].Value;
                                    string title = UnescapeJson(im.Groups["title"].Value);
                                    int dur = 0;
                                    int.TryParse(im.Groups["dur"].Value, out dur);

                                    pl.Items.Add(new PlaylistItem
                                    {
                                        Id = songId,
                                        Title = title,
                                        DurationSeconds = dur
                                    });
                                }
                            }
                        }

                        _playlists.Add(pl);
                    }
                }
                catch { }
            }
        }

        private void SaveToFile()
        {
            try
            {
                AppPaths.EnsureDirectoriesExist();
                var sb = new StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < _playlists.Count; i++)
                {
                    var pl = _playlists[i];
                    string escapedName = EscapeJson(pl.Name);
                    string plComma = (i < _playlists.Count - 1) ? "," : "";

                    sb.AppendLine("  {");
                    sb.AppendLine(string.Format("    \"id\": \"{0}\",", pl.Id));
                    sb.AppendLine(string.Format("    \"name\": \"{0}\",", escapedName));
                    sb.AppendLine(string.Format("    \"created\": \"{0:O}\",", pl.CreatedAtDateTime));
                    sb.AppendLine("    \"items\": [");

                    var items = pl.Items ?? new List<PlaylistItem>();
                    for (int j = 0; j < items.Count; j++)
                    {
                        var it = items[j];
                        string escapedTitle = EscapeJson(it.Title ?? "");
                        string itComma = (j < items.Count - 1) ? "," : "";
                        sb.AppendLine(string.Format("      {{ \"id\": \"{0}\", \"title\": \"{1}\", \"duration\": {2} }}{3}",
                            it.Id, escapedTitle, it.DurationSeconds, itComma));
                    }

                    sb.AppendLine("    ]");
                    sb.AppendLine(string.Format("  }}{0}", plComma));
                }
                sb.AppendLine("]");

                File.WriteAllText(AppPaths.PlaylistsJsonFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }

        private string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
