using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class TranslucentPillButton : Control
    {
        private bool _isHovered = false;
        private bool _isPressed = false;

        public Color PillColor { get; set; } = Color.FromArgb(170, 18, 12, 30);
        public Color PillHoverColor { get; set; } = Color.FromArgb(230, 48, 25, 70);
        public Color PillBorderColor { get; set; } = Color.FromArgb(120, 255, 60, 150);
        public Color TextColor { get; set; } = Color.FromArgb(245, 240, 255);

        public TranslucentPillButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
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
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _isPressed = true;
            Invalidate();
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
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int r = Math.Min(Height, Width) / 2;
            using (GraphicsPath path = CreatePillPath(rect, r))
            {
                Color bg = _isPressed ? PillHoverColor : (_isHovered ? PillHoverColor : PillColor);
                using (SolidBrush b = new SolidBrush(bg))
                {
                    g.FillPath(b, path);
                }
                Color border = _isHovered ? PillBorderColor : Color.FromArgb(60, 255, 255, 255);
                using (Pen p = new Pen(border, 1.2f))
                {
                    g.DrawPath(p, path);
                }
            }

            if (!string.IsNullOrEmpty(Text))
            {
                using (SolidBrush tb = new SolidBrush(TextColor))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(Text, Font, tb, rect, sf);
                }
            }
        }

        private static GraphicsPath CreatePillPath(Rectangle r, int radius)
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

