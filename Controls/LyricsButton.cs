using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class LyricsButton : Control
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isOpen = false;
        private bool _hasLyrics = false;
        private bool _isLoading = false;

        private Timer _loadingTimer;
        private float _pulseFactor = 0f;
        private bool _pulseIncreasing = true;

        private static Bitmap _iconBmp;
        private static bool _iconLoadedAttempted = false;

        public LyricsButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(28, 24);
            Cursor = Cursors.Default;
            Enabled = false; // Disabled by default until a track with lyrics is loaded

            _loadingTimer = new Timer();
            _loadingTimer.Interval = 35;
            _loadingTimer.Tick += (s, e) =>
            {
                if (_pulseIncreasing)
                {
                    _pulseFactor += 0.05f;
                    if (_pulseFactor >= 1f) { _pulseFactor = 1f; _pulseIncreasing = false; }
                }
                else
                {
                    _pulseFactor -= 0.05f;
                    if (_pulseFactor <= 0f) { _pulseFactor = 0f; _pulseIncreasing = true; }
                }
                Invalidate();
            };
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    if (_isLoading)
                    {
                        Enabled = true;
                        Cursor = Cursors.Hand;
                        if (!_loadingTimer.Enabled) _loadingTimer.Start();
                    }
                    else
                    {
                        if (_loadingTimer.Enabled) _loadingTimer.Stop();
                        Enabled = _hasLyrics;
                        Cursor = _hasLyrics ? Cursors.Hand : Cursors.Default;
                    }
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        public Color ActiveColor { get; set; } = Color.FromArgb(255, 65, 150);

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsOpen
        {
            get { return _isOpen; }
            set
            {
                if (_isOpen != value)
                {
                    _isOpen = value;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool HasLyrics
        {
            get { return _hasLyrics; }
            set
            {
                if (_hasLyrics != value)
                {
                    _hasLyrics = value;
                    Enabled = value;
                    Cursor = value ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (Enabled)
            {
                _isHovered = true;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Enabled && e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPressed)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        private static Bitmap GetIconBitmap()
        {
            if (_iconBmp != null) return _iconBmp;
            if (_iconLoadedAttempted) return null;
            _iconLoadedAttempted = true;

            // 1. Try embedded manifest resource
            try
            {
                var asm = typeof(LyricsButton).Assembly;
                using (var stream = asm.GetManifestResourceStream("KahoiiMusic.Resources.lyrics_icon.png"))
                {
                    if (stream != null)
                    {
                        _iconBmp = new Bitmap(stream);
                        return _iconBmp;
                    }
                }
            }
            catch { }

            // 2. Try application Resources directory
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "lyrics_icon.png");
                if (File.Exists(p))
                {
                    using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        _iconBmp = new Bitmap(fs);
                        return _iconBmp;
                    }
                }
            }
            catch { }

            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int cx = Width / 2;
            int cy = Height / 2;

            // Optional hover / open glass pill background
            if (_isOpen || (_isHovered && Enabled))
            {
                Color bg = _isOpen
                    ? Color.FromArgb(45, ActiveColor)
                    : (_isPressed ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(25, 255, 255, 255));
                using (SolidBrush b = new SolidBrush(bg))
                using (GraphicsPath p = GetRoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 6))
                {
                    g.FillPath(b, p);
                }
            }

            Color iconColor;
            Color glowColor = Color.Empty;

            if (_isLoading)
            {
                // Breathing neon glow while lyrics is loading
                int alpha = (int)(70 + 70 * _pulseFactor);
                iconColor = Color.FromArgb(240, 180, 225);
                glowColor = Color.FromArgb(alpha, ActiveColor);
            }
            else if (!Enabled)
            {
                // Muted/disabled look when track has no lyrics
                iconColor = Color.FromArgb(60, 220, 220, 240);
            }
            else if (_isOpen)
            {
                // Active / Open state: Bright neon color with soft glow
                iconColor = ActiveColor;
                glowColor = Color.FromArgb(120, ActiveColor);
            }
            else if (_isPressed)
            {
                iconColor = Color.FromArgb(200, 200, 215);
            }
            else if (_isHovered)
            {
                iconColor = Color.White;
                glowColor = Color.FromArgb(80, 255, 255, 255);
            }
            else
            {
                // Normal enabled state
                iconColor = Color.FromArgb(220, 220, 235);
            }

            // Draw ambient glow if open or hovered
            if (glowColor != Color.Empty)
            {
                using (GraphicsPath glowPath = new GraphicsPath())
                {
                    glowPath.AddEllipse(cx - 10, cy - 10, 20, 20);
                    using (PathGradientBrush pgb = new PathGradientBrush(glowPath))
                    {
                        pgb.CenterColor = glowColor;
                        pgb.SurroundColors = new Color[] { Color.Transparent };
                        g.FillEllipse(pgb, cx - 11, cy - 11, 22, 22);
                    }
                }
            }

            // Draw Lyrics Icon (Lines + Music Note)
            Bitmap icon = GetIconBitmap();
            int iconSize = 17;
            int ix = (Width - iconSize) / 2;
            int iy = (Height - iconSize) / 2 - (_isOpen ? 1 : 0);

            if (icon != null)
            {
                DrawTintedIcon(g, icon, new Rectangle(ix, iy, iconSize, iconSize), iconColor);
            }
            else
            {
                // Fallback vector drawing if bitmap is unavailable
                DrawFallbackVectorIcon(g, cx, cy, iconColor);
            }

            // If active/open, draw a small neon dot indicator underneath
            if (_isOpen)
            {
                using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(255, 65, 150)))
                {
                    g.FillEllipse(dotBrush, cx - 1.5f, Height - 3.5f, 3f, 3f);
                }
            }
        }

        private void DrawTintedIcon(Graphics g, Bitmap bmp, Rectangle destRect, Color color)
        {
            if (bmp == null) return;

            float r = color.R / 255f;
            float gr = color.G / 255f;
            float b = color.B / 255f;
            float a = color.A / 255f;

            ColorMatrix cm = new ColorMatrix(new float[][]
            {
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { r, gr, b, a, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });

            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                g.DrawImage(bmp, destRect, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
            }
        }

        private void DrawFallbackVectorIcon(Graphics g, int cx, int cy, Color color)
        {
            // 3 lines on the left, musical note on the right
            using (Pen pen = new Pen(color, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                // 3 horizontal bars
                g.DrawLine(pen, cx - 7, cy - 5, cx + 1, cy - 5);
                g.DrawLine(pen, cx - 7, cy,     cx + 1, cy);
                g.DrawLine(pen, cx - 7, cy + 5, cx - 2, cy + 5);

                // Note stem and hook
                g.DrawLine(pen, cx + 5, cy - 6, cx + 5, cy + 3);
                g.DrawLine(pen, cx + 5, cy - 6, cx + 7, cy - 4);
                // Note loop
                g.DrawArc(pen, cx + 1, cy + 1, 4, 4, 0, 360);
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
