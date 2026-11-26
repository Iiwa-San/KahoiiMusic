using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Threading;

namespace KahoiiMusic.Services
{
    public static class ThumbnailManager
    {
        private const int ThumbSize = 48;
        private const int MaxMemoryCache = 45; // Strict LRU limit: 45 * ~9KB = ~400KB RAM
        private const int CardThumbSize = 220;
        private const int MaxCardCache = 48;
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Bitmap> _memCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> _lruOrder = new LinkedList<string>();
        private static readonly HashSet<string> _pendingLoads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Bitmap> _cardCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> _cardLruOrder = new LinkedList<string>();
        private static readonly HashSet<string> _pendingCardLoads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static ThumbnailManager()
        {
            AppPaths.EnsureDirectoriesExist();
        }

        public static Bitmap GetThumbnail(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return null;

            lock (_lock)
            {
                if (_memCache.TryGetValue(videoId, out Bitmap bmp))
                {
                    // Move to end of LRU list
                    _lruOrder.Remove(videoId);
                    _lruOrder.AddLast(videoId);
                    return bmp;
                }
            }

            return null;
        }

        public static void DrawRoundedImage(Graphics g, Image img, Rectangle destRect, GraphicsPath path)
        {
            if (g == null || img == null || path == null || destRect.Width <= 0 || destRect.Height <= 0) return;
            var prevSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (TextureBrush tb = new TextureBrush(img, WrapMode.Clamp))
            {
                float sx = (float)destRect.Width / Math.Max(1, img.Width);
                float sy = (float)destRect.Height / Math.Max(1, img.Height);
                tb.TranslateTransform(destRect.X, destRect.Y);
                tb.ScaleTransform(sx, sy);
                g.FillPath(tb, path);
            }
            g.SmoothingMode = prevSmooth;
        }

        public static Bitmap GetCardThumbnail(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            lock (_lock)
            {
                if (_cardCache.TryGetValue(key, out Bitmap bmp))
                {
                    _cardLruOrder.Remove(key);
                    _cardLruOrder.AddLast(key);
                    return bmp;
                }
            }
            return null;
        }

        public static void QueueLoadCard(string key, string urlOrVideoId, bool isVideoId, Action onLoaded)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(urlOrVideoId)) return;

            lock (_lock)
            {
                if (_cardCache.ContainsKey(key) || _pendingCardLoads.Contains(key)) return;
                _pendingCardLoads.Add(key);
            }

            string url = isVideoId
                ? string.Format("https://img.youtube.com/vi/{0}/hqdefault.jpg", urlOrVideoId)
                : urlOrVideoId;

            ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap result = null;
                try
                {
                    using (WebClient wc = new WebClient())
                    {
                        byte[] data = wc.DownloadData(url);
                        if (data != null && data.Length > 200)
                        {
                            using (MemoryStream ms = new MemoryStream(data))
                            using (Image img = Image.FromStream(ms))
                            {
                                result = ResizeToMicro(img, CardThumbSize, CardThumbSize);
                            }
                        }
                    }
                }
                catch { }

                bool added = false;
                lock (_lock)
                {
                    _pendingCardLoads.Remove(key);
                    if (result != null)
                    {
                        if (_cardCache.ContainsKey(key))
                        {
                            try { result.Dispose(); } catch { }
                        }
                        else
                        {
                            while (_cardCache.Count >= MaxCardCache && _cardLruOrder.Count > 0)
                            {
                                string oldest = _cardLruOrder.First.Value;
                                _cardLruOrder.RemoveFirst();
                                if (_cardCache.TryGetValue(oldest, out Bitmap evictBmp))
                                {
                                    _cardCache.Remove(oldest);
                                    try { evictBmp.Dispose(); } catch { }
                                }
                            }
                            _cardCache[key] = result;
                            _cardLruOrder.AddLast(key);
                            added = true;
                        }
                    }
                }

