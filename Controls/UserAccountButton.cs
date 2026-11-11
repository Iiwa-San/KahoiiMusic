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
        private readonly Color _accentPink = Color.FromArgb(255, 45, 120); // Neon Pink
        private readonly Color _accentPurple = Color.FromArgb(168, 85, 247); // Neon Purple

        public UserAccountButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(36, 36);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            EventHandler refreshHandler = (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try { BeginInvoke(new Action(Invalidate)); } catch { }
                }
            };

            FirebaseAuthService.Instance.AuthStateChanged += refreshHandler;
            FirebaseAuthService.Instance.AvatarChanged += refreshHandler;
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
            try
            {
                if (Width <= 4 || Height <= 4) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                bool isLoggedIn = FirebaseAuthService.Instance.IsLoggedIn;
                Image avatar = FirebaseAuthService.Instance.UserAvatar;

                int size = Math.Min(Width, Height);
                int cx = Width / 2;
                int cy = Height / 2;

                if (isLoggedIn)
                {
                    int d = Math.Max(4, size - 4);
                    int x = cx - d / 2;
                    int y = cy - d / 2;
                    Rectangle rCircle = new Rectangle(x, y, d, d);

                    // 1. Viền ngoài tỏa sáng gradient (Glowing Aura)
                    int glowAlpha = _isHovered ? 140 : 65;
                    using (Pen glowPen = new Pen(Color.FromArgb(glowAlpha, _accentPink), 2.5f))
                    {
                        g.DrawEllipse(glowPen, x - 1, y - 1, d + 2, d + 2);
                    }

                    // 2. Nội dung Avatar tròn
                    int innerD = Math.Max(2, d - 2);
                    Rectangle rInner = new Rectangle(x + 1, y + 1, innerD, innerD);
                    bool drewAvatar = false;
                    if (avatar != null)
                    {
                        try
                        {
                            using (GraphicsPath clip = new GraphicsPath())
                            {
                                clip.AddEllipse(rInner);
                                g.SetClip(clip);
                                g.DrawImage(avatar, rInner);
                                g.ResetClip();
                            }
                            drewAvatar = true;
                        }
                        catch { }
                    }

                    if (!drewAvatar)
                    {
                        // Gradient nền mặc định khi chưa upload ảnh hoặc lỗi ảnh
                        using (LinearGradientBrush bgBrush = new LinearGradientBrush(rInner,
                            Color.FromArgb(48, 22, 38), Color.FromArgb(28, 16, 36), 45f))
                        {
                            g.FillEllipse(bgBrush, rInner);
                        }

                        // Silhouette icon
                        using (SolidBrush headBrush = new SolidBrush(_accentPink))
                        using (Pen bodyPen = new Pen(_accentPink, 1.6f))
                        {
                            bodyPen.StartCap = LineCap.Round;
                            bodyPen.EndCap = LineCap.Round;
                            int headR = 3;
                            g.FillEllipse(headBrush, cx - headR, cy - 4 - headR, headR * 2, headR * 2);
                            g.DrawArc(bodyPen, cx - 6, cy + 1, 12, 10, 195, 150);
                        }
                    }

                    // 3. Viền gradient sắc nét (Sharp Gradient Ring)
                    using (LinearGradientBrush gradBrush = new LinearGradientBrush(rCircle,
                        _accentPink, _accentPurple, 45f))
                    using (Pen gradPen = new Pen(gradBrush, 1.8f))
                    {
                        g.DrawEllipse(gradPen, rCircle);
                    }

                    // 4. Chấm online xanh mint ở góc trên phải
                    using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(52, 211, 153)))
                    using (Pen dotBorder = new Pen(Color.FromArgb(24, 21, 32), 1.2f))
                    {
                        int dotSize = Math.Max(6, (int)(size * 0.22f));
                        int dotX = Width - dotSize - 2;
                        int dotY = 2;
                        g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                        g.DrawEllipse(dotBorder, dotX, dotY, dotSize, dotSize);
                    }
                }
                else
                {
                    // Not logged in: Default subtle pill / circle
                    if (_isHovered)
                    {
                        Color bg = _isPressed
                            ? Color.FromArgb(50, 255, 255, 255)
                            : Color.FromArgb(25, 255, 255, 255);
                        using (SolidBrush b = new SolidBrush(bg))
                        {
                            g.FillEllipse(b, 2, 2, Math.Max(2, Width - 5), Math.Max(2, Height - 5));
                        }
                    }

                    Color iconColor = _isHovered ? _hoverColor : _defaultColor;
                    using (Pen pen = new Pen(iconColor, 2f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;

                        // Head
                        int headR = Math.Max(4, (int)(size * 0.16f));
                        int headY = cy - 4;
                        g.DrawEllipse(pen, cx - headR, headY - headR, headR * 2, headR * 2);

                        // Shoulders arc
                        int bodyW = Math.Max(14, (int)(size * 0.52f));
                        int bodyH = Math.Max(8, (int)(size * 0.32f));
                        int bodyX = cx - bodyW / 2;
                        int bodyY = cy + 2;
                        g.DrawArc(pen, bodyX, bodyY, bodyW, bodyH * 2, 195, 150);
                    }
                }
            }
            catch { }
        }
    }
}
