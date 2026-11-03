using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public class UserAccountButton : Control
    {
        private bool _isHovered = false;
        private bool _isPressed = false;

        private readonly Color _defaultColor = Color.FromArgb(190, 190, 205);
        private readonly Color _hoverColor = Color.White;
        private readonly Color _activeColor = Color.FromArgb(255, 45, 120); // Neon Pink

        public UserAccountButton()
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

            FirebaseAuthService.Instance.AuthStateChanged += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    try { Invoke(new Action(Invalidate)); } catch { }
                }
            };
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

            bool isLoggedIn = FirebaseAuthService.Instance.IsLoggedIn;

            // Hover pill background
            if (_isHovered)
            {
                Color bg = _isPressed
                    ? Color.FromArgb(50, 255, 255, 255)
                    : (isLoggedIn ? Color.FromArgb(40, 255, 45, 120) : Color.FromArgb(25, 255, 255, 255));
                using (SolidBrush b = new SolidBrush(bg))
                using (GraphicsPath p = GetRoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 6))
                {
                    g.FillPath(b, p);
                }
            }

            Color iconColor = isLoggedIn ? _activeColor : (_isHovered ? _hoverColor : _defaultColor);

            // Draw crisp avatar icon: head circle + shoulder curve
            int cx = Width / 2;
            int cy = Height / 2;

            using (Pen pen = new Pen(iconColor, 1.8f))
            using (SolidBrush brush = new SolidBrush(iconColor))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                // Head
                int headR = 3;
                int headY = cy - 4;
                if (isLoggedIn)
                {
                    g.FillEllipse(brush, cx - headR, headY - headR, headR * 2, headR * 2);
                }
                else
                {
                    g.DrawEllipse(pen, cx - headR, headY - headR, headR * 2, headR * 2);
                }

                // Shoulders arc
                int bodyW = 12;
                int bodyH = 7;
                int bodyX = cx - bodyW / 2;
                int bodyY = cy + 1;
                g.DrawArc(pen, bodyX, bodyY, bodyW, bodyH * 2, 195, 150);

                // If logged in, draw a tiny active status dot at top right
                if (isLoggedIn)
                {
                    using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(52, 211, 153))) // Mint green online dot
                    {
                        g.FillEllipse(dotBrush, cx + 5, cy - 7, 4, 4);
                    }
                }
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
