using KahoiiMusic.Services;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class ArtworkBox : Control
    {
        private Image _image;
        private bool _isSpinning = false;
        private float _rotationAngle = 0f;
        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private const float DegreesPerSecond = 36f; // Exactly 10 seconds per revolution (smooth, natural, relaxing)
        private System.Windows.Forms.Timer _spinTimer;

        public ArtworkBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(164, 162);
            DoubleBuffered = true;

            _spinTimer = new System.Windows.Forms.Timer();
            _spinTimer.Interval = 16; // ~60 FPS buttery smooth rotation
            _spinTimer.Tick += SpinTimer_Tick;
        }

        [Category("Behavior")]
        public bool IsSpinning
        {
            get { return _isSpinning; }
            set
            {
                if (_isSpinning != value)
                {
                    _isSpinning = value;
                    if (_isSpinning)
                    {
                        if (!_stopwatch.IsRunning) _stopwatch.Start();
                        _spinTimer.Start();
                    }
                    else
                    {
                        if (_stopwatch.IsRunning) _stopwatch.Stop();
                        _spinTimer.Stop();
                    }
                    Invalidate();
                }
            }
        }

        private string _timeText = string.Empty;

        [Category("Appearance")]
        public string TimeText
        {
            get { return _timeText; }
            set
            {
                string val = value ?? string.Empty;
                if (_timeText != val)
                {
                    _timeText = val;
                    // Only explicitly invalidate when not spinning (when spinning, the 60fps timer already renders it)
                    if (!_isSpinning)
                    {
                        Invalidate();
                    }
                }
            }
        }

        [Category("Appearance")]
        public Image Image
        {
            get { return _image; }
            set
            {
                if (_image != value)
                {
                    if (_image != null)
                    {
                        try { _image.Dispose(); } catch { }
                    }
                    _image = value;
                    Invalidate();
                }
            }
        }

        private void SpinTimer_Tick(object sender, EventArgs e)
        {
            if (_isSpinning)
            {
                // Wall-clock continuous rotation: completely immune to timer jitter, drift or delays
                _rotationAngle = (float)((_stopwatch.Elapsed.TotalSeconds * DegreesPerSecond) % 360.0);
                Invalidate();
            }
        }

        public void LoadFromUrlAsync(string urlOrVideoId)
        {
            if (string.IsNullOrWhiteSpace(urlOrVideoId))
            {
                ClearImage();
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // 1. Extract videoId
                    string videoId = null;
                    if (urlOrVideoId.Length == 11 && System.Text.RegularExpressions.Regex.IsMatch(urlOrVideoId, @"^[a-zA-Z0-9_-]{11}$"))
                    {
                        videoId = urlOrVideoId;
                    }
                    else
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(urlOrVideoId, @"(?:vi\/|v=|youtu\.be\/|embed\/)([a-zA-Z0-9_-]{11})");
                        if (m.Success)
                        {
                            videoId = m.Groups[1].Value;
                        }
                    }

                    Bitmap resultThumb = null;

                    // 2. Check Saved/thumb/ (Highest priority: Instant offline playback)
                    if (!string.IsNullOrEmpty(videoId))
                    {
                        string savedThumb = Path.Combine(AppPaths.SavedThumbDir, videoId + ".jpg");
                        resultThumb = TryLoadCircularThumbFromFile(savedThumb);
                    }

                    // 3. Check KahoiiTemp/thumb/ (Cached thumbnails)
                    if (resultThumb == null && !string.IsNullOrEmpty(videoId))
                    {
                        string tempThumb = Path.Combine(AppPaths.TempThumbDir, videoId + ".jpg");
                        resultThumb = TryLoadCircularThumbFromFile(tempThumb);
                    }

                    // 4. Check if urlOrVideoId is an existing direct file path
                    if (resultThumb == null && File.Exists(urlOrVideoId))
                    {
                        resultThumb = TryLoadCircularThumbFromFile(urlOrVideoId);
                    }

                    // If successfully loaded from local disk, apply immediately! (0ms offline visual feedback)
                    if (resultThumb != null)
                    {
                        if (IsHandleCreated)
                        {
                            BeginInvoke(new Action(delegate { Image = resultThumb; }));
                        }
                        else
                        {
                            resultThumb.Dispose();
                        }
                        return;
                    }

                    // 5. If not found on disk, attempt online download from YouTube
                    string downloadUrl = urlOrVideoId;
                    if (!downloadUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !downloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(videoId))
                    {
                        downloadUrl = string.Format("https://img.youtube.com/vi/{0}/hqdefault.jpg", videoId);
                    }

                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            byte[] data = wc.DownloadData(downloadUrl);
                            if (data != null && data.Length > 200)
                            {
                                // Cache thumbnail in TempThumbDir for future offline plays
                                if (!string.IsNullOrEmpty(videoId))
                                {
                                    try
                                    {
                                        string cacheFile = Path.Combine(AppPaths.TempThumbDir, videoId + ".jpg");
                                        File.WriteAllBytes(cacheFile, data);
                                    }
                                    catch { }
                                }

                                using (MemoryStream ms = new MemoryStream(data))
                                using (Image loaded = Image.FromStream(ms))
                                {
                                    resultThumb = CreateCircularVinylArtwork(loaded);
                                }

                                if (resultThumb != null)
                                {
                                    if (IsHandleCreated)
                                    {
                                        BeginInvoke(new Action(delegate { Image = resultThumb; }));
                                    }
                                    else
                                    {
                                        resultThumb.Dispose();
                                    }
                                    return;
                                }
                            }
                        }
                    }
                    catch { }

                    // If all failed (e.g. offline with no cached image), reset image so old track thumbnail does not linger
                    if (IsHandleCreated)
                    {
                        BeginInvoke(new Action(delegate { Image = null; }));
                    }
                }
                catch
                {
                    if (IsHandleCreated)
                    {
                        BeginInvoke(new Action(delegate { Image = null; }));
                    }
                }
            });
        }

        private Bitmap TryLoadCircularThumbFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;
            try
            {
                FileInfo fi = new FileInfo(filePath);
                if (fi.Length > 100)
                {
                    byte[] data = File.ReadAllBytes(filePath);
                    using (MemoryStream ms = new MemoryStream(data))
                    using (Image loaded = Image.FromStream(ms))
                    {
                        return CreateCircularVinylArtwork(loaded);
                    }
                }
            }
            catch { }
            return null;
        }

        private Bitmap CreateCircularVinylArtwork(Image loaded)
        {
            if (loaded == null) return null;
            try
            {
                int thumbDim = 116;
                Bitmap thumb = new Bitmap(thumbDim, thumbDim, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(thumb))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using (GraphicsPath circlePath = new GraphicsPath())
                    {
                        circlePath.AddEllipse(0, 0, thumbDim, thumbDim);
                        g.SetClip(circlePath);

                        int cropSize;
                        if (Math.Abs(loaded.Width - loaded.Height) <= 2 || loaded.Width <= 64)
                        {
                            // Already square or micro-thumbnail: preserve entire image
                            cropSize = Math.Min(loaded.Width, loaded.Height);
                        }
                        else
                        {
                            // Standard 16:9 YouTube thumbnail: crop center 75% focused area
                            cropSize = (int)(Math.Min(loaded.Width, loaded.Height) * 0.75f);
                        }
                        int srcX = (loaded.Width - cropSize) / 2;
                        int srcY = (loaded.Height - cropSize) / 2;
                        Rectangle srcRect = new Rectangle(srcX, srcY, cropSize, cropSize);
                        Rectangle destRect = new Rectangle(0, 0, thumbDim, thumbDim);

                        g.DrawImage(loaded, destRect, srcRect, GraphicsUnit.Pixel);
                    }
                }
                return thumb;
            }
            catch
            {
                return null;
            }
        }

        public void ClearImage()
        {
            IsSpinning = false;
            _stopwatch.Reset();
            _rotationAngle = 0f;
            _timeText = string.Empty;
            if (_image != null)
            {
                try { _image.Dispose(); } catch { }
                _image = null;
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_stopwatch.IsRunning)
                {
                    _stopwatch.Stop();
                }
                if (_spinTimer != null)
                {
                    _spinTimer.Stop();
                    _spinTimer.Dispose();
                    _spinTimer = null;
                }
                if (_image != null)
                {
                    try { _image.Dispose(); } catch { }
                    _image = null;
                }
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int cx = Width / 2;
            int cy = 72;
            int R = 66; // Vinyl record radius (diameter = 132)
            int rLabel = 29; // Center artwork sticker radius (diameter = 58)

            // 1. Soft Ambient Drop Shadow under the vinyl disc
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, cx - R + 2, cy - R + 5, 2 * R, 2 * R);
            }

            // 2. Draw Rotating Vinyl Components (Disc Body, Grooves, Center Artwork)
            GraphicsState state = g.Save();

            if (_rotationAngle != 0f)
            {
                // Rotate disc around its center point
                g.TranslateTransform(cx, cy);
                g.RotateTransform(_rotationAngle);
                g.TranslateTransform(-cx, -cy);
            }

            // Outer vinyl disc body (Deep obsidian black)
            using (SolidBrush discBrush = new SolidBrush(Color.FromArgb(18, 18, 18)))
            {
                g.FillEllipse(discBrush, cx - R, cy - R, 2 * R, 2 * R);
            }

            // Outer bevel rim
            using (Pen rimPen = new Pen(Color.FromArgb(45, 45, 45), 1.5f))
            {
                g.DrawEllipse(rimPen, cx - R, cy - R, 2 * R, 2 * R);
            }

            // High-fidelity sound grooves
            using (Pen groovePen = new Pen(Color.FromArgb(28, 28, 28), 1.0f))
            {
                int[] grooves = new int[] { 60, 55, 50, 45, 40, 35 };
                for (int i = 0; i < grooves.Length; i++)
                {
                    int gr = grooves[i];
                    g.DrawEllipse(groovePen, cx - gr, cy - gr, 2 * gr, 2 * gr);
                }
            }

            // Center Label / Album Artwork
            if (_image != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(_image, cx - rLabel, cy - rLabel, 2 * rLabel, 2 * rLabel);

                // Fine gold/silver border around center artwork
                using (Pen labelBorder = new Pen(Color.FromArgb(100, 255, 255, 255), 1.5f))
                {
                    g.DrawEllipse(labelBorder, cx - rLabel, cy - rLabel, 2 * rLabel, 2 * rLabel);
                }
            }
            else
            {
                // Empty state: Golden-amber vinyl label matching the theme
                using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(43, 16, 127)))
                using (Pen labelBorder = new Pen(Color.FromArgb(255, 38, 128), 1.2f))
                {
                    g.FillEllipse(labelBrush, cx - rLabel, cy - rLabel, 2 * rLabel, 2 * rLabel);
                    g.DrawEllipse(labelBorder, cx - rLabel, cy - rLabel, 2 * rLabel, 2 * rLabel);
                }

                // Decorative inner ring on label
                using (Pen innerRing = new Pen(Color.FromArgb(140, 100, 10), 1.0f))
                {
                    g.DrawEllipse(innerRing, cx - 18, cy - 18, 36, 36);
                }
            }

            // Restore from rotation transform so specular highlights stay fixed
            g.Restore(state);

            // 3. Ambient Specular Sheen (Reflections stay fixed while the vinyl spins underneath)
            using (GraphicsPath sheenPath = new GraphicsPath())
            {
                sheenPath.AddPie(cx - R + 2, cy - R + 2, 2 * R - 4, 2 * R - 4, 35, 30);
                sheenPath.AddPie(cx - R + 2, cy - R + 2, 2 * R - 4, 2 * R - 4, 215, 30);
                using (SolidBrush sheenBrush = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
                {
                    g.FillPath(sheenBrush, sheenPath);
                }
            }

            // 4. Center Spindle Hole (metallic silver ring with dark hole)
            using (SolidBrush holeBrush = new SolidBrush(Color.FromArgb(18, 18, 18)))
            using (Pen silverPen = new Pen(Color.FromArgb(200, 200, 200), 1.2f))
            {
                g.FillEllipse(holeBrush, cx - 4, cy - 4, 8, 8);
                g.DrawEllipse(silverPen, cx - 4, cy - 4, 8, 8);
            }

            // 5. Text below the vinyl disc (Time display or "Chưa phát hiện Audio")
            if (_image == null)
            {
                using (Font textFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (SolidBrush textShadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(235, 235, 235)))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    Rectangle textRectShadow = new Rectangle(0, cy + R + 4, Width, 18);
                    Rectangle textRect = new Rectangle(0, cy + R + 3, Width, 18);
                    g.DrawString("Chưa phát hiện Audio", textFont, textShadow, textRectShadow, sf);
                    g.DrawString("Chưa phát hiện Audio", textFont, textBrush, textRect, sf);
                }
            }
            else
            {
                string displayTime = !string.IsNullOrEmpty(_timeText) ? _timeText : "00:00 / 00:00";
                using (Font textFont = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                using (SolidBrush textShadow = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(240, 240, 240)))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    Rectangle textRectShadow = new Rectangle(0, cy + R + 4, Width, 18);
                    Rectangle textRect = new Rectangle(0, cy + R + 3, Width, 18);
                    g.DrawString(displayTime, textFont, textShadow, textRectShadow, sf);
                    g.DrawString(displayTime, textFont, textBrush, textRect, sf);
                }
            }
        }
    }
}
