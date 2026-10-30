using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class DownloadButton : Control
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isOpen = false;
        private readonly ToolTip _toolTip;

        private Color _iconColor = Color.FromArgb(200, 200, 210);
        private Color _hoverIconColor = Color.White;
        private Color _activeColor = Color.FromArgb(168, 85, 247);

        public DownloadButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(26, 24);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            _toolTip = new ToolTip();
            _toolTip.ShowAlways = true;
            _toolTip.SetToolTip(this, "Nhạc đã lưu offline (Saved)");
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsOpen
        {
            get => _isOpen;
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
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Hover / Open background pill
            if (_isOpen || _isHovered)
            {
                Color bg = _isOpen
                    ? Color.FromArgb(50, 168, 85, 247)
                    : (_isPressed ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(30, 255, 255, 255));
                using (SolidBrush b = new SolidBrush(bg))
                using (GraphicsPath p = GetRoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 5))
                {
                    g.FillPath(b, p);
                }
            }

            Color drawColor = _isOpen ? _activeColor : (_isHovered ? _hoverIconColor : _iconColor);
            if (_isPressed)
            {
                drawColor = Color.FromArgb(Math.Max(0, drawColor.R - 25),
                                           Math.Max(0, drawColor.G - 25),
                                           Math.Max(0, drawColor.B - 25));
            }

            float cx = Width / 2f;
            float cy = Height / 2f;

            using (Pen pen = new Pen(drawColor, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                // 1. Sleek download arrow
                // Vertical stem
                g.DrawLine(pen, cx, cy - 6.5f, cx, cy + 2.0f);
                // Arrowhead chevron
                g.DrawLine(pen, cx - 3.8f, cy - 1.2f, cx, cy + 2.2f);
                g.DrawLine(pen, cx + 3.8f, cy - 1.2f, cx, cy + 2.2f);

                // 2. Base tray / bracket
                using (GraphicsPath tray = new GraphicsPath())
                {
                    tray.AddLine(cx - 6.5f, cy + 2.5f, cx - 6.5f, cy + 6.0f);
                    tray.AddLine(cx - 6.5f, cy + 6.0f, cx + 6.5f, cy + 6.0f);
                    tray.AddLine(cx + 6.5f, cy + 6.0f, cx + 6.5f, cy + 2.5f);
                    g.DrawPath(pen, tray);
                }
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
