using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public class FavoriteButton : Control
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isFavorite = false;
        private PlaylistItem _currentTrack = null;
        private readonly ToolTip _toolTip;

        private readonly Color _activeColor = Color.FromArgb(255, 45, 85);       // Neon Pink #FF2D55
        private readonly Color _activeHoverColor = Color.FromArgb(255, 84, 117);  // Brighter Pink #FF5475
        private readonly Color _inactiveColor = Color.FromArgb(190, 190, 205);   // Muted outline
        private readonly Color _inactiveHoverColor = Color.White;                // Hover white

        public event EventHandler<bool> FavoriteChanged;

        public FavoriteButton()
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

            _toolTip = new ToolTip
            {
                ShowAlways = true,
                AutoPopDelay = 4000,
                InitialDelay = 350,
                ReshowDelay = 150
            };

            FavoritesService.FavoritesChanged += (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((Action)(() => RefreshState()));
                    }
                    catch { }
                }
            };

            UpdateToolTip();
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    UpdateToolTip();
                    Invalidate();
                }
            }
        }

        public PlaylistItem CurrentTrack => _currentTrack;

        public void SetCurrentTrack(PlaylistItem track)
        {
            _currentTrack = track;
            RefreshState();
        }

        public void RefreshState()
        {
            if (_currentTrack != null && !string.IsNullOrWhiteSpace(_currentTrack.Id))
            {
                _isFavorite = FavoritesService.IsFavorite(_currentTrack.Id);
            }
            else
            {
                _isFavorite = false;
            }
            UpdateToolTip();
            Invalidate();
        }

        public bool ToggleFavorite()
        {
            if (_currentTrack == null || string.IsNullOrWhiteSpace(_currentTrack.Id))
            {
                return false;
            }

            bool newState = FavoritesService.ToggleFavorite(_currentTrack);
            _isFavorite = newState;
            UpdateToolTip();
            Invalidate();
            FavoriteChanged?.Invoke(this, newState);
            return newState;
        }

        private void UpdateToolTip()
        {
            if (_currentTrack == null || string.IsNullOrWhiteSpace(_currentTrack.Id))
            {
                _toolTip.SetToolTip(this, "Yêu thích (Chưa phát bài hát)");
            }
            else if (_isFavorite)
            {
                _toolTip.SetToolTip(this, "Bỏ yêu thích (Phím tắt: F)");
            }
            else
            {
                _toolTip.SetToolTip(this, "Thêm vào Yêu thích (Phím tắt: F)");
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
            if (_isPressed)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ToggleFavorite();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int w = Width;
            int h = Height;

            // Optional hover glass pill background matching other buttons
            if (_isHovered)
            {
                Color bg = _isPressed ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(28, 255, 255, 255);
                using (SolidBrush hoverBg = new SolidBrush(bg))
                using (GraphicsPath pill = GetRoundedRect(new Rectangle(1, 1, w - 2, h - 2), 6))
                {
                    g.FillPath(hoverBg, pill);
                }
            }

            // Target heart size scaled for 28x24
            float heartW = 16f;
            float heartH = 14.5f;

            if (_isPressed)
            {
                heartW *= 0.90f;
                heartH *= 0.90f;
            }
            else if (_isHovered)
            {
                heartW *= 1.06f;
                heartH *= 1.06f;
            }

            float heartX = (w - heartW) / 2f;
            float heartY = (h - heartH) / 2f;

            RectangleF heartBounds = new RectangleF(heartX, heartY, heartW, heartH);

            using (GraphicsPath heartPath = CreateHeartPath(heartBounds))
            {
                if (_isFavorite)
                {
                    // 1. Radiant Soft Neon Glow Halo behind heart
                    RectangleF glowBounds = new RectangleF(heartX - 3f, heartY - 3f, heartW + 6f, heartH + 6f);
                    using (GraphicsPath glowPath = CreateHeartPath(glowBounds))
                    using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(60, 255, 23, 68)))
                    {
                        g.FillPath(glowBrush, glowPath);
                    }

                    // 2. Rich Ruby Red Gradient Fill (bright neon coral-ruby to deep radiant ruby)
                    Color topRuby = _isHovered ? Color.FromArgb(255, 95, 130) : Color.FromArgb(255, 68, 108);
                    Color bottomRuby = _isHovered ? Color.FromArgb(255, 30, 80) : Color.FromArgb(235, 15, 65);
                    using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                        heartBounds,
                        topRuby,
                        bottomRuby,
                        LinearGradientMode.Vertical))
                    {
                        g.FillPath(fillBrush, heartPath);
                    }

                    // 3. Specular highlight at top lobes for 3D depth and glass glow
                    RectangleF highlightRect = new RectangleF(heartX + 2f, heartY + 1.2f, heartW - 4f, (heartH / 2f) - 1f);
                    using (GraphicsPath hlPath = new GraphicsPath())
                    {
                        hlPath.AddEllipse(highlightRect);
                        using (PathGradientBrush pgb = new PathGradientBrush(hlPath))
                        {
                            pgb.CenterColor = Color.FromArgb(170, 255, 220, 235);
                            pgb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 220, 235) };
                            g.FillPath(pgb, hlPath);
                        }
                    }

                    // 4. Crisp high-contrast border
                    using (Pen borderPen = new Pen(Color.FromArgb(220, 255, 160, 190), 1.0f))
                    {
                        g.DrawPath(borderPen, heartPath);
                    }
                }
                else
                {
                    // Subtle elegant inactive outline
                    Color strokeColor = _isHovered ? _inactiveHoverColor : _inactiveColor;
                    using (Pen strokePen = new Pen(strokeColor, 1.8f) { LineJoin = LineJoin.Round })
                    {
                        g.DrawPath(strokePen, heartPath);
                    }
                }
            }
        }

        private static GraphicsPath GetRoundedRect(Rectangle rect, int radius)
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

        private static GraphicsPath CreateHeartPath(RectangleF rect)
        {
            GraphicsPath path = new GraphicsPath();
            float x = rect.X;
            float y = rect.Y;
            float w = rect.Width;
            float h = rect.Height;

            PointF topCenter = new PointF(x + w * 0.5f, y + h * 0.28f);
            PointF bottomTip = new PointF(x + w * 0.5f, y + h * 0.95f);

            // Left half
            path.AddBezier(
                topCenter,
                new PointF(x + w * 0.28f, y),
                new PointF(x, y + h * 0.15f),
                new PointF(x, y + h * 0.45f)
            );
            path.AddBezier(
                new PointF(x, y + h * 0.45f),
                new PointF(x, y + h * 0.70f),
                new PointF(x + w * 0.22f, y + h * 0.85f),
                bottomTip
            );

            // Right half
            path.AddBezier(
                bottomTip,
                new PointF(x + w * 0.78f, y + h * 0.85f),
                new PointF(x + w, y + h * 0.70f),
                new PointF(x + w, y + h * 0.45f)
            );
            path.AddBezier(
                new PointF(x + w, y + h * 0.45f),
                new PointF(x + w, y + h * 0.15f),
                new PointF(x + w * 0.72f, y),
                topCenter
            );

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

