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
            Size = new Size(28, 28);
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

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (!Enabled) return;
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
            if (!Enabled) return;
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

            if (_isPressed && Enabled)
            {
                g.TranslateTransform(0, 1);
            }

            Color iconColor;
            if (!Enabled)
            {
                iconColor = Color.FromArgb(40, 180, 160, 200);
            }
            else if (_isOpen || _isHovered)
            {
                iconColor = Color.FromArgb(255, 60, 160);
            }
            else
            {
                iconColor = Color.FromArgb(235, 0, 130); // Vibrant hot pink matching Figma
            }

            float w = Width;
            float h = Height;
            float padX = 2f;
            float midY = h / 2f;

            // 1. Top full-width horizontal line
            using (Pen linePen = new Pen(iconColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(linePen, padX, 4f, w - padX, 4f);
            }

            // 2. Middle row: Play triangle on left, horizontal line on right
            using (SolidBrush triBrush = new SolidBrush(iconColor))
            {
                PointF[] tri = new PointF[]
                {
                    new PointF(padX, midY - 4f),
                    new PointF(padX + 7.5f, midY),
                    new PointF(padX, midY + 4f)
                };
                g.FillPolygon(triBrush, tri);
            }

            using (Pen midLinePen = new Pen(iconColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(midLinePen, padX + 11f, midY, w - padX, midY);
            }

            // 3. Bottom full-width horizontal line
            using (Pen bottomLinePen = new Pen(iconColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(bottomLinePen, padX, h - 4.5f, w - padX, h - 4.5f);
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

