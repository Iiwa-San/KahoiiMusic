using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class ThumbnailActionPopup : Control
    {
        private const int WM_ERASEBKGND = 0x0014;
        private int _hoveredIndex = -1;

        public event EventHandler SaveLibraryClicked;
        public event EventHandler DownloadClicked;

        public ThumbnailActionPopup()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(118, 54);
            Visible = false;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newIdx = (e.Y < Height / 2) ? 0 : 1;
            if (newIdx != _hoveredIndex)
            {
                _hoveredIndex = newIdx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                int clickedIdx = (e.Y < Height / 2) ? 0 : 1;
                Visible = false;
                if (clickedIdx == 0)
                {
                    SaveLibraryClicked?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    DownloadClicked?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = CreateRoundedRect(rect, 8))
            {
                // Background dark glass
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(245, 18, 12, 34)))
                {
                    g.FillPath(bg, path);
                }
                // Neon border matching the mockup
                using (Pen borderPen = new Pen(Color.FromArgb(200, 140, 60, 210), 1.2f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            int itemH = Height / 2;
            Rectangle item0 = new Rectangle(2, 2, Width - 4, itemH - 2);
            Rectangle item1 = new Rectangle(2, itemH, Width - 4, itemH - 2);

            // Hover row background
            if (_hoveredIndex == 0)
            {
                using (GraphicsPath ip = CreateRoundedRect(item0, 6))
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(70, 255, 45, 135)))
                {
                    g.FillPath(hb, ip);
                }
            }
            else if (_hoveredIndex == 1)
            {
                using (GraphicsPath ip = CreateRoundedRect(item1, 6))
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(70, 255, 45, 135)))
                {
                    g.FillPath(hb, ip);
                }
            }

            // Divider line
            using (Pen divPen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
            {
                g.DrawLine(divPen, 6, itemH, Width - 6, itemH);
            }

            // Text
            using (Font f = new Font("Segoe UI", 8.25f, FontStyle.Bold))
            using (SolidBrush b0 = new SolidBrush(_hoveredIndex == 0 ? Color.White : Color.FromArgb(235, 230, 245)))
            using (SolidBrush b1 = new SolidBrush(_hoveredIndex == 1 ? Color.White : Color.FromArgb(235, 230, 245)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("Lưu vào Library", f, b0, item0, sf);
                g.DrawString("Tải xuống", f, b1, item1, sf);
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

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
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
