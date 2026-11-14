using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public class PlaylistButton : Control
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _hasPlaylist = false;
        private bool _isOpen = false;

        private Color _iconColor = Color.FromArgb(200, 200, 210);
        private Color _hoverIconColor = Color.White;
        private Color _activeColor = Color.FromArgb(168, 85, 247); // Accent Purple
        private DynamicThemePalette _theme;

        public void SetTheme(DynamicThemePalette theme)
        {
            if (theme != null)
            {
                _theme = theme;
                _activeColor = theme.AccentColor;
                Invalidate();
            }
        }

        [Category("Appearance")]
        public Color ActiveColor
        {
            get => _activeColor;
            set
            {
                if (_activeColor != value)
                {
                    _activeColor = value;
                    Invalidate();
                }
            }
        }

        public PlaylistButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(28, 24);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool HasPlaylist
        {
            get { return _hasPlaylist; }
            set
            {
                if (_hasPlaylist != value)
                {
                    _hasPlaylist = value;
                    Invalidate();
                }
            }
        }

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

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
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
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color activeCol = _theme?.AccentColor ?? _activeColor;

            // Optional hover glass pill background
            if (_isOpen || _isHovered)
            {
                Color bg;
                if (_isOpen)
                {
                    bg = Color.FromArgb(50, activeCol);
                }
                else if (_isPressed)
                {
                    bg = _theme != null ? Color.FromArgb(45, activeCol) : Color.FromArgb(60, 255, 255, 255);
                }
                else
                {
                    bg = _theme != null ? Color.FromArgb(28, activeCol) : Color.FromArgb(30, 255, 255, 255);
                }

                using (SolidBrush b = new SolidBrush(bg))
                using (GraphicsPath p = GetRoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 6))
                {
                    g.FillPath(b, p);
                }
            }

            Color drawColor;
            if (_isOpen)
            {
                drawColor = activeCol;
            }
            else if (_isHovered)
            {
                drawColor = _theme != null ? ColorExtractorService.Lerp(activeCol, Color.White, 0.5f) : _hoverIconColor;
            }
            else
            {
                drawColor = _theme != null ? ColorExtractorService.Lerp(Color.FromArgb(215, 215, 225), activeCol, 0.45f) : _iconColor;
            }

            int barW = 18;
            int barH = 2;
            int spacing = 4;
            int totalBarsH = (3 * barH) + (2 * spacing); // 14px total height
            int startX = (Width - barW) / 2;
            int startY = (Height - totalBarsH) / 2; // Exactly vertically centered

            // Draw 3 clean horizontal menu (hamburger) bars
            using (SolidBrush barBrush = new SolidBrush(drawColor))
            {
                // Top bar
                FillRoundedBar(g, barBrush, startX, startY, barW, barH);
                // Middle bar
                FillRoundedBar(g, barBrush, startX, startY + barH + spacing, barW, barH);
                // Bottom bar
                FillRoundedBar(g, barBrush, startX, startY + (2 * (barH + spacing)), barW, barH);
            }

            // Indicator dot when playlist is loaded - placed as clean top-right badge
            if (_hasPlaylist)
            {
                using (SolidBrush dotBrush = new SolidBrush(activeCol))
                {
                    g.FillEllipse(dotBrush, startX + barW - 1, startY - 2, 4, 4);
                }
            }
        }

        private void FillRoundedBar(Graphics g, Brush brush, int x, int y, int w, int h)
        {
            using (GraphicsPath p = GetRoundedRect(new Rectangle(x, y, w, h), h))
            {
                g.FillPath(brush, p);
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