                if (added && onLoaded != null)
                {
                    try { onLoaded(); } catch { }
                }
            });
        }

        public static void QueueLoad(string videoId, Action onLoaded)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return;
            QueueLoadInternal(videoId, string.Format("https://img.youtube.com/vi/{0}/mqdefault.jpg", videoId), true, onLoaded);
        }

        public static void QueueLoadUrl(string cacheKey, string url, Action onLoaded)
        {
            if (string.IsNullOrWhiteSpace(cacheKey) || string.IsNullOrWhiteSpace(url)) return;
            QueueLoadInternal(cacheKey, url, false, onLoaded);
        }

        private static void QueueLoadInternal(string cacheKey, string url, bool checkLocalFiles, Action onLoaded)
        {
            string videoId = cacheKey;
            lock (_lock)
            {
                if (_memCache.ContainsKey(videoId) || _pendingLoads.Contains(videoId))
                {
                    return;
                }
                _pendingLoads.Add(videoId);
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap result = null;
                try
                {
                    string savedThumbFile = Path.Combine(AppPaths.SavedThumbDir, videoId + ".jpg");
                    string tempThumbFile = Path.Combine(AppPaths.TempThumbDir, videoId + ".jpg");

                    // 1. Check Saved/thumb/ first (Instant local read, 0ms network)
                    if (checkLocalFiles && File.Exists(savedThumbFile))
                    {
                        try
                        {
                            using (FileStream fs = new FileStream(savedThumbFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (Image img = Image.FromStream(fs))
                            {
                                result = ResizeToMicro(img, ThumbSize, ThumbSize);
                            }
                        }
                        catch { }
                    }

                    // 2. Check KahoiiTemp/thumb/ next
                    if (result == null && checkLocalFiles && File.Exists(tempThumbFile))
                    {
                        try
                        {
                            using (FileStream fs = new FileStream(tempThumbFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (Image img = Image.FromStream(fs))
                            {
                                result = ResizeToMicro(img, ThumbSize, ThumbSize);
                            }
                        }
                        catch { }
                    }

                    // 3. Download thumbnail from URL
                    if (result == null)
                    {
                        using (WebClient wc = new WebClient())
                        {
                            byte[] data = wc.DownloadData(url);
                            if (data != null && data.Length > 200)
                            {
                                using (MemoryStream ms = new MemoryStream(data))
                                using (Image img = Image.FromStream(ms))
                                {
                                    result = ResizeToMicro(img, ThumbSize, ThumbSize);

                                    // Persist thumbnail on disk only if no high-res thumbnail already exists
                                    if (checkLocalFiles && data != null && !File.Exists(tempThumbFile))
                                    {
                                        try
                                        {
                                            File.WriteAllBytes(tempThumbFile, data);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                bool added = false;
                lock (_lock)
                {
                    _pendingLoads.Remove(videoId);
                    if (result != null)
                    {
                        if (_memCache.ContainsKey(videoId))
                        {
                            try { result.Dispose(); } catch { }
                            result = null;
                        }
                        else
                        {
                            // Enforce strict LRU cache size
                            while (_memCache.Count >= MaxMemoryCache && _lruOrder.Count > 0)
                            {
                                string oldestKey = _lruOrder.First.Value;
                                _lruOrder.RemoveFirst();
                                if (_memCache.TryGetValue(oldestKey, out Bitmap evictBmp))
                                {
                                    _memCache.Remove(oldestKey);
                                    try { evictBmp.Dispose(); } catch { }
                                }
                            }

                            _memCache[videoId] = result;
                            _lruOrder.AddLast(videoId);
                            added = true;
                        }
                    }
                }

                if (added && onLoaded != null)
                {
                    try { onLoaded(); } catch { }
                }
            });
        }

        public static void ClearMemory()
        {
            lock (_lock)
            {
                foreach (var kvp in _memCache)
                {
                    try { kvp.Value.Dispose(); } catch { }
                }
                _memCache.Clear();
                _lruOrder.Clear();
                _pendingLoads.Clear();
            }
        }

        public static void DeleteThumb(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return;
            try
            {
                lock (_lock)
                {
                    if (_memCache.TryGetValue(videoId, out Bitmap bmp))
                    {
                        _memCache.Remove(videoId);
                        _lruOrder.Remove(videoId);
                        try { bmp.Dispose(); } catch { }
                    }
                }

                // Delete from KahoiiTemp/thumb
                string tempThumb = Path.Combine(AppPaths.TempThumbDir, videoId + ".jpg");
                if (File.Exists(tempThumb))
                {
                    try { File.Delete(tempThumb); } catch { }
                }
            }
            catch { }
        }

        public static void CleanNonHistoryDiskThumbs(HashSet<string> historyIds, bool cleanAll)
        {
            try
            {
                ClearMemory();
                if (!Directory.Exists(AppPaths.TempThumbDir)) return;

                foreach (string file in Directory.GetFiles(AppPaths.TempThumbDir))
                {
                    string id = Path.GetFileNameWithoutExtension(file);
                    if (cleanAll || historyIds == null || !historyIds.Contains(id))
                    {
                        for (int r = 0; r < 3; r++)
                        {
                            try { File.Delete(file); break; } catch { Thread.Sleep(20); }
                        }
                    }
                }
            }
            catch { }
        }

        private static Bitmap ResizeToMicro(Image src, int w, int h)
        {
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.Clear(Color.Black);

                int sw = src.Width;
                int sh = src.Height;
                int cropSize = Math.Min(sw, sh);
                int sx = (sw - cropSize) / 2;
                int sy = (sh - cropSize) / 2;

                g.DrawImage(src, new Rectangle(0, 0, w, h), sx, sy, cropSize, cropSize, GraphicsUnit.Pixel);
            }
            return bmp;
        }
    }
}
