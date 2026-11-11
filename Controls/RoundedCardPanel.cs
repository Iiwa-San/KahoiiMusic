using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class RoundedCardPanel : Panel
    {
        private const int WM_ERASEBKGND = 0x0014;

        public int CornerRadius { get; set; } = 14;
        public Color CardBackColor { get; set; } = Color.FromArgb(235, 22, 14, 38);
        public Color CardBorderColor { get; set; } = Color.FromArgb(60, 180, 80, 220);
        public bool BorderGradient { get; set; } = false;
        public Color BorderGradientColor1 { get; set; } = Color.FromArgb(140, 30, 180);
        public Color BorderGradientColor2 { get; set; } = Color.FromArgb(255, 40, 140);

        public RoundedCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = CreateRoundedRect(rect, CornerRadius))
            {
                using (SolidBrush bgBrush = new SolidBrush(CardBackColor))
                {
                    g.FillPath(bgBrush, path);
                }
                if (BorderGradient)
                {
                    using (LinearGradientBrush borderGrad = new LinearGradientBrush(
                        rect,
                        BorderGradientColor1,
                        BorderGradientColor2,
                        LinearGradientMode.ForwardDiagonal))
                    using (Pen pen = new Pen(borderGrad, 1.4f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
                else
                {
                    using (Pen pen = new Pen(CardBorderColor, 1.2f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            base.OnPaint(e);
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

        private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

