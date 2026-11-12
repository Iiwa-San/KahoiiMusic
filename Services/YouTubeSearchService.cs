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
    public class SearchResultBatch
    {
        public List<PlaylistItem> Items { get; set; } = new List<PlaylistItem>();
        public string NextContinuationToken { get; set; }
    }

    public static class YouTubeSearchService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly Dictionary<string, (DateTime ExpireAt, List<PlaylistItem> Results, string NextToken)> _cache = new Dictionary<string, (DateTime ExpireAt, List<PlaylistItem> Results, string NextToken)>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new object();

        static YouTubeSearchService()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(8);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public static async Task<SearchResultBatch> SearchBatchAsync(string query, string continuationToken = null, CancellationToken ct = default)
        {
            var batch = new SearchResultBatch();
            if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(continuationToken)) return batch;
            query = query?.Trim() ?? string.Empty;

            // If initial search and cached
            if (string.IsNullOrEmpty(continuationToken))
            {
                lock (_cacheLock)
                {
                    if (_cache.TryGetValue(query, out var entry) && entry.ExpireAt > DateTime.UtcNow && entry.Results.Count > 0)
                    {
                        batch.Items = new List<PlaylistItem>(entry.Results);
                        batch.NextContinuationToken = entry.NextToken;
                        return batch;
                    }
                }
            }

            try
            {
                string curToken = continuationToken;
                int hops = 0;
                // Fetch at least 20 items per batch, hopping forward up to 3 times if YouTube returns empty shelf (e.g. Shorts)
                while (batch.Items.Count < (string.IsNullOrEmpty(continuationToken) ? 35 : 20) && hops < 3)
                {
                    hops++;
                    string payload;
                    if (!string.IsNullOrEmpty(curToken))
                    {
                        payload = string.Format(
                            "{{\"continuation\":\"{0}\",\"context\":{{\"client\":{{\"clientName\":\"WEB\",\"clientVersion\":\"2.20240101.00.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}",
                            EscapeJson(curToken));
                    }
                    else
                    {
                        payload = string.Format(
                            "{{\"query\":\"{0}\",\"context\":{{\"client\":{{\"clientName\":\"WEB\",\"clientVersion\":\"2.20240101.00.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}",
                            EscapeJson(query));
                    }

                    var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    using (var response = await _httpClient.PostAsync("https://www.youtube.com/youtubei/v1/search", content, ct).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) break;
                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var items = ParseSearchResults(json, 40);
                        foreach (var it in items)
                        {
                            if (!batch.Items.Exists(x => string.Equals(x.Id, it.Id, StringComparison.OrdinalIgnoreCase)))
                            {
                                batch.Items.Add(it);
                            }
                        }

                        var mToken = Regex.Match(json, @"""continuationCommand""\s*:\s*\{[^}]*?""token""\s*:\s*""([^""]+)""");
                        curToken = mToken.Success ? mToken.Groups[1].Value : null;
                        batch.NextContinuationToken = curToken;

                        if (string.IsNullOrEmpty(curToken)) break;
                    }
                }

                if (string.IsNullOrEmpty(continuationToken) && batch.Items.Count > 0)
                {
                    lock (_cacheLock)
                    {
                        if (_cache.Count > 60) _cache.Clear();
                        _cache[query] = (DateTime.UtcNow.AddMinutes(5), batch.Items, batch.NextContinuationToken);
                    }
                }
            }
            catch { }

            return batch;
        }

        public static async Task<List<PlaylistItem>> SearchAsync(string query, int maxResults = 20, CancellationToken ct = default)
        {
            var batch = await SearchBatchAsync(query, null, ct).ConfigureAwait(false);
            if (batch.Items.Count >= maxResults)
            {
                return batch.Items.GetRange(0, maxResults);
            }
            return batch.Items;
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

