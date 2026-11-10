using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace KahoiiMusic.Services
{
    public class LyricLine
    {
        public TimeSpan Timestamp { get; set; }
        public string Text { get; set; }
        public string RomanizedText { get; set; }

        public LyricLine() { }

        public LyricLine(TimeSpan timestamp, string text, string romanizedText = null)
        {
            Timestamp = timestamp;
            Text = text ?? string.Empty;
            RomanizedText = romanizedText;
        }
    }

    public class LyricsData
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public bool HasLyrics { get; set; }
        public bool IsSynced { get; set; }
        public string DetectedLanguage { get; set; } // "ja", "zh", or null
        public List<LyricLine> Lines { get; set; } = new List<LyricLine>();
        public string PlainText { get; set; }

        public bool HasRomanization
        {
            get
            {
                return Lines != null && Lines.Exists(l => !string.IsNullOrEmpty(l.RomanizedText));
            }
        }

        public static LyricsData Empty(string videoId = "")
        {
            return new LyricsData
            {
                VideoId = videoId ?? string.Empty,
                HasLyrics = false,
                IsSynced = false,
                DetectedLanguage = null,
                Lines = new List<LyricLine>(),
                PlainText = string.Empty
            };
        }
    }

    public class TranslationResult
    {
        public string Translation { get; set; }
        public string Romaji { get; set; }
        public string SourceLang { get; set; }
    }

    public class LyricsService
    {
        private static readonly ConcurrentDictionary<string, TranslationResult> _translationMemoryCache =
            new ConcurrentDictionary<string, TranslationResult>(StringComparer.OrdinalIgnoreCase);

        public static bool ContainsCjk(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if ((c >= '\u3040' && c <= '\u309F') || // Hiragana
                    (c >= '\u30A0' && c <= '\u30FF') || // Katakana
                    (c >= '\u4E00' && c <= '\u9FFF') || // CJK Unified Ideographs
                    (c >= '\u3400' && c <= '\u4DBF') || // CJK Extension A
                    (c >= '\uAC00' && c <= '\uD7AF'))   // Hangul
                {
                    return true;
                }
            }
            return false;
        }

        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            string res = sb.ToString().Normalize(NormalizationForm.FormC);
            return Regex.Replace(res, "[āǎàá]", "a", RegexOptions.IgnoreCase)
                        .Replace("ō", "o").Replace("Ō", "O")
                        .Replace("ū", "u").Replace("Ū", "U")
                        .Replace("ē", "e").Replace("Ē", "E")
                        .Replace("ī", "i").Replace("Ī", "I");
        }

        public static TranslationResult TranslateAndRomanizeSync(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !ContainsCjk(text)) return null;
            string clean = text.Trim();
            if (_translationMemoryCache.TryGetValue(clean, out var cached)) return cached;

            try
            {
                string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=en&dt=t&dt=rm&q=" + Uri.EscapeDataString(clean);
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                    wc.Encoding = Encoding.UTF8;
                    string json = wc.DownloadString(url);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var serializer = new JavaScriptSerializer();
                        var list = serializer.Deserialize<ArrayList>(json);
                        if (list != null && list.Count > 0 && list[0] is ArrayList parts)
                        {
                            var sb = new StringBuilder();
                            string romaji = string.Empty;
                            foreach (var p in parts)
                            {
                                if (p is ArrayList item)
                                {
                                    if (item.Count > 0 && item[0] is string piece && !string.IsNullOrWhiteSpace(piece))
                                        sb.Append(piece);
                                    if (item.Count > 3 && item[3] is string rom && !string.IsNullOrWhiteSpace(rom))
                                        romaji = rom;
                                }
                            }
                            string srcLang = list.Count > 2 && list[2] is string sl ? sl : string.Empty;
                            var result = new TranslationResult
                            {
                                Translation = sb.ToString().Trim(),
                                Romaji = romaji.Trim(),
                                SourceLang = srcLang.Trim()
                            };
                            if (!string.IsNullOrEmpty(result.Translation) || !string.IsNullOrEmpty(result.Romaji))
                            {
                                _translationMemoryCache[clean] = result;
                                return result;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
        private static readonly Lazy<LyricsService> _instance = new Lazy<LyricsService>(() => new LyricsService());
        public static LyricsService Instance => _instance.Value;

        private readonly HttpClient _httpClient;
        private readonly JavaScriptSerializer _serializer;

        public LyricsService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(6);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

            try
            {
                CleanCorruptedTestCache();
            }
            catch { }
        }

        private static readonly string[] KnownCorruptedSnippets = new string[]
        {
            "ちょっと泣いているだけだよ",
            "風抖落雪霜",
            "切り分けた命のひとひら"
        };

        public static bool IsCorruptedTestLyrics(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;
            foreach (var snippet in KnownCorruptedSnippets)
            {
                if (content.Contains(snippet)) return true;
            }
            return false;
        }

        public void PurgeDiskCache(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return;
            try
            {
                string[] paths = new string[]
                {
                    Path.Combine(AppPaths.SavedLyricsDir, videoId + ".lrc"),
                    Path.Combine(AppPaths.SavedLyricsDir, videoId + ".txt"),
                    Path.Combine(AppPaths.SavedLyricsDir, videoId + "_romaji.json"),
                    Path.Combine(AppPaths.TempLyricsDir, videoId + ".lrc"),
                    Path.Combine(AppPaths.TempLyricsDir, videoId + ".txt"),
                    Path.Combine(AppPaths.TempLyricsDir, videoId + ".none"),
                    Path.Combine(AppPaths.TempLyricsDir, videoId + "_romaji.json")
                };
                foreach (var p in paths)
                {
                    try { if (File.Exists(p)) File.Delete(p); } catch { }
                }
            }
            catch { }
        }

        public void CleanCorruptedTestCache()
        {
            try
            {
                string[] dirs = new string[] { AppPaths.SavedLyricsDir, AppPaths.TempLyricsDir };
                foreach (var dir in dirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    var files = Directory.GetFiles(dir, "*.*");
                    foreach (var file in files)
                    {
                        try
                        {
                            string ext = Path.GetExtension(file).ToLowerInvariant();
                            if (ext == ".lrc" || ext == ".txt")
                            {
                                string text = File.ReadAllText(file, Encoding.UTF8);
                                if (IsCorruptedTestLyrics(text))
                                {
                                    string baseName = Path.GetFileNameWithoutExtension(file);
                                    PurgeDiskCache(baseName);
                                }
                            }
                            else if (file.EndsWith("_romaji.json", StringComparison.OrdinalIgnoreCase))
                            {
                                string text = File.ReadAllText(file, Encoding.UTF8);
                                if (text.Contains("Chotto naite iru dakeda yo") || text.Contains("ちょっと泣いているだけだよ"))
                                {
                                    string baseName = Path.GetFileName(file).Replace("_romaji.json", "");
                                    PurgeDiskCache(baseName);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public async Task<LyricsData> FetchLyricsAsync(string videoId, string title, string artist, int durationSeconds, bool forceRefresh = false)
        {
            videoId = (videoId ?? string.Empty).Trim();
            title = (title ?? string.Empty).Trim();
            artist = (artist ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(videoId) && string.IsNullOrWhiteSpace(title))
            {
                return LyricsData.Empty(videoId);
            }

            AppPaths.EnsureDirectoriesExist();

            // Force refresh: clear any cached files for this video to ensure a 100% clean fetch
            if (forceRefresh && !string.IsNullOrEmpty(videoId))
            {
                PurgeDiskCache(videoId);
            }

            // 0. Check Saved/lyrics/ (Highest priority for Downloaded tracks: 0ms offline support)
            if (!string.IsNullOrEmpty(videoId))
            {
                string savedLrc = Path.Combine(AppPaths.SavedLyricsDir, videoId + ".lrc");
                if (File.Exists(savedLrc))
                {
                    try
                    {
                        string lrcContent = File.ReadAllText(savedLrc, Encoding.UTF8);
                        if (IsCorruptedTestLyrics(lrcContent))
                        {
                            PurgeDiskCache(videoId);
                        }
                        else
                        {
                            var cachedData = ParseLrc(lrcContent, videoId, title, artist);
                            if (cachedData != null && cachedData.HasLyrics)
                            {
                                double maxSec = cachedData.Lines != null && cachedData.Lines.Count > 0 ? cachedData.Lines[cachedData.Lines.Count - 1].Timestamp.TotalSeconds : 0;
                                if (durationSeconds > 0 && maxSec > durationSeconds + 12)
                                {
                                    PurgeDiskCache(videoId);
                                }
                                else
                                {
                                    AttachCachedRomanization(cachedData, videoId);
                                    if (!cachedData.HasRomanization)
                                    {
                                        TriggerBackgroundRomanization(videoId, cachedData);
                                    }
                                    return cachedData;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // Check .none marker (only treat as valid if created within last 30 minutes)
                string noneCache = Path.Combine(AppPaths.TempLyricsDir, videoId + ".none");
                if (!forceRefresh && File.Exists(noneCache))
                {
                    try
                    {
                        DateTime lastWrite = File.GetLastWriteTimeUtc(noneCache);
                        if ((DateTime.UtcNow - lastWrite).TotalMinutes < 30)
                        {
                            return LyricsData.Empty(videoId);
                        }
                    }
                    catch
                    {
                        return LyricsData.Empty(videoId);
                    }
                }

                // 1. Check KahoiiTemp/lyrics/ for cached .lrc
                string lrcCache = Path.Combine(AppPaths.TempLyricsDir, videoId + ".lrc");
                if (File.Exists(lrcCache))
                {
                    try
                    {
                        string lrcContent = File.ReadAllText(lrcCache, Encoding.UTF8);
                        if (IsCorruptedTestLyrics(lrcContent))
                        {
                            PurgeDiskCache(videoId);
                        }
                        else
                        {
                            var cachedData = ParseLrc(lrcContent, videoId, title, artist);
                            if (cachedData != null && cachedData.HasLyrics)
                            {
                                double maxSec = cachedData.Lines != null && cachedData.Lines.Count > 0 ? cachedData.Lines[cachedData.Lines.Count - 1].Timestamp.TotalSeconds : 0;
                                if (durationSeconds > 0 && maxSec > durationSeconds + 12)
                                {
                                    PurgeDiskCache(videoId);
                                }
                                else
                                {
                                    AttachCachedRomanization(cachedData, videoId);
                                    if (!cachedData.HasRomanization)
                                    {
                                        TriggerBackgroundRomanization(videoId, cachedData);
                                    }
                                    return cachedData;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 2. Try LRCLIB API with smart multi-candidate search (Track Name + Duration + Artist)
            // Searching LRCLIB first ensures we get full, complete, vocal-synchronized LRC lyrics!
            LyricsData lrclibResult = await TryFetchFromLrclibAsync(videoId, title, artist, durationSeconds);
            if (lrclibResult != null && lrclibResult.HasLyrics && lrclibResult.IsSynced)
            {
                CacheLyrics(videoId, lrclibResult);
                TriggerBackgroundRomanization(videoId, lrclibResult);
                return lrclibResult;
            }

            // 3. Check existing .txt cache if available before calling web scrapers
            if (!string.IsNullOrEmpty(videoId))
            {
                string savedTxt = Path.Combine(AppPaths.SavedLyricsDir, videoId + ".txt");
                if (File.Exists(savedTxt))
                {
                    try
                    {
                        string txtContent = File.ReadAllText(savedTxt, Encoding.UTF8);
                        if (IsCorruptedTestLyrics(txtContent))
                        {
                            PurgeDiskCache(videoId);
                        }
                        else
                        {
                            var cachedData = ParsePlainText(txtContent, videoId, title, artist);
                            if (cachedData != null && cachedData.HasLyrics)
                            {
                                AttachCachedRomanization(cachedData, videoId);
                                if (!cachedData.HasRomanization)
                                {
                                    TriggerBackgroundRomanization(videoId, cachedData);
                                }
                                return cachedData;
                            }
                        }
                    }
                    catch { }
                }

                string txtCache = Path.Combine(AppPaths.TempLyricsDir, videoId + ".txt");
                if (File.Exists(txtCache))
                {
                    try
                    {
                        string txtContent = File.ReadAllText(txtCache, Encoding.UTF8);
                        if (IsCorruptedTestLyrics(txtContent))
                        {
                            PurgeDiskCache(videoId);
                        }
                        else
                        {
                            var cachedData = ParsePlainText(txtContent, videoId, title, artist);
                            if (cachedData != null && cachedData.HasLyrics)
                            {
                                AttachCachedRomanization(cachedData, videoId);
                                if (!cachedData.HasRomanization)
                                {
                                    TriggerBackgroundRomanization(videoId, cachedData);
                                }
                                return cachedData;
                            }
                        }
                    }
                    catch { }
                }
            }

            // 4. If LRCLIB had unsynced plain lyrics, use it
            if (lrclibResult != null && lrclibResult.HasLyrics)
            {
                CacheLyrics(videoId, lrclibResult);
                TriggerBackgroundRomanization(videoId, lrclibResult);
                return lrclibResult;
            }

            // 5. Try YouTube Music Innertube API (Official lyrics tab from YouTube Music)
            if (!string.IsNullOrEmpty(videoId) && videoId.Length == 11)
            {
                LyricsData ytmResult = await TryFetchFromYouTubeMusicAsync(videoId, title, artist);
                if (ytmResult != null && ytmResult.HasLyrics)
                {
                    CacheLyrics(videoId, ytmResult);
                    TriggerBackgroundRomanization(videoId, ytmResult);
                    return ytmResult;
                }
            }

            // 5b. Search YouTube Music for title + artist to find official track if MV/fan video
            if (!string.IsNullOrWhiteSpace(title))
            {
                try
                {
                    string officialTrackId = await SearchYouTubeMusicTrackIdAsync(title, artist);
                    if (!string.IsNullOrEmpty(officialTrackId) && !string.Equals(officialTrackId, videoId, StringComparison.OrdinalIgnoreCase))
                    {
                        LyricsData ytmSearchResult = await TryFetchFromYouTubeMusicAsync(officialTrackId, title, artist);
                        if (ytmSearchResult != null && ytmSearchResult.HasLyrics)
                        {
                            ytmSearchResult.VideoId = videoId; // Bind to current track's videoId
                            CacheLyrics(videoId, ytmSearchResult);
                            TriggerBackgroundRomanization(videoId, ytmSearchResult);
                            return ytmSearchResult;
                        }
                    }
                }
                catch { }
            }

            // 4. Mark as having no lyrics in cache to avoid repeated failed lookups
            if (!string.IsNullOrEmpty(videoId))
            {
                try
                {
                    string noneCache = Path.Combine(AppPaths.TempLyricsDir, videoId + ".none");
                    File.WriteAllText(noneCache, DateTime.UtcNow.ToString("o"));
                }
                catch { }
            }

            return LyricsData.Empty(videoId);
        }

        public void PrefetchLyricsAsync(string videoId, string title, string artist, int durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return;

            System.Threading.ThreadPool.QueueUserWorkItem(async delegate
            {
                try
                {
                    string savedLrc = Path.Combine(AppPaths.SavedLyricsDir, videoId + ".lrc");
                    string tempLrc = Path.Combine(AppPaths.TempLyricsDir, videoId + ".lrc");
                    string savedTxt = Path.Combine(AppPaths.SavedLyricsDir, videoId + ".txt");
                    string tempTxt = Path.Combine(AppPaths.TempLyricsDir, videoId + ".txt");

                    if (File.Exists(savedLrc) || File.Exists(tempLrc) || File.Exists(savedTxt) || File.Exists(tempTxt))
                    {
                        return; // Already cached
                    }

                    await FetchLyricsAsync(videoId, title, artist, durationSeconds);
                }
                catch { }
            });
        }

        private void TriggerBackgroundRomanization(string videoId, LyricsData data)
        {
            if (data == null || !data.HasLyrics) return;

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    AttachCachedRomanization(data, videoId);
                    if (!data.HasRomanization)
                    {
                        await FetchRomanizationAsync(data);
                    }
                    if (data.HasRomanization)
                    {
                        Form1.Instance?.NotifyLyricsUpdated(videoId);
                    }
                }
                catch { }
            });
        }

        public static string NormalizeForMatch(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            string lower = s.ToLowerInvariant();
            return Regex.Replace(lower, @"[\s\-_/|–—'""~`\.・,!?\(\)\[\]\{\}【】『』「」]+", "");
        }

        public static bool IsTitleMatch(string targetTitle, string resultTrack, string targetArtist = null, string resultArtist = null, string resultAlbum = null)
        {
            if (string.IsNullOrWhiteSpace(targetTitle) || string.IsNullOrWhiteSpace(resultTrack))
                return false;

            string tTarget = NormalizeForMatch(targetTitle);
            string tTrack = NormalizeForMatch(resultTrack);
            string tAlbum = NormalizeForMatch(resultAlbum);
            string aTarget = NormalizeForMatch(targetArtist);
            string aResult = NormalizeForMatch(resultArtist);

            // MANDATORY ARTIST VERIFICATION:
            // If both target artist and result artist are provided, verify they are compatible!
            if (!string.IsNullOrEmpty(aTarget) && !string.IsNullOrEmpty(aResult))
            {
                if (!AreArtistsSimilar(aTarget, aResult))
                {
                    return false;
                }
            }

            // 1. Exact normalized title match
            if (string.Equals(tTarget, tTrack, StringComparison.OrdinalIgnoreCase))
                return true;

            // 1b. Cleaned title match (ignoring (feat. ...), [MV], etc.)
            string cTarget = NormalizeForMatch(CleanTitleForSearch(targetTitle));
            string cTrack = NormalizeForMatch(CleanTitleForSearch(resultTrack));
            if (!string.IsNullOrEmpty(cTarget) && string.Equals(cTarget, cTrack, StringComparison.OrdinalIgnoreCase))
                return true;

            // 1c. Translation & Romanization match for CJK songs
            if (ContainsCjk(targetTitle))
            {
                var trkRes = TranslateAndRomanizeSync(targetTitle);
                if (trkRes != null)
                {
                    if (!string.IsNullOrEmpty(trkRes.Translation) && IsTitleMatch(trkRes.Translation, resultTrack, targetArtist, resultArtist, resultAlbum))
                        return true;
                    string cleanRom = RemoveDiacritics(trkRes.Romaji);
                    if (!string.IsNullOrEmpty(cleanRom) && IsTitleMatch(cleanRom, resultTrack, targetArtist, resultArtist, resultAlbum))
                        return true;
                }
            }

            // 2. Substring containment
            if (tTarget.Length >= 4 && tTrack.Length >= 4)
            {
                if (tTarget.Contains(tTrack) || tTrack.Contains(tTarget))
                {
                    double ratio = (double)Math.Min(tTarget.Length, tTrack.Length) / Math.Max(tTarget.Length, tTrack.Length);
                    if (ratio >= 0.50) return true;
                }
            }

            // 3. Album match (especially for anime / video game soundtrack releases)
            if (!string.IsNullOrEmpty(tAlbum) && (string.Equals(tTarget, tAlbum, StringComparison.OrdinalIgnoreCase) || (tTarget.Length >= 4 && tAlbum.Contains(tTarget))))
            {
                if (string.IsNullOrEmpty(aTarget) || string.IsNullOrEmpty(aResult) || AreArtistsSimilar(aTarget, aResult))
                {
                    return true;
                }
            }

            // 4. Token overlap of meaningful words
            var w1 = new List<string>();
            foreach (Match m in Regex.Matches(targetTitle.ToLowerInvariant(), @"\b[\p{L}\p{N}]{2,}\b"))
            {
                string w = m.Value;
                if (!IsCommonStopWord(w)) w1.Add(w);
            }

            var w2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(resultTrack.ToLowerInvariant(), @"\b[\p{L}\p{N}]{2,}\b"))
            {
                string w = m.Value;
                if (!IsCommonStopWord(w)) w2.Add(w);
            }

            if (w1.Count > 0 && w2.Count > 0)
            {
                int matchCount = 0;
                foreach (var w in w1)
                {
                    if (w2.Contains(w)) matchCount++;
                }

                double overlap = (double)matchCount / Math.Min(w1.Count, w2.Count);
                if (overlap >= 0.55)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool AreArtistsSimilar(string a1, string a2)
        {
            if (string.IsNullOrWhiteSpace(a1) || string.IsNullOrWhiteSpace(a2)) return true;

            string n1 = NormalizeForMatch(a1);
            string n2 = NormalizeForMatch(a2);
            if (string.Equals(n1, n2, StringComparison.OrdinalIgnoreCase)) return true;
            if (n1.Contains(n2) || n2.Contains(n1)) return true;

            // Translation & Romanization check if either artist contains CJK
            if (ContainsCjk(a1))
            {
                var t1 = TranslateAndRomanizeSync(a1);
                if (t1 != null)
                {
                    if (!string.IsNullOrEmpty(t1.Translation) && AreArtistsSimilar(t1.Translation, a2)) return true;
                    string cleanRom = RemoveDiacritics(t1.Romaji);
                    if (!string.IsNullOrEmpty(cleanRom) && AreArtistsSimilar(cleanRom, a2)) return true;
                }
            }
            if (ContainsCjk(a2))
            {
                var t2 = TranslateAndRomanizeSync(a2);
                if (t2 != null)
                {
                    if (!string.IsNullOrEmpty(t2.Translation) && AreArtistsSimilar(a1, t2.Translation)) return true;
                    string cleanRom = RemoveDiacritics(t2.Romaji);
                    if (!string.IsNullOrEmpty(cleanRom) && AreArtistsSimilar(a1, cleanRom)) return true;
                }
            }

            // Token overlap of words with >= 2 characters
            var w1 = Regex.Matches(a1.ToLowerInvariant(), @"\b[\p{L}\p{N}]{2,}\b");
            var w2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(a2.ToLowerInvariant(), @"\b[\p{L}\p{N}]{2,}\b"))
            {
                w2.Add(m.Value);
            }

            foreach (Match m in w1)
            {
                if (w2.Contains(m.Value)) return true;
            }

            return false;
        }

        private static bool IsCommonStopWord(string word)
        {
            switch (word)
            {
                case "the": case "a": case "an": case "and": case "or": case "in":
                case "on": case "at": case "to": case "for": case "of": case "with":
                case "by": case "from": case "is": case "it": case "no": case "ga":
                case "wa": case "wo": case "ni": case "de": case "va": case "la":
                    return true;
                default:
                    return false;
            }
        }

        private class SearchCandidate
        {
            public string Track { get; set; }
            public string Artist { get; set; }
            public SearchCandidate(string track, string artist)
            {
                Track = track ?? string.Empty;
                Artist = artist ?? string.Empty;
            }
        }

        private List<SearchCandidate> BuildSearchCandidates(string title, string artist)
        {
            var list = new List<SearchCandidate>();
            if (string.IsNullOrWhiteSpace(title)) return list;

            // Clean brackets, hashtags and metadata
            string cleanT = CleanTitleForSearch(title);
            string cleanA = CleanArtistForSearch(artist);

            // Check for #Artist hashtag
            var mHash = Regex.Match(title, @"#(\w+)");
            if (mHash.Success && string.IsNullOrEmpty(cleanA))
            {
                cleanA = CleanArtistForSearch(mHash.Groups[1].Value.Trim());
            }

            // 1. Japanese quotes:
            // "「Track/Artist」"
            var mSlash = Regex.Match(title, @"^[\u300C\u300E\u3010]([^/\u300D\u300F\u3011]+)/([^/\u300D\u300F\u3011]+)[\u300D\u300F\u3011]");
            if (mSlash.Success)
            {
                list.Add(new SearchCandidate(mSlash.Groups[1].Value.Trim(), mSlash.Groups[2].Value.Trim()));
                list.Add(new SearchCandidate(mSlash.Groups[2].Value.Trim(), mSlash.Groups[1].Value.Trim()));
            }

            // "Artist「Track」" or "Track「Artist」"
            var mJp = Regex.Match(title, @"([^\s\u300C\u300E\u3010]+)\s*[\u300C\u300E\u3010]([^\u300D\u300F\u3011]+)[\u300D\u300F\u3011]");
            if (mJp.Success)
            {
                list.Add(new SearchCandidate(mJp.Groups[2].Value.Trim(), mJp.Groups[1].Value.Trim()));
                list.Add(new SearchCandidate(mJp.Groups[1].Value.Trim(), mJp.Groups[2].Value.Trim()));
            }

            // "「Track」" anywhere in string
            var mJustTrack = Regex.Match(title, @"[\u300C\u300E\u3010]([^\u300D\u300F\u3011]+)[\u300D\u300F\u3011]");
            if (mJustTrack.Success)
            {
                string inner = mJustTrack.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(inner))
                {
                    if (inner.Contains("/"))
                    {
                        string[] p = inner.Split(new char[] { '/' }, 2);
                        list.Add(new SearchCandidate(p[0].Trim(), p[1].Trim()));
                        list.Add(new SearchCandidate(p[1].Trim(), p[0].Trim()));
                    }
                    else
                    {
                        list.Insert(0, new SearchCandidate(inner, cleanA));
                    }
                }
            }

            // 2. Delimiters: " | ", " - ", " / ", " – ", " — "
            string[] separators = new string[] { " | ", " - ", " / ", " – ", " — " };
            foreach (var sep in separators)
            {
                if (cleanT.Contains(sep))
                {
                    string[] parts = cleanT.Split(new string[] { sep }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string p0 = parts[0].Trim();
                        string p1 = parts[1].Trim();

                        // If p0 matches artist, then p1 is definitely track title!
                        if (!string.IsNullOrEmpty(cleanA) && AreArtistsSimilar(p0, cleanA))
                        {
                            list.Insert(0, new SearchCandidate(p1, cleanA));
                            list.Insert(1, new SearchCandidate(p1, p0));
                        }
                        // If p1 matches artist, then p0 is definitely track title!
                        else if (!string.IsNullOrEmpty(cleanA) && AreArtistsSimilar(p1, cleanA))
                        {
                            list.Insert(0, new SearchCandidate(p0, cleanA));
                            list.Insert(1, new SearchCandidate(p0, p1));
                        }
                        else
                        {
                            list.Add(new SearchCandidate(p0, p1));
                            list.Add(new SearchCandidate(p1, p0));
                            if (!string.IsNullOrEmpty(cleanA))
                            {
                                list.Add(new SearchCandidate(p0, cleanA));
                                list.Add(new SearchCandidate(p1, cleanA));
                            }
                        }
                    }
                    break;
                }
            }

            // 3. Featured artist detection (feat. / ft.)
            var mFeat = Regex.Match(title, @"(?:feat\.|ft\.|featuring)\s*([^/|()\[\]–—]+)", RegexOptions.IgnoreCase);
            if (mFeat.Success)
            {
                string featArtist = mFeat.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(featArtist))
                {
                    string baseT = !string.IsNullOrEmpty(cleanT) ? cleanT : title;
                    if (!string.IsNullOrEmpty(cleanA))
                    {
                        list.Add(new SearchCandidate(baseT, $"{cleanA} feat. {featArtist}"));
                        list.Add(new SearchCandidate(baseT, $"{cleanA} & {featArtist}"));
                    }
                    list.Add(new SearchCandidate(baseT, featArtist));
                }
            }

            // 4. Clean track with clean artist
            if (!string.IsNullOrEmpty(cleanT))
            {
                if (!string.IsNullOrEmpty(cleanA))
                {
                    // If artist is inside clean title, prioritize title without artist!
                    if (cleanT.ToLower().Contains(cleanA.ToLower()))
                    {
                        string withoutA = Regex.Replace(cleanT, Regex.Escape(cleanA), "", RegexOptions.IgnoreCase).Trim(" -/|–—_".ToCharArray()).Trim();
                        if (!string.IsNullOrEmpty(withoutA))
                        {
                            list.Insert(0, new SearchCandidate(withoutA, cleanA));
                        }
                        else
                        {
                            list.Insert(0, new SearchCandidate(cleanT, cleanA));
                        }
                    }
                    else
                    {
                        list.Insert(0, new SearchCandidate(cleanT, cleanA));
                    }
                }
                list.Add(new SearchCandidate(cleanT, ""));
            }

            // Deduplicate while prioritizing candidates WITH Artist
            var unique = new List<SearchCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Pass 1: candidates that have BOTH Track and Artist
            foreach (var c in list)
            {
                string t = Regex.Replace(c.Track ?? "", @"\s+", " ").Trim();
                string a = Regex.Replace(c.Artist ?? "", @"\s+", " ").Trim();
                if (string.IsNullOrEmpty(t) || string.IsNullOrEmpty(a)) continue;
                string key = t + "||" + a;
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    unique.Add(new SearchCandidate(t, a));
                }
            }

            // Pass 2: candidates without Artist
            foreach (var c in list)
            {
                string t = Regex.Replace(c.Track ?? "", @"\s+", " ").Trim();
                string a = Regex.Replace(c.Artist ?? "", @"\s+", " ").Trim();
                if (string.IsNullOrEmpty(t)) continue;
                string key = t + "||" + a;
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    unique.Add(new SearchCandidate(t, a));
                }
            }

            return unique;
        }

        private async Task<LyricsData> TryFetchFromLrclibAsync(string videoId, string title, string artist, int durationSeconds)
        {
            try
            {
                var rawCandidates = BuildSearchCandidates(title, artist);
                if (rawCandidates.Count == 0) return null;

                var candidates = new List<SearchCandidate>();
                if (rawCandidates.Count > 0) candidates.Add(rawCandidates[0]);

                string cleanA = CleanArtistForSearch(artist);

                // Enrich candidates with English translation & Romaji/Pinyin for CJK titles
                var tracksWithCjk = rawCandidates.Select(c => c.Track).Where(ContainsCjk).Distinct().Take(2).ToList();
                foreach (var trk in tracksWithCjk)
                {
                    var trkRes = TranslateAndRomanizeSync(trk);
                    if (trkRes != null)
                    {
                        string trans = trkRes.Translation;
                        string cleanRom = RemoveDiacritics(trkRes.Romaji);

                        if (!string.IsNullOrWhiteSpace(trans) && !string.Equals(trans, trk, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(new SearchCandidate(trans, cleanA));
                            var otherArtist = rawCandidates.FirstOrDefault(c => !string.IsNullOrEmpty(c.Artist) && c.Artist != cleanA)?.Artist;
                            if (!string.IsNullOrEmpty(otherArtist))
                            {
                                candidates.Add(new SearchCandidate(trans, otherArtist));
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(cleanRom) && !string.Equals(cleanRom, trk, StringComparison.OrdinalIgnoreCase) && !string.Equals(cleanRom, trans, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(new SearchCandidate(cleanRom, cleanA));
                        }
                    }
                }

                // If artist has CJK, translate artist too
                if (ContainsCjk(cleanA))
                {
                    var artRes = TranslateAndRomanizeSync(cleanA);
                    if (artRes != null && !string.IsNullOrWhiteSpace(artRes.Translation) && !string.Equals(artRes.Translation, cleanA, StringComparison.OrdinalIgnoreCase))
                    {
                        string artTrans = artRes.Translation;
                        var currentCands = candidates.ToList();
                        foreach (var cand in currentCands)
                        {
                            candidates.Add(new SearchCandidate(cand.Track, artTrans));
                        }
                    }
                }

                // Append remaining raw candidates
                foreach (var rc in rawCandidates)
                {
                    if (!candidates.Any(c => string.Equals(c.Track, rc.Track, StringComparison.OrdinalIgnoreCase) && string.Equals(c.Artist, rc.Artist, StringComparison.OrdinalIgnoreCase)))
                    {
                        candidates.Add(rc);
                    }
                }

                Dictionary<string, object> bestSyncedMatch = null;
                Dictionary<string, object> bestPlainMatch = null;

                // Take top 8 most promising candidates
                int candLimit = Math.Min(8, candidates.Count);
                for (int ci = 0; ci < candLimit; ci++)
                {
                    var cand = candidates[ci];

                    // Step 1: Try /api/get with exact duration if artist is provided
                    if (durationSeconds > 0 && !string.IsNullOrWhiteSpace(cand.Artist))
                    {
                        try
                        {
                            string getUrl = "https://lrclib.net/api/get?track_name=" + Uri.EscapeDataString(cand.Track) +
                                            "&artist_name=" + Uri.EscapeDataString(cand.Artist) +
                                            "&duration=" + durationSeconds;
                            var req = new HttpRequestMessage(HttpMethod.Get, getUrl);
                            req.Headers.Add("User-Agent", "KahoiiMusic/2.0 (https://github.com/kahoii/music)");

                            var resp = await _httpClient.SendAsync(req);
                            if (resp.IsSuccessStatusCode)
                            {
                                string json = await resp.Content.ReadAsStringAsync();
                                var item = _serializer.Deserialize<Dictionary<string, object>>(json);
                                if (item != null)
                                {
                                    bool isInst = item.ContainsKey("instrumental") && item["instrumental"] is bool bi && bi;
                                    if (!isInst)
                                    {
                                        string synced = item.ContainsKey("syncedLyrics") ? item["syncedLyrics"] as string : null;
                                        string plain = item.ContainsKey("plainLyrics") ? item["plainLyrics"] as string : null;
                                        if (!string.IsNullOrWhiteSpace(synced))
                                        {
                                            var data = ParseLrc(synced, videoId, title, artist);
                                            if (data != null && data.HasLyrics) return data;
                                        }
                                        else if (!string.IsNullOrWhiteSpace(plain) && bestPlainMatch == null)
                                        {
                                            bestPlainMatch = item;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    // Step 2: Try /api/search?track_name=...
                    try
                    {
                        string searchUrl = "https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(cand.Track);
                        if (!string.IsNullOrWhiteSpace(cand.Artist))
                        {
                            searchUrl += "&artist_name=" + Uri.EscapeDataString(cand.Artist);
                        }
                        var req = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                        req.Headers.Add("User-Agent", "KahoiiMusic/2.0 (https://github.com/kahoii/music)");

                        var resp = await _httpClient.SendAsync(req);
                        if (resp.IsSuccessStatusCode)
                        {
                            string json = await resp.Content.ReadAsStringAsync();
                            var list = _serializer.Deserialize<List<Dictionary<string, object>>>(json);
                            if (list != null && list.Count > 0)
                            {
                                foreach (var item in list)
                                {
                                    if (item == null) continue;
                                    bool isInst = item.ContainsKey("instrumental") && item["instrumental"] is bool bi && bi;
                                    if (isInst) continue;

                                    bool hasSynced = item.ContainsKey("syncedLyrics") && item["syncedLyrics"] is string s && !string.IsNullOrWhiteSpace(s);
                                    bool hasPlain = item.ContainsKey("plainLyrics") && item["plainLyrics"] is string p && !string.IsNullOrWhiteSpace(p);
                                    if (!hasSynced && !hasPlain) continue;

                                    string itemTrack = item.ContainsKey("trackName") ? item["trackName"] as string : null;
                                    string itemArtist = item.ContainsKey("artistName") ? item["artistName"] as string : null;
                                    string itemAlbum = item.ContainsKey("albumName") ? item["albumName"] as string : null;

                                    string effectiveTargetArtist = !string.IsNullOrWhiteSpace(artist) ? artist : cand.Artist;
                                    // MANDATORY: Verify title matches to prevent grabbing wrong songs!
                                    if (!IsTitleMatch(cand.Track, itemTrack, effectiveTargetArtist, itemArtist, itemAlbum))
                                    {
                                        continue;
                                    }

                                    int itemDur = 0;
                                    if (item.ContainsKey("duration") && item["duration"] != null)
                                    {
                                        double.TryParse(item["duration"].ToString(), out double d);
                                        itemDur = (int)Math.Round(d);
                                    }

                                    // Duration safeguard: Reject if difference is greater than 8 seconds
                                    if (durationSeconds > 0 && itemDur > 0 && Math.Abs(itemDur - durationSeconds) > 8)
                                    {
                                        continue;
                                    }

                                    if (hasSynced)
                                    {
                                        if (durationSeconds <= 0 || itemDur <= 0 || Math.Abs(itemDur - durationSeconds) <= 6)
                                        {
                                            string synced = item["syncedLyrics"] as string;
                                            var data = ParseLrc(synced, videoId, title, artist);
                                            if (data != null && data.HasLyrics) return data;
                                        }
                                        if (bestSyncedMatch == null) bestSyncedMatch = item;
                                    }
                                    else if (bestPlainMatch == null)
                                    {
                                        bestPlainMatch = item;
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    // Step 3: Try /api/search?q=...
                    try
                    {
                        string q = (cand.Track + " " + cand.Artist).Trim();
                        string qUrl = "https://lrclib.net/api/search?q=" + Uri.EscapeDataString(q);
                        var req = new HttpRequestMessage(HttpMethod.Get, qUrl);
                        req.Headers.Add("User-Agent", "KahoiiMusic/2.0 (https://github.com/kahoii/music)");

                        var resp = await _httpClient.SendAsync(req);
                        if (resp.IsSuccessStatusCode)
                        {
                            string json = await resp.Content.ReadAsStringAsync();
                            var list = _serializer.Deserialize<List<Dictionary<string, object>>>(json);
                            if (list != null && list.Count > 0)
                            {
                                foreach (var item in list)
                                {
                                    if (item == null) continue;
                                    bool isInst = item.ContainsKey("instrumental") && item["instrumental"] is bool bi && bi;
                                    if (isInst) continue;

                                    bool hasSynced = item.ContainsKey("syncedLyrics") && item["syncedLyrics"] is string s && !string.IsNullOrWhiteSpace(s);
                                    bool hasPlain = item.ContainsKey("plainLyrics") && item["plainLyrics"] is string p && !string.IsNullOrWhiteSpace(p);
                                    if (!hasSynced && !hasPlain) continue;

                                    string itemTrack = item.ContainsKey("trackName") ? item["trackName"] as string : null;
                                    string itemArtist = item.ContainsKey("artistName") ? item["artistName"] as string : null;
                                    string itemAlbum = item.ContainsKey("albumName") ? item["albumName"] as string : null;

                                    string effectiveTargetArtist = !string.IsNullOrWhiteSpace(artist) ? artist : cand.Artist;
                                    // MANDATORY: Verify title matches to prevent grabbing wrong songs!
                                    if (!IsTitleMatch(cand.Track, itemTrack, effectiveTargetArtist, itemArtist, itemAlbum))
                                    {
                                        continue;
                                    }

                                    int itemDur = 0;
                                    if (item.ContainsKey("duration") && item["duration"] != null)
                                    {
                                        double.TryParse(item["duration"].ToString(), out double d);
                                        itemDur = (int)Math.Round(d);
                                    }

                                    // Duration safeguard: Reject if difference is greater than 8 seconds
                                    if (durationSeconds > 0 && itemDur > 0 && Math.Abs(itemDur - durationSeconds) > 8)
                                    {
                                        continue;
                                    }

                                    if (hasSynced)
                                    {
                                        if (durationSeconds <= 0 || itemDur <= 0 || Math.Abs(itemDur - durationSeconds) <= 6)
                                        {
                                            string synced = item["syncedLyrics"] as string;
                                            var data = ParseLrc(synced, videoId, title, artist);
                                            if (data != null && data.HasLyrics) return data;
                                        }
                                        if (bestSyncedMatch == null) bestSyncedMatch = item;
                                    }
                                    else if (bestPlainMatch == null)
                                    {
                                        bestPlainMatch = item;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // Fallback: If any strictly validated synced match within 6 seconds exists
                if (bestSyncedMatch != null)
                {
                    int itemDur = 0;
                    if (bestSyncedMatch.ContainsKey("duration") && bestSyncedMatch["duration"] != null)
                    {
                        double.TryParse(bestSyncedMatch["duration"].ToString(), out double d);
                        itemDur = (int)Math.Round(d);
                    }
                    if (durationSeconds <= 0 || itemDur <= 0 || Math.Abs(itemDur - durationSeconds) <= 6)
                    {
                        string synced = bestSyncedMatch["syncedLyrics"] as string;
                        var data = ParseLrc(synced, videoId, title, artist);
                        if (data != null && data.HasLyrics) return data;
                    }
                }

                // If only validated plain lyrics from LRCLIB:
                if (bestPlainMatch != null)
                {
                    string plain = bestPlainMatch["plainLyrics"] as string;
                    var data = ParsePlainText(plain, videoId, title, artist);
                    if (data != null && data.HasLyrics) return data;
                }
            }
            catch { }

            return null;
        }

        private async Task<LyricsData> TryFetchFromYouTubeMusicAsync(string videoId, string title, string artist, int durationSeconds = 0)
        {
            try
            {
                string nextUrl = "https://music.youtube.com/youtubei/v1/next";
                string payloadNext = "{\"context\":{\"client\":{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20230515.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}},\"videoId\":\"" + videoId + "\"}";

                var contentNext = new StringContent(payloadNext, Encoding.UTF8, "application/json");
                var reqNext = new HttpRequestMessage(HttpMethod.Post, nextUrl) { Content = contentNext };
                reqNext.Headers.Add("Origin", "https://music.youtube.com");
                reqNext.Headers.Add("Referer", "https://music.youtube.com/watch?v=" + videoId);

                var respNext = await _httpClient.SendAsync(reqNext);
                if (!respNext.IsSuccessStatusCode) return null;

                string jsonNext = await respNext.Content.ReadAsStringAsync();
                string browseId = ExtractBrowseIdFromWatchNext(jsonNext);
                if (string.IsNullOrEmpty(browseId)) return null;

                string browseUrl = "https://music.youtube.com/youtubei/v1/browse";
                string payloadBrowse = "{\"context\":{\"client\":{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20230515.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}},\"browseId\":\"" + browseId + "\"}";

                var contentBrowse = new StringContent(payloadBrowse, Encoding.UTF8, "application/json");
                var reqBrowse = new HttpRequestMessage(HttpMethod.Post, browseUrl) { Content = contentBrowse };
                reqBrowse.Headers.Add("Origin", "https://music.youtube.com");
                reqBrowse.Headers.Add("Referer", "https://music.youtube.com/watch?v=" + videoId);

                var respBrowse = await _httpClient.SendAsync(reqBrowse);
                if (!respBrowse.IsSuccessStatusCode) return null;

                string jsonBrowse = await respBrowse.Content.ReadAsStringAsync();
                string lyricsText = ExtractLyricsTextFromBrowse(jsonBrowse);

                if (!string.IsNullOrWhiteSpace(lyricsText))
                {
                    return ParsePlainText(lyricsText, videoId, title, artist);
                }
            }
            catch { }

            return null;
        }

        private string ExtractBrowseIdFromWatchNext(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            // 1. YouTube Music Lyrics browse IDs always start with MPLY
            var m = Regex.Match(json, @"""browseId""\s*:\s*""(MPLY[a-zA-Z0-9_\-]+)""");
            if (m.Success) return m.Groups[1].Value;

            // 2. Tab renderer containing MUSIC_PAGE_TYPE_TRACK_LYRICS (browseId comes before pageType)
            m = Regex.Match(json, @"""browseId""\s*:\s*""([^""]+)"".*?""MUSIC_PAGE_TYPE_TRACK_LYRICS""", RegexOptions.Singleline);
            if (m.Success) return m.Groups[1].Value;

            return null;
        }

        private string ExtractLyricsTextFromBrowse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var m = Regex.Match(json, @"""musicDescriptionShelfRenderer"".*?""description""\s*:\s*\{\s*""runs""\s*:\s*\[(.*?)\]\s*\}", RegexOptions.Singleline);
            if (!m.Success)
            {
                // Fallback: direct runs inside musicDescriptionShelfRenderer
                m = Regex.Match(json, @"""musicDescriptionShelfRenderer"".*?""runs""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                if (!m.Success) return null;
            }

            string runsJson = m.Groups[1].Value;
            var textMatches = Regex.Matches(runsJson, @"""text""\s*:\s*""((?:\\.|[^""])*)""");
            StringBuilder sb = new StringBuilder();
            foreach (Match tm in textMatches)
            {
                sb.Append(Regex.Unescape(tm.Groups[1].Value));
            }
            return sb.ToString().Trim();
        }

        private async Task<string> SearchYouTubeMusicTrackIdAsync(string title, string artist)
        {
            try
            {
                string cleanTitle = CleanTitleForSearch(title);
                string query = cleanTitle;
                if (!string.IsNullOrWhiteSpace(artist) && !cleanTitle.ToLower().Contains(artist.ToLower()))
                {
                    query = cleanTitle + " " + artist;
                }
                query = query.Trim();
                if (string.IsNullOrWhiteSpace(query)) return null;

                string searchUrl = "https://music.youtube.com/youtubei/v1/search";
                string payload = "{\"context\":{\"client\":{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20230515.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}},\"query\":\"" + EscapeJsonString(query) + "\"}";

                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var req = new HttpRequestMessage(HttpMethod.Post, searchUrl) { Content = content };
                req.Headers.Add("Origin", "https://music.youtube.com");

                var resp = await _httpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                string json = await resp.Content.ReadAsStringAsync();
                var m = Regex.Match(json, @"""videoId""\s*:\s*""([a-zA-Z0-9_\-]{11})""");
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }
            catch { }
            return null;
        }

        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }

        public async Task FetchRomanizationAsync(LyricsData data)
        {
            if (data == null || !data.HasLyrics || data.Lines == null || data.Lines.Count == 0) return;

            // 1. Detect language
            StringBuilder fullTextSb = new StringBuilder();
            foreach (var l in data.Lines)
            {
                if (!string.IsNullOrEmpty(l.Text)) fullTextSb.Append(l.Text).Append(" ");
            }
            string combined = fullTextSb.ToString();

            int kanaCount = Regex.Matches(combined, @"[\u3040-\u309F\u30A0-\u30FF]").Count;
            int hanziCount = Regex.Matches(combined, @"[\u4E00-\u9FFF]").Count;

            string lang = null;
            if (kanaCount >= 2)
            {
                lang = "ja";
            }
            else if (hanziCount >= 4)
            {
                lang = "zh-CN";
            }

            data.DetectedLanguage = (lang == "ja" ? "ja" : (lang == "zh-CN" ? "zh" : null));
            if (lang == null) return;

            // 2. Check cache first
            if (!string.IsNullOrEmpty(data.VideoId))
            {
                string savedRomFile = Path.Combine(AppPaths.SavedLyricsDir, data.VideoId + "_romaji.json");
                string tempRomFile = Path.Combine(AppPaths.TempLyricsDir, data.VideoId + "_romaji.json");
                string cacheFile = File.Exists(savedRomFile) ? savedRomFile : (File.Exists(tempRomFile) ? tempRomFile : null);

                if (cacheFile != null)
                {
                    try
                    {
                        string json = File.ReadAllText(cacheFile, Encoding.UTF8);
                        var romList = _serializer.Deserialize<List<string>>(json);
                        if (romList != null && romList.Count == data.Lines.Count)
                        {
                            for (int i = 0; i < data.Lines.Count; i++)
                            {
                                data.Lines[i].RomanizedText = romList[i];
                            }
                            return;
                        }
                    }
                    catch { }
                }
            }

            // 3. Query Google Translate GTX Romanization in batches of ~25 lines to prevent URL length limits
            try
            {
                int batchSize = 25;
                for (int start = 0; start < data.Lines.Count; start += batchSize)
                {
                    int count = Math.Min(batchSize, data.Lines.Count - start);
                    var batchLines = new List<string>();
                    for (int i = 0; i < count; i++)
                    {
                        string t = data.Lines[start + i].Text ?? string.Empty;
                        batchLines.Add(string.IsNullOrWhiteSpace(t) ? "♪" : t.Trim());
                    }

                    string delim = " ~ ";
                    string batchText = string.Join(delim, batchLines);
                    string url = "https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl=" + lang + "&tl=en&dt=rm&q=" + Uri.EscapeDataString(batchText);

                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    var resp = await _httpClient.SendAsync(req);
                    if (resp.IsSuccessStatusCode)
                    {
                        string json = await resp.Content.ReadAsStringAsync();
                        var rawData = _serializer.Deserialize<object[]>(json);
                        if (rawData != null && rawData.Length > 0 && rawData[0] is object[] items && items.Length > 0)
                        {
                            if (items[0] is object[] firstItem && firstItem.Length > 3 && firstItem[3] is string romStr)
                            {
                                string[] parts = romStr.Split(new string[] { "~" }, StringSplitOptions.None);
                                for (int i = 0; i < count && i < parts.Length; i++)
                                {
                                    string r = parts[i].Trim();
                                    if (r != "♪")
                                    {
                                        if (lang == "ja") r = FormatAnimeRomaji(r);
                                        else if (lang == "zh-CN") r = FormatChinesePinyin(r);
                                        data.Lines[start + i].RomanizedText = r;
                                    }
                                }
                            }
                        }
                    }
                }

                // 4. Save to cache
                if (!string.IsNullOrEmpty(data.VideoId) && data.HasRomanization)
                {
                    var romList = new List<string>();
                    foreach (var l in data.Lines)
                    {
                        romList.Add(l.RomanizedText ?? string.Empty);
                    }
                    string json = _serializer.Serialize(romList);
                    string targetFile = Path.Combine(AppPaths.TempLyricsDir, data.VideoId + "_romaji.json");
                    File.WriteAllText(targetFile, json, Encoding.UTF8);

                    if (SavedTracksService.Instance.IsTrackSaved(data.VideoId))
                    {
                        string savedFile = Path.Combine(AppPaths.SavedLyricsDir, data.VideoId + "_romaji.json");
                        File.WriteAllText(savedFile, json, Encoding.UTF8);
                    }
                }
            }
            catch { }
        }

        private void AttachCachedRomanization(LyricsData data, string videoId)
        {
            if (data == null || data.Lines == null || string.IsNullOrEmpty(videoId)) return;
            try
            {
                // Detect language
                StringBuilder fullTextSb = new StringBuilder();
                foreach (var l in data.Lines)
                {
                    if (!string.IsNullOrEmpty(l.Text)) fullTextSb.Append(l.Text).Append(" ");
                }
                string combined = fullTextSb.ToString();

                int kanaCount = Regex.Matches(combined, @"[\u3040-\u309F\u30A0-\u30FF]").Count;
                int hanziCount = Regex.Matches(combined, @"[\u4E00-\u9FFF]").Count;

                if (kanaCount >= 2) data.DetectedLanguage = "ja";
                else if (hanziCount >= 4) data.DetectedLanguage = "zh";

                string savedRom = Path.Combine(AppPaths.SavedLyricsDir, videoId + "_romaji.json");
                string tempRom = Path.Combine(AppPaths.TempLyricsDir, videoId + "_romaji.json");
                string file = File.Exists(savedRom) ? savedRom : (File.Exists(tempRom) ? tempRom : null);
                if (file != null)
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    var list = _serializer.Deserialize<List<string>>(json);
                    if (list != null && list.Count == data.Lines.Count)
                    {
                        for (int i = 0; i < data.Lines.Count; i++)
                        {
                            data.Lines[i].RomanizedText = list[i];
                        }
                    }
                }
            }
            catch { }
        }

        public static LyricsData ParseLrc(string lrcContent, string videoId, string title, string artist)
        {
            if (string.IsNullOrWhiteSpace(lrcContent)) return null;

            var lines = new List<LyricLine>();
            var lineRegex = new Regex(@"\[(\d{1,2}):(\d{1,2})(?:\.(\d{1,3}))?\](.*)", RegexOptions.Compiled);

            using (var reader = new StringReader(lrcContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var m = lineRegex.Match(line);
                    if (m.Success)
                    {
                        int min = int.Parse(m.Groups[1].Value);
                        int sec = int.Parse(m.Groups[2].Value);
                        int ms = 0;
                        if (m.Groups[3].Success)
                        {
                            string msStr = m.Groups[3].Value;
                            if (msStr.Length == 2) ms = int.Parse(msStr) * 10;
                            else if (msStr.Length == 1) ms = int.Parse(msStr) * 100;
                            else if (msStr.Length >= 3) ms = int.Parse(msStr.Substring(0, 3));
                        }

                        TimeSpan ts = new TimeSpan(0, 0, min, sec, ms);
                        string text = m.Groups[4].Value.Trim();
                        lines.Add(new LyricLine(ts, text));
                    }
                }
            }

            if (lines.Count == 0) return null;

            lines.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            return new LyricsData
            {
                VideoId = videoId,
                Title = title,
                Artist = artist,
                HasLyrics = true,
                IsSynced = true,
                Lines = lines,
                PlainText = lrcContent
            };
        }

        public static LyricsData ParsePlainText(string textContent, string videoId, string title, string artist)
        {
            if (string.IsNullOrWhiteSpace(textContent)) return null;

            var lines = new List<LyricLine>();
            using (var reader = new StringReader(textContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        lines.Add(new LyricLine(TimeSpan.Zero, line));
                    }
                }
            }

            if (lines.Count == 0) return null;

            return new LyricsData
            {
                VideoId = videoId,
                Title = title,
                Artist = artist,
                HasLyrics = true,
                IsSynced = false,
                Lines = lines,
                PlainText = textContent
            };
        }

        private void CacheLyrics(string videoId, LyricsData data)
        {
            if (string.IsNullOrEmpty(videoId) || data == null || !data.HasLyrics) return;
            try
            {
                AppPaths.EnsureDirectoriesExist();
                if (data.IsSynced)
                {
                    string lrcPath = Path.Combine(AppPaths.TempLyricsDir, videoId + ".lrc");
                    File.WriteAllText(lrcPath, data.PlainText ?? string.Empty, Encoding.UTF8);

                    if (SavedTracksService.Instance.IsTrackSaved(videoId))
                    {
                        string savedLrc = Path.Combine(AppPaths.SavedLyricsDir, videoId + ".lrc");
                        File.WriteAllText(savedLrc, data.PlainText ?? string.Empty, Encoding.UTF8);
                    }
                }

                // Always save raw text
                string txtPath = Path.Combine(AppPaths.TempLyricsDir, videoId + ".txt");
                File.WriteAllText(txtPath, data.PlainText ?? string.Empty, Encoding.UTF8);

                if (SavedTracksService.Instance.IsTrackSaved(videoId))
                {
                    string savedTxt = Path.Combine(AppPaths.SavedLyricsDir, videoId + ".txt");
                    File.WriteAllText(savedTxt, data.PlainText ?? string.Empty, Encoding.UTF8);
                }
            }
            catch { }
        }

        public static string CleanTitleForSearch(string rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle)) return string.Empty;

            string s = rawTitle;
            // 1. Remove hashtags
            s = Regex.Replace(s, @"#\S+", " ");
            // 2. Remove Japanese cover patterns (歌ってみた, 弾き語り, cover, etc.)
            s = Regex.Replace(s, @"(?:弾き語りで)?歌ってみた(?:のさ)?", " ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\b(?:covered\s+by|cover|acoustic\s+ver|piano\s+ver|full\s+ver|short\s+ver)\b", " ", RegexOptions.IgnoreCase);
            // 3. Remove bracketed metadata: [MV], (Official Audio), 【Lyric Video】, [wuwa 3.7 ost], etc.
            s = Regex.Replace(s, @"\s*[\[\(\{【『「][^\]\)\}】』」]*(?:MV|Official|Video|Audio|Lyric|Vietsub|Full|4K|1080p|HD|Sub|Prod|Visualizer|Teaser|Music Video|MusicVideo|OST|Soundtrack|Theme|Clip|wuwa|graduation)[^\]\)\}】』」]*[\]\)\}】』」]", " ", RegexOptions.IgnoreCase);
            // 4. Remove unbracketed common suffixes: Official Music Video, Official Video, Lyric Video, Music Video, Official Audio, Official Chinese/English Lyrics
            s = Regex.Replace(s, @"\b(?:Official\s+(?:Music\s+)?Video|Official\s+Audio|Lyric\s+Video|Music\s+Video|MV|Audio|Official\s+Chinese\s+Lyrics|Official\s+English\s+Lyrics)\b", " ", RegexOptions.IgnoreCase);
            // 5. Remove featuring
            s = Regex.Replace(s, @"\s*(?:feat\.|ft\.|featuring)\s+[^/| -]+", " ", RegexOptions.IgnoreCase);
            // 6. Remove remaining brackets and quotes
            s = Regex.Replace(s, @"[\[\]\(\)\{\}【】『』「」""'~]", " ");
            // 7. Clean spacing and trim trailing/leading separators
            s = Regex.Replace(s, @"\s+", " ").Trim(" -/|–—_ \t".ToCharArray());

            return s;
        }

        public static string CleanArtistForSearch(string rawArtist)
        {
            if (string.IsNullOrWhiteSpace(rawArtist)) return string.Empty;

            string s = rawArtist;
            // 1. Remove YouTube Topic suffix: "Artist - Topic"
            s = Regex.Replace(s, @"\s*-\s*Topic\b", " ", RegexOptions.IgnoreCase);
            // 2. Remove VEVO, Official Channel, etc.
            s = Regex.Replace(s, @"\b(?:Official\s+(?:Channel|Music|Audio|YouTube)|VEVO|Channel)\b", " ", RegexOptions.IgnoreCase);
            // 3. Remove Japanese common suffixes
            s = Regex.Replace(s, @"(?:公式チャンネル|Official\s*Channel)", " ", RegexOptions.IgnoreCase);
            // 4. Clean brackets
            s = Regex.Replace(s, @"[\[\]\(\)\{\}【】『』「」""'~]", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim(" -/|–—_ \t".ToCharArray());

            return s;
        }

        public static string FormatAnimeRomaji(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string s = text;
            s = s.Replace("ō", "ou").Replace("Ō", "Ou")
                 .Replace("ū", "uu").Replace("Ū", "Uu")
                 .Replace("ā", "aa").Replace("Ā", "Aa")
                 .Replace("ī", "ii").Replace("Ī", "Ii")
                 .Replace("ē", "ee").Replace("Ē", "Ee");

            // Particle 'o' -> 'wo'
            s = Regex.Replace(s, @"\b[Oo]\b", "wo");

            // Fix parenthesis spacing: ( wa ) -> (Wa)
            s = Regex.Replace(s, @"\(\s*wa\s*\)", "(Wa)", RegexOptions.IgnoreCase);

            // Clean spacing
            s = Regex.Replace(s, @"([,!?])([^\s])", "$1 $2");
            s = Regex.Replace(s, @"\s+", " ").Trim();

            if (s.Length > 0 && char.IsLower(s[0]))
            {
                s = char.ToUpper(s[0]) + s.Substring(1);
            }

            return s;
        }

        public static string FormatChinesePinyin(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string s = Regex.Replace(text, @"\s+", " ").Trim();
            if (s.Length > 0 && char.IsLower(s[0]))
            {
                s = char.ToUpper(s[0]) + s.Substring(1);
            }
            return s;
        }
    }
}
