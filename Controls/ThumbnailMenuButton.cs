using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class ThumbnailMenuButton : Control
    {
        private const int WM_ERASEBKGND = 0x0014;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isOpen = false;

        public ThumbnailMenuButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(26, 26);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GetRoundedRect(rect, 6))
            {
                // Background
                Color bg = _isOpen || _isPressed
                    ? Color.FromArgb(220, 45, 20, 65)
                    : (_isHovered ? Color.FromArgb(190, 35, 18, 55) : Color.FromArgb(150, 18, 12, 30));
                using (SolidBrush b = new SolidBrush(bg))
                {
                    g.FillPath(b, path);
                }

                // Border
                Color borderColor = _isOpen || _isHovered
                    ? Color.FromArgb(230, 255, 60, 140)
                    : Color.FromArgb(120, 168, 85, 247);
                using (Pen p = new Pen(borderColor, 1.2f))
                {
                    g.DrawPath(p, path);
                }
            }

            Color iconColor = _isOpen || _isHovered ? Color.White : Color.FromArgb(230, 220, 245);

            // Draw 3 horizontal lines and a small play triangle
            // Lines on left: x = 6..13
            using (Pen linePen = new Pen(iconColor, 1.6f))
            {
                linePen.StartCap = LineCap.Round;
                linePen.EndCap = LineCap.Round;
                g.DrawLine(linePen, 6, 8, 13, 8);
                g.DrawLine(linePen, 6, 13, 13, 13);
                g.DrawLine(linePen, 6, 18, 13, 18);
            }

            // Small play triangle on right: x = 16..20, y = 9..17
            using (SolidBrush triBrush = new SolidBrush(iconColor))
            {
                PointF[] tri = new PointF[]
                {
                    new PointF(16f, 9.5f),
                    new PointF(20.5f, 13f),
                    new PointF(16f, 16.5f)
                };
                g.FillPolygon(triBrush, tri);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        private static GraphicsPath GetRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
