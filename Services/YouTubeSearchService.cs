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

    public class ArtistItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Subscribers { get; set; }
        public string ThumbnailUrl { get; set; }
    }

    public class PlaylistSearchItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string TrackCountText { get; set; }
        public string ThumbnailUrl { get; set; }
    }

    public class StructuredSearchResult
    {
        public List<PlaylistItem> Tracks { get; set; } = new List<PlaylistItem>();
        public string NextContinuationToken { get; set; }
        public List<ArtistItem> Artists { get; set; } = new List<ArtistItem>();
        public List<PlaylistSearchItem> Playlists { get; set; } = new List<PlaylistSearchItem>();
    }

    public class ArtistProfileResult
    {
        public ArtistItem Artist { get; set; } = new ArtistItem();
        public List<PlaylistItem> Tracks { get; set; } = new List<PlaylistItem>();
        public List<PlaylistSearchItem> Playlists { get; set; } = new List<PlaylistSearchItem>();
    }

    public class DiscoveryFeedResult
    {
        public List<PlaylistItem> Trending { get; set; } = new List<PlaylistItem>();
        public List<PlaylistItem> NewReleases { get; set; } = new List<PlaylistItem>();
        public List<ArtistItem> Artists { get; set; } = new List<ArtistItem>();
        public List<PlaylistItem> Recommended { get; set; } = new List<PlaylistItem>();
    }

    public static class YouTubeSearchService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly Dictionary<string, (DateTime ExpireAt, List<PlaylistItem> Results, string NextToken)> _cache = new Dictionary<string, (DateTime ExpireAt, List<PlaylistItem> Results, string NextToken)>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new object();
        private static readonly Dictionary<string, string> _artistResolveCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _artistPending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim _artistSem = new SemaphoreSlim(3, 3);

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

        public static async Task<List<ArtistItem>> SearchArtistsAsync(string query, int maxResults = 10, CancellationToken ct = default)
        {
            var results = new List<ArtistItem>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            try
            {
                string payload = string.Format(
                    "{{\"query\":\"{0}\",\"params\":\"EgWKAQIgAWoSEAUQAxAJEAQQChAVEBAQERAO\",\"context\":{{\"client\":{{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20240101.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}",
                    EscapeJson(query.Trim()));

                using (var req = new HttpRequestMessage(HttpMethod.Post, "https://music.youtube.com/youtubei/v1/search"))
                {
                    req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                    req.Headers.TryAddWithoutValidation("Referer", "https://music.youtube.com/");
                    req.Headers.TryAddWithoutValidation("Origin", "https://music.youtube.com");

                    using (var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) return results;
                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        string[] blocks = Regex.Split(json, @"""musicResponsiveListItemRenderer""\s*:\s*\{");
                        for (int i = 1; i < blocks.Length && results.Count < maxResults; i++)
                        {
                            string block = blocks[i];
                            var mId = Regex.Match(block, @"""browseId""\s*:\s*""(UC[^""]+)""");
                            if (!mId.Success) continue;
                            string browseId = mId.Groups[1].Value;

                            var mRuns = Regex.Matches(block, @"""flexColumns"".*?""text""\s*:\s*""((?:\\.|[^""\\])*)""", RegexOptions.Singleline);
                            string name = mRuns.Count > 0 ? UnescapeJson(mRuns[0].Groups[1].Value) : string.Empty;
                            if (string.IsNullOrWhiteSpace(name)) continue;

                            // Extract subscriber text if present
                            string subText = string.Empty;
                            var mSub = Regex.Match(block, @"""text""\s*:\s*""([^""]*(?:người đăng ký|subscribers|sub)[^""]*)""", RegexOptions.IgnoreCase);
                            if (mSub.Success) subText = UnescapeJson(mSub.Groups[1].Value);

                            // Extract largest thumbnail URL
                            string thumbUrl = string.Empty;
                            var mThumbs = Regex.Match(block, @"""thumbnails""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                            if (mThumbs.Success)
                            {
                                var urls = Regex.Matches(mThumbs.Groups[1].Value, @"""url""\s*:\s*""([^""]+)""");
                                if (urls.Count > 0)
                                {
                                    thumbUrl = UnescapeJson(urls[urls.Count - 1].Groups[1].Value);
                                    thumbUrl = Regex.Replace(thumbUrl, @"=w\d+-h\d+", "=w240-h240");
                                }
                            }

                            if (!results.Exists(a => string.Equals(a.Id, browseId, StringComparison.OrdinalIgnoreCase)))
                            {
                                results.Add(new ArtistItem
                                {
                                    Id = browseId,
                                    Name = name,
                                    Subscribers = subText,
                                    ThumbnailUrl = thumbUrl
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            return results;
        }

        private static bool IsCompilationOrNonSong(string title, int durationSeconds)
        {
            if (durationSeconds > 720) return true; // > 12 min is almost always a mix/compilation
            if (string.IsNullOrWhiteSpace(title)) return false;
            return Regex.IsMatch(title,
                @"\b(top\s*\d+|tuyển\s*tập|liên\s*khúc|nonstop|mashup\s*\d+|1\s*giờ|1\s*hour|best\s+songs|nhạc\s+tổng\s+hợp|playlist\s+nhạc)\b",
                RegexOptions.IgnoreCase);
        }

        public static async Task<List<PlaylistItem>> SearchSongsYtmAsync(string query, int maxResults = 25, CancellationToken ct = default)
        {
            var results = new List<PlaylistItem>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            try
            {
                string payload = string.Format(
                    "{{\"query\":\"{0}\",\"params\":\"EgWKAQIIAWoSEAUQAxAJEAQQChAVEBAQERAO\",\"context\":{{\"client\":{{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20240101.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}",
                    EscapeJson(query.Trim()));

                using (var req = new HttpRequestMessage(HttpMethod.Post, "https://music.youtube.com/youtubei/v1/search"))
                {
                    req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                    req.Headers.TryAddWithoutValidation("Referer", "https://music.youtube.com/");
                    req.Headers.TryAddWithoutValidation("Origin", "https://music.youtube.com");

                    using (var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) return results;
                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        string[] blocks = Regex.Split(json, @"""musicResponsiveListItemRenderer""\s*:\s*\{");
                        for (int i = 1; i < blocks.Length && results.Count < maxResults; i++)
                        {
                            string block = blocks[i];
                            var mId = Regex.Match(block, @"""videoId""\s*:\s*""([a-zA-Z0-9_-]{11})""");
                            if (!mId.Success) continue;
                            string videoId = mId.Groups[1].Value;

                            var mCols = Regex.Matches(block, @"""musicResponsiveListItemFlexColumnRenderer""\s*:\s*\{.*?""runs""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                            string title = string.Empty;
                            string author = string.Empty;
                            int durSec = 0;

                            if (mCols.Count > 0)
                            {
                                var mT = Regex.Match(mCols[0].Groups[1].Value, @"""text""\s*:\s*""((?:\\.|[^""\\])*)""");
                                if (mT.Success) title = UnescapeJson(mT.Groups[1].Value);
                            }
                            if (string.IsNullOrWhiteSpace(title)) continue;

                            if (mCols.Count > 1)
                            {
                                var runTexts = Regex.Matches(mCols[1].Groups[1].Value, @"""text""\s*:\s*""((?:\\.|[^""\\])*)""");
                                foreach (Match rt in runTexts)
                                {
                                    string txt = UnescapeJson(rt.Groups[1].Value).Trim();
                                    if (string.IsNullOrEmpty(txt) || txt == "•" || txt == "·" || txt == "," ||
                                        txt == "Bài hát" || txt == "Song" || txt == "Video") continue;

                                    if (Regex.IsMatch(txt, @"^\d+:\d+(?::\d+)?$"))
                                    {
                                        durSec = ParseDuration(txt);
                                        continue;
                                    }
                                    if (string.IsNullOrEmpty(author) &&
                                        !Regex.IsMatch(txt, @"^\d+[\s\u00a0]*(Tr|T|k|N|M|B)?\s*(lượt|views|plays)", RegexOptions.IgnoreCase))
                                    {
                                        author = txt;
                                    }
                                }
                            }

                            if (IsCompilationOrNonSong(title, durSec)) continue;

                            if (!results.Exists(x => string.Equals(x.Id, videoId, StringComparison.OrdinalIgnoreCase)))
                            {
                                results.Add(new PlaylistItem
                                {
                                    Id = videoId,
                                    Title = title,
                                    Author = !string.IsNullOrEmpty(author) ? author : ExtractSmartArtistFromTitle(title),
                                    DurationSeconds = durSec
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            return results;
        }

        public static async Task<DiscoveryFeedResult> GetDiscoveryFeedAsync(bool forceRefresh = false, CancellationToken ct = default)
        {
            string[] trendingQueries = new[]
            {
                "Nhạc trẻ Vpop hot",
                "Hit Vpop thịnh hành",
                "Nhạc Việt mới nổi bật",
                "Vpop top songs"
            };
            string[] newReleaseQueries = new[]
            {
                "Nhạc Vpop mới phát hành",
                "Bài hát Việt mới nhất",
                "Single Vpop mới"
            };
            string[] artistQueries = new[]
            {
                "Ca sĩ Việt Nam nổi bật",
                "Nghệ sĩ Vpop",
                "Ca sĩ trẻ Việt Nam"
            };

            var rng = new Random();
            string qTrend = trendingQueries[rng.Next(trendingQueries.Length)];
            string qNew = newReleaseQueries[rng.Next(newReleaseQueries.Length)];
            string qArtist = artistQueries[rng.Next(artistQueries.Length)];

            // Personalize "Có thể bạn sẽ thích" from Favorites / History
            string qRec = "Nhạc chill acoustic Việt";
            try
            {
                var favs = FavoritesService.GetFavorites();
                var hist = HistoryService.GetHistory();
                var pool = (favs != null && favs.Count > 0) ? favs : hist;
                if (pool != null && pool.Count > 0)
                {
                    var sample = pool[rng.Next(Math.Min(pool.Count, 10))];
                    string art = !string.IsNullOrWhiteSpace(sample?.Author) ? sample.Author : ExtractSmartArtistFromTitle(sample?.Title);
                    if (!string.IsNullOrWhiteSpace(art) && !string.Equals(art, "YouTube", StringComparison.OrdinalIgnoreCase))
                    {
                        qRec = art;
                    }
                }
            }
            catch { }

            var tTrend = SearchSongsYtmAsync(qTrend, 12, ct);
            var tNew = SearchSongsYtmAsync(qNew, 12, ct);
            var tRec = SearchSongsYtmAsync(qRec, 12, ct);

            await Task.WhenAll(tTrend, tNew, tRec).ConfigureAwait(false);

            var trending = tTrend.Result ?? new List<PlaylistItem>();
            var newReleases = tNew.Result ?? new List<PlaylistItem>();
            var recommended = tRec.Result ?? new List<PlaylistItem>();

            // Build 6-8 diverse Featured Artists from the fetched Trending & New Releases songs
            var artistNames = new List<string>();
            foreach (var it in trending)
            {
                if (!string.IsNullOrWhiteSpace(it.Author) &&
                    !it.Author.Equals("Various Artists", StringComparison.OrdinalIgnoreCase) &&
                    !artistNames.Exists(x => x.Equals(it.Author, StringComparison.OrdinalIgnoreCase)))
                {
                    artistNames.Add(it.Author);
                    if (artistNames.Count >= 6) break;
                }
            }
            if (artistNames.Count < 6)
            {
                foreach (var it in newReleases)
                {
                    if (!string.IsNullOrWhiteSpace(it.Author) &&
                        !it.Author.Equals("Various Artists", StringComparison.OrdinalIgnoreCase) &&
                        !artistNames.Exists(x => x.Equals(it.Author, StringComparison.OrdinalIgnoreCase)))
                    {
                        artistNames.Add(it.Author);
                        if (artistNames.Count >= 6) break;
                    }
                }
            }

            var artistTasks = new List<Task<List<ArtistItem>>>();
            if (artistNames.Count > 0)
            {
                foreach (var name in artistNames)
                {
                    artistTasks.Add(SearchArtistsAsync(name, 1, ct));
                }
            }
            else
            {
                artistTasks.Add(SearchArtistsAsync(qArtist, 8, ct));
            }

            var artistResults = await Task.WhenAll(artistTasks).ConfigureAwait(false);
            var artists = new List<ArtistItem>();
            foreach (var list in artistResults)
            {
                if (list == null) continue;
                foreach (var a in list)
                {
                    if (!artists.Exists(x => string.Equals(x.Id, a.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        artists.Add(a);
                    }
                }
            }

            var feed = new DiscoveryFeedResult
            {
                Trending = trending,
                NewReleases = newReleases,
                Artists = artists,
                Recommended = recommended
            };

            // Release transient JSON/regex memory immediately after parsing
            MemoryOptimizer.TrimMemoryAsync(400, aggressive: true);

            return feed;
        }

        public static async Task<List<PlaylistSearchItem>> SearchPlaylistsAsync(string query, int maxResults = 12, CancellationToken ct = default)
        {
            var results = new List<PlaylistSearchItem>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            try
            {
                string payload = string.Format(
                    "{{\"query\":\"{0}\",\"params\":\"EgeKAQQoAEABahIQBRADEAkQBBAKEBUQEBAREA4%3D\",\"context\":{{\"client\":{{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20240101.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}",
                    EscapeJson(query.Trim()));

                using (var req = new HttpRequestMessage(HttpMethod.Post, "https://music.youtube.com/youtubei/v1/search"))
                {
                    req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                    req.Headers.TryAddWithoutValidation("Referer", "https://music.youtube.com/");
                    req.Headers.TryAddWithoutValidation("Origin", "https://music.youtube.com");

                    using (var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) return results;
                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        string[] blocks = Regex.Split(json, @"""musicResponsiveListItemRenderer""\s*:\s*\{");
                        for (int i = 1; i < blocks.Length && results.Count < maxResults; i++)
                        {
                            string block = blocks[i];
                            var mId = Regex.Match(block, @"""(?:browseId|playlistId)""\s*:\s*""((?:VL|PL|OLAK|RDCLAK)[^""]+)""");
                            if (!mId.Success) continue;
                            string plId = mId.Groups[1].Value;

                            var mRuns = Regex.Matches(block, @"""flexColumns"".*?""text""\s*:\s*""((?:\\.|[^""\\])*)""", RegexOptions.Singleline);
                            string title = mRuns.Count > 0 ? UnescapeJson(mRuns[0].Groups[1].Value) : string.Empty;
                            if (string.IsNullOrWhiteSpace(title)) continue;

                            string countText = string.Empty;
                            var mCnt = Regex.Match(block, @"""text""\s*:\s*""([^""]*(?:bài hát|songs|tracks|video)[^""]*)""", RegexOptions.IgnoreCase);
                            if (mCnt.Success) countText = UnescapeJson(mCnt.Groups[1].Value);

                            string thumbUrl = string.Empty;
                            var mThumbs = Regex.Match(block, @"""thumbnails""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                            if (mThumbs.Success)
                            {
                                var urls = Regex.Matches(mThumbs.Groups[1].Value, @"""url""\s*:\s*""([^""]+)""");
                                if (urls.Count > 0)
                                {
                                    thumbUrl = UnescapeJson(urls[urls.Count - 1].Groups[1].Value);
                                    thumbUrl = Regex.Replace(thumbUrl, @"=w\d+-h\d+", "=w240-h240");
                                }
                            }

                            if (!results.Exists(p => string.Equals(p.Id, plId, StringComparison.OrdinalIgnoreCase)))
                            {
                                results.Add(new PlaylistSearchItem
                                {
                                    Id = plId,
                                    Title = title,
                                    Author = "YouTube Music",
                                    TrackCountText = countText,
                                    ThumbnailUrl = thumbUrl
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            return results;
        }

        public static async Task<StructuredSearchResult> SearchStructuredAsync(string query, CancellationToken ct = default)
        {
            var tYtmSongs = SearchSongsYtmAsync(query, 30, ct);
            var tArtists = SearchArtistsAsync(query, 10, ct);
            var tPlaylists = SearchPlaylistsAsync(query, 12, ct);

            await Task.WhenAll(tYtmSongs, tArtists, tPlaylists).ConfigureAwait(false);

            var tracks = tYtmSongs.Result ?? new List<PlaylistItem>();
            string nextToken = null;
            if (tracks.Count < 8)
            {
                var fallback = await SearchBatchAsync(query, null, ct).ConfigureAwait(false);
                if (fallback?.Items != null)
                {
                    foreach (var it in fallback.Items)
                    {
                        if (!IsCompilationOrNonSong(it.Title, it.DurationSeconds) &&
                            !tracks.Exists(x => string.Equals(x.Id, it.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            tracks.Add(it);
                        }
                    }
                    nextToken = fallback.NextContinuationToken;
                }
            }

            MemoryOptimizer.TrimMemoryAsync(400, aggressive: true);

            return new StructuredSearchResult
            {
                Tracks = tracks,
                NextContinuationToken = nextToken,
                Artists = tArtists.Result ?? new List<ArtistItem>(),
                Playlists = tPlaylists.Result ?? new List<PlaylistSearchItem>()
            };
        }

        public static async Task<ArtistProfileResult> GetArtistProfileAsync(ArtistItem artist, CancellationToken ct = default)
        {
            var res = new ArtistProfileResult
            {
                Artist = new ArtistItem
                {
                    Id = artist?.Id ?? string.Empty,
                    Name = artist?.Name ?? "Nghệ sĩ",
                    Subscribers = artist?.Subscribers ?? string.Empty,
                    ThumbnailUrl = artist?.ThumbnailUrl ?? string.Empty
                }
            };

            if (artist == null || string.IsNullOrWhiteSpace(artist.Id) || !artist.Id.StartsWith("UC"))
            {
                return res;
            }

            try
            {
                string payload = string.Format(
                    "{{\"browseId\":\"{0}\",\"context\":{{\"client\":{{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20240101.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}",
                    EscapeJson(artist.Id));

                using (var req = new HttpRequestMessage(HttpMethod.Post, "https://music.youtube.com/youtubei/v1/browse"))
                {
                    req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                    req.Headers.TryAddWithoutValidation("Referer", "https://music.youtube.com/");
                    req.Headers.TryAddWithoutValidation("Origin", "https://music.youtube.com");

                    using (var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) return res;
                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        // Header info (Subscribers, Avatar)
                        var mSub = Regex.Match(json, @"""subscriberCountText""\s*:\s*\{.*?""text""\s*:\s*""([^""]+)""", RegexOptions.Singleline);
                        if (mSub.Success && string.IsNullOrWhiteSpace(res.Artist.Subscribers))
                        {
                            res.Artist.Subscribers = UnescapeJson(mSub.Groups[1].Value);
                        }

                        var mHeaderThumb = Regex.Match(json, @"""music(?:Immersive|Visual)HeaderRenderer"".*?""thumbnails""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                        if (mHeaderThumb.Success)
                        {
                            var urls = Regex.Matches(mHeaderThumb.Groups[1].Value, @"""url""\s*:\s*""([^""]+)""");
                            if (urls.Count > 0)
                            {
                                string u = UnescapeJson(urls[urls.Count - 1].Groups[1].Value);
                                res.Artist.ThumbnailUrl = Regex.Replace(u, @"=w\d+-h\d+", "=w240-h240");
                            }
                        }

                        // 1. Fetch ALL songs via bottomEndpoint ("Xem tất cả") if available
                        string songsJson = json;
                        var mBottom = Regex.Match(json, @"""bottomEndpoint""\s*:\s*\{[^}]*?""browseEndpoint""\s*:\s*\{([^}]+)\}", RegexOptions.Singleline);
                        if (mBottom.Success)
                        {
                            var mBid = Regex.Match(mBottom.Groups[1].Value, @"""browseId""\s*:\s*""([^""]+)""");
                            var mParams = Regex.Match(mBottom.Groups[1].Value, @"""params""\s*:\s*""([^""]+)""");
                            if (mBid.Success)
                            {
                                string subPayload = mParams.Success
                                    ? string.Format("{{\"browseId\":\"{0}\",\"params\":\"{1}\",\"context\":{{\"client\":{{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20240101.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}", EscapeJson(mBid.Groups[1].Value), EscapeJson(mParams.Groups[1].Value))
                                    : string.Format("{{\"browseId\":\"{0}\",\"context\":{{\"client\":{{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20240101.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}", EscapeJson(mBid.Groups[1].Value));

                                using (var subReq = new HttpRequestMessage(HttpMethod.Post, "https://music.youtube.com/youtubei/v1/browse"))
                                {
                                    subReq.Content = new StringContent(subPayload, Encoding.UTF8, "application/json");
                                    subReq.Headers.TryAddWithoutValidation("Referer", "https://music.youtube.com/");
                                    subReq.Headers.TryAddWithoutValidation("Origin", "https://music.youtube.com");
                                    using (var subResp = await _httpClient.SendAsync(subReq, ct).ConfigureAwait(false))
                                    {
                                        if (subResp.IsSuccessStatusCode)
                                        {
                                            string allJson = await subResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                                            if (!string.IsNullOrWhiteSpace(allJson) && allJson.Contains("musicResponsiveListItemRenderer"))
                                            {
                                                songsJson = allJson;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // 2. Parse Songs (musicResponsiveListItemRenderer) with fixedColumns + flexColumns duration
                        string[] songBlocks = Regex.Split(songsJson, @"""musicResponsiveListItemRenderer""\s*:\s*\{");
                        for (int i = 1; i < songBlocks.Length && res.Tracks.Count < 60; i++)
                        {
                            string block = songBlocks[i];
                            var mId = Regex.Match(block, @"""videoId""\s*:\s*""([a-zA-Z0-9_-]{11})""");
                            if (!mId.Success) continue;
                            string videoId = mId.Groups[1].Value;

                            var mCols = Regex.Matches(block, @"""musicResponsiveListItemFlexColumnRenderer""\s*:\s*\{.*?""runs""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                            string title = string.Empty;
                            if (mCols.Count > 0)
                            {
                                var mT = Regex.Match(mCols[0].Groups[1].Value, @"""text""\s*:\s*""((?:\\.|[^""\\])*)""");
                                if (mT.Success) title = UnescapeJson(mT.Groups[1].Value);
                            }
                            if (string.IsNullOrWhiteSpace(title)) continue;

                            int durSec = 0;
                            // Check fixedColumns first (standard for YTM browse), then flexColumns
                            var mFixedDur = Regex.Match(block, @"""musicResponsiveListItemFixedColumnRenderer"".*?""text""\s*:\s*""(\d+:\d+(?::\d+)?)""", RegexOptions.Singleline);
                            if (mFixedDur.Success)
                            {
                                durSec = ParseDuration(mFixedDur.Groups[1].Value);
                            }
                            else
                            {
                                for (int c = 1; c < mCols.Count; c++)
                                {
                                    var mDur = Regex.Match(mCols[c].Groups[1].Value, @"""text""\s*:\s*""(\d+:\d+(?::\d+)?)""");
                                    if (mDur.Success) { durSec = ParseDuration(mDur.Groups[1].Value); break; }
                                }
                            }

                            if (!res.Tracks.Exists(x => string.Equals(x.Id, videoId, StringComparison.OrdinalIgnoreCase)))
                            {
                                res.Tracks.Add(new PlaylistItem
                                {
                                    Id = videoId,
                                    Title = title,
                                    Author = res.Artist.Name,
                                    DurationSeconds = durSec
                                });
                            }
                        }

                        // Parse Albums / Playlists (musicTwoRowItemRenderer)
                        string[] plBlocks = Regex.Split(json, @"""musicTwoRowItemRenderer""\s*:\s*\{");
                        for (int i = 1; i < plBlocks.Length && res.Playlists.Count < 16; i++)
                        {
                            string block = plBlocks[i];
                            var mPlId = Regex.Match(block, @"""(?:browseId|playlistId)""\s*:\s*""((?:MPRE|VL|PL|OLAK)[^""]+)""");
                            if (!mPlId.Success) continue;
                            string plId = mPlId.Groups[1].Value;

                            var mTitle = Regex.Match(block, @"""title""\s*:\s*\{.*?""text""\s*:\s*""((?:\\.|[^""\\])*)""", RegexOptions.Singleline);
                            if (!mTitle.Success) continue;
                            string plTitle = UnescapeJson(mTitle.Groups[1].Value);

                            string sub = string.Empty;
                            var mSubPl = Regex.Match(block, @"""subtitle""\s*:\s*\{.*?""text""\s*:\s*""((?:\\.|[^""\\])*)""", RegexOptions.Singleline);
                            if (mSubPl.Success) sub = UnescapeJson(mSubPl.Groups[1].Value);

                            string thumbUrl = string.Empty;
                            var mThumbs = Regex.Match(block, @"""thumbnails""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                            if (mThumbs.Success)
                            {
                                var urls = Regex.Matches(mThumbs.Groups[1].Value, @"""url""\s*:\s*""([^""]+)""");
                                if (urls.Count > 0)
                                {
                                    thumbUrl = UnescapeJson(urls[urls.Count - 1].Groups[1].Value);
                                    thumbUrl = Regex.Replace(thumbUrl, @"=w\d+-h\d+", "=w240-h240");
                                }
                            }

                            if (!res.Playlists.Exists(x => string.Equals(x.Id, plId, StringComparison.OrdinalIgnoreCase)))
                            {
                                res.Playlists.Add(new PlaylistSearchItem
                                {
                                    Id = plId,
                                    Title = plTitle,
                                    Author = res.Artist.Name,
                                    TrackCountText = sub,
                                    ThumbnailUrl = thumbUrl
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            MemoryOptimizer.TrimMemoryAsync(400, aggressive: true);
            return res;
        }

        public static string ExtractSmartArtistFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;

            string builtIn = AudioPlayerService.ExtractAuthorFromTitle(title);
            if (!string.IsNullOrWhiteSpace(builtIn)) return builtIn.Trim();

            string t = Regex.Replace(title, @"\s*[\(\[](?:Official\s*(?:MV|Video|Audio|Lyric\s*Video)|MV|Lyrics?|Audio|HD|4K|Vietsub|Engsub)[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase).Trim();

            var mDash = Regex.Match(t, @"^([^\-–—|\/]{2,40}?)\s+[\-–—]\s+.+$");
            if (mDash.Success) return mDash.Groups[1].Value.Trim();

            var mSlash = Regex.Match(t, @"^.+\s+\/\s+([^\/\|]{2,40})$");
            if (mSlash.Success) return mSlash.Groups[1].Value.Trim();

            var mCov = Regex.Match(t, @"(?:covered\s+by|cover\s+by|by)\s+([^\|\(\[\-]{2,35})", RegexOptions.IgnoreCase);
            if (mCov.Success) return mCov.Groups[1].Value.Trim();

            return string.Empty;
        }

        public static string GetCachedArtist(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return null;
            lock (_cacheLock)
            {
                return _artistResolveCache.TryGetValue(videoId, out string val) ? val : null;
            }
        }

        public static void QueueResolveArtist(PlaylistItem item, Action onResolved)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;

            lock (_cacheLock)
            {
                if (_artistResolveCache.TryGetValue(item.Id, out string cached))
                {
                    if (!string.IsNullOrWhiteSpace(cached)) item.Author = cached;
                    return;
                }
                if (_artistPending.Contains(item.Id)) return;
                _artistPending.Add(item.Id);
            }

            ThreadPool.QueueUserWorkItem(async delegate
            {
                string resolved = null;
                try
                {
                    await _artistSem.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        string payload = string.Format(
                            "{{\"videoId\":\"{0}\",\"context\":{{\"client\":{{\"clientName\":\"WEB\",\"clientVersion\":\"2.20240101.00.00\",\"hl\":\"vi\",\"gl\":\"VN\"}}}}}}",
                            EscapeJson(item.Id));
                        using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
                        using (var resp = await _httpClient.PostAsync("https://www.youtube.com/youtubei/v1/player", content).ConfigureAwait(false))
                        {
                            if (resp.IsSuccessStatusCode)
                            {
                                string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                                // 1. Check shortDescription for "Title · Artist" or "Vocal/Artist/Ca sĩ: ..."
                                var mDesc = Regex.Match(json, @"""shortDescription""\s*:\s*""((?:\\.|[^""\\])*)""");
                                if (mDesc.Success)
                                {
                                    string desc = UnescapeJson(mDesc.Groups[1].Value);
                                    string[] lines = desc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                    for (int i = 0; i < Math.Min(lines.Length, 25); i++)
                                    {
                                        string line = lines[i].Trim();
                                        if (line.Contains("·") && !line.StartsWith("Provided to YouTube", StringComparison.OrdinalIgnoreCase))
                                        {
                                            string[] parts = line.Split('·');
                                            if (parts.Length >= 2)
                                            {
                                                string cand = parts[parts.Length - 1].Trim();
                                                if (cand.Length >= 2 && cand.Length <= 60) { resolved = cand; break; }
                                            }
                                        }
                                        var mVoc = Regex.Match(line, @"(?:vocal|artist|singer|ca sĩ|trình bày|cover by|performed by)\s*[:：]\s*([^\|,\r\n]{2,50})", RegexOptions.IgnoreCase);
                                        if (mVoc.Success)
                                        {
                                            resolved = mVoc.Groups[1].Value.Trim();
                                            break;
                                        }
                                    }
                                }

                                // 2. Fallback to channel/author name
                                if (string.IsNullOrWhiteSpace(resolved))
                                {
                                    var mAuth = Regex.Match(json, @"""videoDetails""\s*:\s*\{.*?""author""\s*:\s*""((?:\\.|[^""\\])*)""", RegexOptions.Singleline);
                                    if (mAuth.Success)
                                    {
                                        resolved = UnescapeJson(mAuth.Groups[1].Value).Trim();
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        _artistSem.Release();
                    }
                }
                catch { }

                lock (_cacheLock)
                {
                    _artistPending.Remove(item.Id);
                    _artistResolveCache[item.Id] = resolved ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    item.Author = resolved;
                    try { onResolved?.Invoke(); } catch { }
                }
            });
        }
    }
}

