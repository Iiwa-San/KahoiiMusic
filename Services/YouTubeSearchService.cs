using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KahoiiMusic.Models;

namespace KahoiiMusic.Services
{
    public static class YouTubeSearchService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly Dictionary<string, (DateTime ExpireAt, List<PlaylistItem> Results)> _cache = new Dictionary<string, (DateTime ExpireAt, List<PlaylistItem> Results)>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new object();

        static YouTubeSearchService()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(8);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public static async Task<List<PlaylistItem>> SearchAsync(string query, int maxResults = 20, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<PlaylistItem>();
            query = query.Trim();

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(query, out var entry) && entry.ExpireAt > DateTime.UtcNow)
                {
                    if (entry.Results.Count >= maxResults || entry.Results.Count > 0)
                    {
                        int take = Math.Min(maxResults, entry.Results.Count);
                        return entry.Results.GetRange(0, take);
                    }
                }
            }

            try
            {
                string payload = string.Format(
                    "{{\"query\":\"{0}\",\"context\":{{\"client\":{{\"clientName\":\"WEB\",\"clientVersion\":\"2.20240101.00.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}",
                    EscapeJson(query));

                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using (var response = await _httpClient.PostAsync("https://www.youtube.com/youtubei/v1/search", content, ct).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) return new List<PlaylistItem>();
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    int parseCount = Math.Max(30, maxResults);
                    var parsed = ParseSearchResults(json, parseCount);

                    // Fetch next page via continuation token if user requested more items
                    if (parsed.Count < maxResults)
                    {
                        var mToken = Regex.Match(json, @"""continuationCommand""\s*:\s*\{[^}]*?""token""\s*:\s*""([^""]+)""");
                        if (mToken.Success)
                        {
                            try
                            {
                                string contPayload = string.Format(
                                    "{{\"continuation\":\"{0}\",\"context\":{{\"client\":{{\"clientName\":\"WEB\",\"clientVersion\":\"2.20240101.00.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}",
                                    EscapeJson(mToken.Groups[1].Value));
                                var contContent = new StringContent(contPayload, Encoding.UTF8, "application/json");
                                using (var contResponse = await _httpClient.PostAsync("https://www.youtube.com/youtubei/v1/search", contContent, ct).ConfigureAwait(false))
                                {
                                    if (contResponse.IsSuccessStatusCode)
                                    {
                                        string contJson = await contResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                                        var moreItems = ParseSearchResults(contJson, maxResults - parsed.Count);
                                        parsed.AddRange(moreItems);
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    lock (_cacheLock)
                    {
                        if (_cache.Count > 60)
                        {
                            _cache.Clear();
                        }
                        _cache[query] = (DateTime.UtcNow.AddMinutes(5), parsed);
                    }

                    int returnCount = Math.Min(maxResults, parsed.Count);
                    return parsed.GetRange(0, returnCount);
                }
            }
            catch
            {
                return new List<PlaylistItem>();
            }
        }

        public static List<PlaylistItem> ParseSearchResults(string json, int maxResults = 20)
        {
            var results = new List<PlaylistItem>();
            if (string.IsNullOrEmpty(json)) return results;

            string[] blocks = Regex.Split(json, @"""videoRenderer""\s*:\s*\{");
            for (int i = 1; i < blocks.Length && results.Count < maxResults; i++)
            {
                string block = blocks[i];
                var mId = Regex.Match(block, @"""videoId""\s*:\s*""([a-zA-Z0-9_-]{11})""");
                if (!mId.Success) continue;
                string videoId = mId.Groups[1].Value;

                // Title
                var mTitle = Regex.Match(block, @"""title""\s*:\s*\{[^}]*?""text""\s*:\s*""((?:\\.|[^""\\])*)""", RegexOptions.Singleline);
                string title = mTitle.Success ? UnescapeJson(mTitle.Groups[1].Value) : "Bài hát";

                // Channel/Author
                var mAuthor = Regex.Match(block, @"""(?:longBylineText|ownerText|shortBylineText)""\s*:\s*\{[^}]*?""text""\s*:\s*""((?:\\.|[^""\\])*)""", RegexOptions.Singleline);
                string author = mAuthor.Success ? UnescapeJson(mAuthor.Groups[1].Value) : string.Empty;

                // Duration
                int durationSeconds = 0;
                var mDur = Regex.Match(block, @"""lengthText""\s*:\s*\{.+?""simpleText""\s*:\s*""([0-9:]+)""", RegexOptions.Singleline);
                if (!mDur.Success)
                {
                    mDur = Regex.Match(block, @"""thumbnailOverlayTimeStatusRenderer""\s*:\s*\{.+?""simpleText""\s*:\s*""([0-9:]+)""", RegexOptions.Singleline);
                }
                if (!mDur.Success)
                {
                    mDur = Regex.Match(block, @"""lengthText""\s*:\s*\{.+?""text""\s*:\s*""([0-9:]+)""", RegexOptions.Singleline);
                }
                if (!mDur.Success)
                {
                    mDur = Regex.Match(block, @"""thumbnailOverlayTimeStatusRenderer""\s*:\s*\{.+?""text""\s*:\s*""([0-9:]+)""", RegexOptions.Singleline);
                }
                if (mDur.Success)
                {
                    durationSeconds = ParseDuration(mDur.Groups[1].Value);
                }

                results.Add(new PlaylistItem
                {
                    Id = videoId,
                    Title = title,
                    Author = author,
                    DurationSeconds = durationSeconds
                });
            }
            return results;
        }

        private static int ParseDuration(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string[] parts = text.Split(':');
            if (parts.Length == 2)
            {
                int.TryParse(parts[0], out int min);
                int.TryParse(parts[1], out int sec);
                return min * 60 + sec;
            }
            if (parts.Length == 3)
            {
                int.TryParse(parts[0], out int hr);
                int.TryParse(parts[1], out int min);
                int.TryParse(parts[2], out int sec);
                return hr * 3600 + min * 60 + sec;
            }
            return 0;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "");
        }

        private static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            try { return Regex.Unescape(s); } catch { return s; }
        }
    }
}

