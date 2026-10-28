using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

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

            // Optional hover glass pill background
            if (_isOpen || _isHovered)
            {
                Color bg = _isOpen
                    ? Color.FromArgb(50, 168, 85, 247)
                    : (_isPressed ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(30, 255, 255, 255));
                using (SolidBrush b = new SolidBrush(bg))
                using (GraphicsPath p = GetRoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 6))
                {
                    g.FillPath(b, p);
                }
            }

            Color drawColor = _isOpen ? _activeColor : (_isHovered ? _hoverIconColor : _iconColor);

            int startX = 6;
            int barW = 12;
            int barH = 2;

            // Draw 3 horizontal list bars
            using (SolidBrush barBrush = new SolidBrush(drawColor))
            {
                // Top bar
                FillRoundedBar(g, barBrush, startX, 6, barW, barH);
                // Middle bar
                FillRoundedBar(g, barBrush, startX, 11, barW, barH);
                // Bottom bar
                FillRoundedBar(g, barBrush, startX, 16, barW - 3, barH);

                // Small musical note icon on the right
                int nx = startX + barW + 2;
                int ny = 5;
                // Stem
                g.FillRectangle(barBrush, nx + 3, ny, 2, 9);
                // Flag / Hook
                g.FillRectangle(barBrush, nx + 5, ny, 2, 3);
                // Note head
                g.FillEllipse(barBrush, nx + 1, ny + 7, 4, 3);
            }

            // Indicator dot when playlist is loaded
            if (_hasPlaylist)
            {
                using (SolidBrush dotBrush = new SolidBrush(_activeColor))
                {
                    g.FillEllipse(dotBrush, Width - 6, Height - 6, 4, 4);
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
