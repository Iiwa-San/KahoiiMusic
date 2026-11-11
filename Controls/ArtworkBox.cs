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
        private ArtworkDisplayMode _displayMode = ArtworkDisplayMode.RoundedCard;
        private ContextMenuStrip _contextMenu;
        private ToolStripMenuItem _miVinyl;
        private ToolStripMenuItem _miCard;
        private ToolTip _toolTip;

        public bool EnableModeToggleOnClick { get; set; } = true;
        public bool ShowBottomTimeText { get; set; } = true;

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
            Cursor = Cursors.Hand;

            try
            {
                _displayMode = HistoryService.ArtworkMode;
            }
            catch { }

            _spinTimer = new System.Windows.Forms.Timer();
            _spinTimer.Interval = 16; // ~60 FPS buttery smooth rotation
            _spinTimer.Tick += SpinTimer_Tick;

            InitializeContextMenu();
            UpdateToolTip();

            HistoryService.SettingsChanged += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            DisplayMode = HistoryService.ArtworkMode;
                        }));
                    }
                    catch { }
                }
                else
                {
                    _displayMode = HistoryService.ArtworkMode;
                }
            };
        }

        [Category("Appearance")]
        public ArtworkDisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                if (_displayMode != value)
                {
                    _displayMode = value;
                    UpdateContextMenuCheckedStates();
                    UpdateToolTip();
                    Invalidate();
                }
            }
        }

        public void SetDisplayMode(ArtworkDisplayMode mode)
        {
            DisplayMode = mode;
            try
            {
                HistoryService.ArtworkMode = mode;
            }
            catch { }
        }

        private void InitializeContextMenu()
        {
            _contextMenu = null;
            this.ContextMenuStrip = null;
        }

        private void UpdateContextMenuCheckedStates()
        {
        }

        private void UpdateToolTip()
        {
            if (_toolTip == null)
            {
                _toolTip = new ToolTip { InitialDelay = 500, ReshowDelay = 200 };
            }
            _toolTip.SetToolTip(this, "Ảnh bài hát");
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
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
                    // Invalidate when not spinning OR when in RoundedCard mode (since RoundedCard doesn't rotate via SpinTimer)
                    if (!_isSpinning || _displayMode == ArtworkDisplayMode.RoundedCard)
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
            if (_isSpinning && _displayMode == ArtworkDisplayMode.VinylRecord)
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
                        resultThumb = TryLoadArtworkFromFile(savedThumb);
                    }

                    // 3. Check KahoiiTemp/thumb/ (Cached thumbnails)
                    if (resultThumb == null && !string.IsNullOrEmpty(videoId))
                    {
                        string tempThumb = Path.Combine(AppPaths.TempThumbDir, videoId + ".jpg");
                        resultThumb = TryLoadArtworkFromFile(tempThumb);
                    }

                    // 4. Check if urlOrVideoId is an existing direct file path
                    if (resultThumb == null && File.Exists(urlOrVideoId))
                    {
                        resultThumb = TryLoadArtworkFromFile(urlOrVideoId);
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
                                    resultThumb = CreateArtworkBitmap(loaded);
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

        private Bitmap TryLoadArtworkFromFile(string filePath)
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
                        return CreateArtworkBitmap(loaded);
                    }
                }
            }
            catch { }
            return null;
        }

        private Bitmap CreateArtworkBitmap(Image loaded)
        {
            if (loaded == null) return null;
            try
            {
                int thumbDim = 256;
                Bitmap thumb = new Bitmap(thumbDim, thumbDim, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(thumb))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    int cropSize;
                    if (Math.Abs(loaded.Width - loaded.Height) <= 2 || loaded.Width <= 64)
                    {
                        // Already square or micro-thumbnail: preserve entire image
                        cropSize = Math.Min(loaded.Width, loaded.Height);
                    }
                    else
                    {
                        // Standard 16:9 YouTube thumbnail: crop center 85% focused area
                        cropSize = (int)(Math.Min(loaded.Width, loaded.Height) * 0.85f);
                    }
                    int srcX = (loaded.Width - cropSize) / 2;
                    int srcY = (loaded.Height - cropSize) / 2;
                    Rectangle srcRect = new Rectangle(srcX, srcY, cropSize, cropSize);
                    Rectangle destRect = new Rectangle(0, 0, thumbDim, thumbDim);

                    g.DrawImage(loaded, destRect, srcRect, GraphicsUnit.Pixel);
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
            int textSpace = ShowBottomTimeText ? 22 : 0;
            int availableH = Height - textSpace;
            int R = Math.Max(50, Math.Min(cx - 10, (availableH - 8) / 2));
            int cy = 4 + R;
            int rLabel = (int)(R * 0.44f); // Center artwork sticker radius

            // Permanently rounded card thumbnail (no vinyl record mode)
            if (!ShowBottomTimeText)
            {
                // Maximized template: rounded card fills the full bounds of ArtworkBox neatly
                DrawRoundedCard(g, 0, 0, Width, Height, 14f);
            }
            else
            {
                int cardSize = R * 2;
                int cardX = (Width - cardSize) / 2;
                int cardY = (cy - cardSize / 2);
                float corner = Math.Max(12f, cardSize * 0.10f);
                DrawRoundedCard(g, cardX, cardY, cardSize, cardSize, corner);
            }

            // Text below the artwork (Time display or "Chưa phát hiện Audio")
            if (ShowBottomTimeText)
            {
                DrawBottomTimeText(g, cy + R + 3);
            }

            base.OnPaint(e);
        }

        private void DrawVinylRecord(Graphics g, int cx, int cy, int R, int rLabel)
        {
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
                // Circular clipping for the vinyl center sticker
                using (GraphicsPath clipPath = new GraphicsPath())
                {
                    clipPath.AddEllipse(cx - rLabel, cy - rLabel, 2 * rLabel, 2 * rLabel);
                    Region oldClip = g.Clip;
                    g.SetClip(clipPath, CombineMode.Intersect);
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(_image, cx - rLabel, cy - rLabel, 2 * rLabel, 2 * rLabel);
                    g.Clip = oldClip;
                }

                // Fine border around center artwork
                using (Pen labelBorder = new Pen(Color.FromArgb(100, 255, 255, 255), 1.5f))
                {
                    g.DrawEllipse(labelBorder, cx - rLabel, cy - rLabel, 2 * rLabel, 2 * rLabel);
                }
            }
            else
            {
                // Empty state: Golden-amber vinyl label matching the cyber theme
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
        }

        private void DrawRoundedCard(Graphics g, int cardX, int cardY, int cardW, int cardH, float radius)
        {
            Rectangle cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            // 1. Soft Ambient Drop Shadow under the card (compact mode)
            if (ShowBottomTimeText)
            {
                Rectangle shadowRect = new Rectangle(cardX + 2, cardY + 5, cardW - 4, cardH - 2);
                using (GraphicsPath shadowPath = CreateRoundedRectanglePath(shadowRect, radius))
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }
            }

            // 2. Main Rounded Card Body
            using (GraphicsPath cardPath = CreateRoundedRectanglePath(cardRect, radius))
            {
                if (_image != null)
                {
                    // Draw Album Artwork clipped inside rounded card
                    GraphicsState state = g.Save();
                    Region oldClip = g.Clip;
                    g.SetClip(cardPath, CombineMode.Intersect);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(_image, cardRect);

                    // Subtle top specular reflection gradient (giving it glass / tactile card sheen)
                    Rectangle sheenRect = new Rectangle(cardX, cardY, cardW, cardH / 2);
                    using (LinearGradientBrush sheenBrush = new LinearGradientBrush(
                        sheenRect,
                        Color.FromArgb(36, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255),
                        LinearGradientMode.Vertical))
                    {
                        g.FillRectangle(sheenBrush, sheenRect);
                    }

                    if (!ShowBottomTimeText)
                    {
                        // Dark gradient scrim at the bottom to ensure seekbar & controls pop over bright album art
                        int scrimH = (int)(cardH * 0.44f);
                        Rectangle scrimRect = new Rectangle(cardX, cardY + cardH - scrimH, cardW, scrimH);
                        using (LinearGradientBrush scrimBrush = new LinearGradientBrush(
                            scrimRect,
                            Color.FromArgb(0, 10, 5, 25),
                            Color.FromArgb(170, 10, 5, 25),
                            LinearGradientMode.Vertical))
                        {
                            g.FillRectangle(scrimBrush, scrimRect);
                        }
                    }

                    g.Clip = oldClip;
                    g.Restore(state);
                }
                else
                {
                    // Empty state (no song): 70% opacity background gradient matching user requirement
                    using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                        cardRect,
                        Color.FromArgb(178, 0x22, 0x1A, 0x36),
                        Color.FromArgb(178, 0x14, 0x0F, 0x22),
                        LinearGradientMode.Vertical))
                    {
                        g.FillPath(bgBrush, cardPath);
                    }

                    // Draw stylized vector music note
                    int centerNoteX = cardX + cardW / 2;
                    int centerNoteY = cardY + cardH / 2 - 4;
                    DrawStylizedMusicNote(g, centerNoteX, centerNoteY);
                }

                // 1px Glass border around the card (matching Android: White 22% to White 5%)
                int borderAlpha1 = (_image == null) ? 40 : 56;
                int borderAlpha2 = (_image == null) ? 12 : 16;
                using (LinearGradientBrush borderBrush = new LinearGradientBrush(
                    cardRect,
                    Color.FromArgb(borderAlpha1, 255, 255, 255),
                    Color.FromArgb(borderAlpha2, 255, 255, 255),
                    LinearGradientMode.Vertical))
                using (Pen borderPen = new Pen(borderBrush, 1.2f))
                {
                    g.DrawPath(borderPen, cardPath);
                }
            }
        }

        private void DrawStylizedMusicNote(Graphics g, int cx, int cy)
        {
            // Stylized vector double music note in neon pink (#FF2680)
            using (SolidBrush noteBrush = new SolidBrush(Color.FromArgb(210, 255, 38, 128)))
            using (Pen stemPen = new Pen(Color.FromArgb(210, 255, 38, 128), 3.0f))
            {
                stemPen.StartCap = LineCap.Round;
                stemPen.EndCap = LineCap.Round;

                // Note head 1 (tilted ellipse)
                GraphicsState s1 = g.Save();
                g.TranslateTransform(cx - 16, cy + 12);
                g.RotateTransform(-25f);
                g.FillEllipse(noteBrush, -9, -6, 18, 13);
                g.Restore(s1);

                // Note head 2 (tilted ellipse)
                GraphicsState s2 = g.Save();
                g.TranslateTransform(cx + 10, cy + 6);
                g.RotateTransform(-25f);
                g.FillEllipse(noteBrush, -9, -6, 18, 13);
                g.Restore(s2);

                // Vertical stems
                g.DrawLine(stemPen, cx - 9, cy + 10, cx - 9, cy - 15);
                g.DrawLine(stemPen, cx + 17, cy + 4, cx + 17, cy - 21);

                // Top connecting beam
                using (GraphicsPath beamPath = new GraphicsPath())
                {
                    beamPath.AddPolygon(new PointF[]
                    {
                        new PointF(cx - 10, cy - 11),
                        new PointF(cx + 18, cy - 17),
                        new PointF(cx + 18, cy - 23),
                        new PointF(cx - 10, cy - 17)
                    });
                    g.FillPath(noteBrush, beamPath);
                }
            }

            // "Kahoii Music" subtitle under the note (like Android app)
            using (Font noteFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(140, 255, 255, 255)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                Rectangle textRect = new Rectangle(cx - 50, cy + 28, 100, 16);
                g.DrawString("Kahoii Music", noteFont, textBrush, textRect, sf);
            }
        }

        private void DrawBottomTimeText(Graphics g, int textY)
        {
            if (string.IsNullOrEmpty(_timeText))
            {
                return;
            }

            string displayTime = _timeText;
            using (Font textFont = new Font("Segoe UI", 9.0f, FontStyle.Regular))
            using (SolidBrush textShadow = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(240, 240, 240)))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                Rectangle textRectShadow = new Rectangle(0, textY + 1, Width, 18);
                Rectangle textRect = new Rectangle(0, textY, Width, 18);
                g.DrawString(displayTime, textFont, textShadow, textRectShadow, sf);
                g.DrawString(displayTime, textFont, textBrush, textRect, sf);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;
            RectangleF arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

            // Top-left
            path.AddArc(arc, 180, 90);
            // Top-right
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            // Bottom-right
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            // Bottom-left
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
