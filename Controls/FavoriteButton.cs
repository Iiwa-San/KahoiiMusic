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
            Size = new Size(32, 32);
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

            // Hover circle background
            if (_isHovered)
            {
                using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                {
                    g.FillEllipse(hoverBg, 2, 2, w - 4, h - 4);
                }
            }

            // Target heart size
            float heartW = 19f;
            float heartH = 17f;

            if (_isPressed)
            {
                heartW *= 0.92f;
                heartH *= 0.92f;
            }
            else if (_isHovered)
            {
                heartW *= 1.05f;
                heartH *= 1.05f;
            }

            float heartX = (w - heartW) / 2f;
            float heartY = (h - heartH) / 2f;

            RectangleF heartBounds = new RectangleF(heartX, heartY, heartW, heartH);

            using (GraphicsPath heartPath = CreateHeartPath(heartBounds))
            {
                if (_isFavorite)
                {
                    // Soft Neon Glow behind
                    RectangleF glowBounds = new RectangleF(heartX - 2.5f, heartY - 2.5f, heartW + 5f, heartH + 5f);
                    using (GraphicsPath glowPath = CreateHeartPath(glowBounds))
                    using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(45, 255, 45, 85)))
                    {
                        g.FillPath(glowBrush, glowPath);
                    }

                    // Solid vibrant heart fill
                    Color fillColor = _isHovered ? _activeHoverColor : _activeColor;
                    using (SolidBrush fillBrush = new SolidBrush(fillColor))
                    {
                        g.FillPath(fillBrush, heartPath);
                    }

                    // Crisp border highlight
                    using (Pen borderPen = new Pen(Color.FromArgb(200, 255, 120, 150), 1f))
                    {
                        g.DrawPath(borderPen, heartPath);
                    }
                }
                else
                {
                    // Subtle inactive outline
                    Color strokeColor = _isHovered ? _inactiveHoverColor : _inactiveColor;
                    using (Pen strokePen = new Pen(strokeColor, 1.8f) { LineJoin = LineJoin.Round })
                    {
                        g.DrawPath(strokePen, heartPath);
                    }
                }
            }
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
