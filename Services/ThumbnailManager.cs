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
        private const int ThumbSize = 26;
        private const int MaxMemoryCache = 35; // Strict LRU limit: 35 * ~2.7KB = ~95KB RAM
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Bitmap> _memCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> _lruOrder = new LinkedList<string>();
        private static readonly HashSet<string> _pendingLoads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

        public static void QueueLoad(string videoId, Action onLoaded)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return;

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
                    if (File.Exists(savedThumbFile))
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
                    if (result == null && File.Exists(tempThumbFile))
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

                    // 3. Download tiny default.jpg (120x90, ~3KB payload) from YouTube
                    if (result == null)
                    {
                        string url = string.Format("https://img.youtube.com/vi/{0}/default.jpg", videoId);
                        using (WebClient wc = new WebClient())
                        {
                            byte[] data = wc.DownloadData(url);
                            if (data != null && data.Length > 200)
                            {
                                using (MemoryStream ms = new MemoryStream(data))
                                using (Image img = Image.FromStream(ms))
                                {
                                    result = ResizeToMicro(img, ThumbSize, ThumbSize);

                                    // Persist full-quality thumbnail on disk for future instant offline loads
                                    if (data != null)
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
