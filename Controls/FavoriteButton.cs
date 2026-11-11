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

        private class StarParticle
        {
            public float X;
            public float Y;
            public float Vx;
            public float Vy;
            public float Size;
            public float Rotation;
            public float RotationSpeed;
            public Color Color;
            public int Life;
            public int MaxLife;
        }

        private readonly System.Collections.Generic.List<StarParticle> _particles = new System.Collections.Generic.List<StarParticle>();
        private readonly Timer _particleTimer = new Timer();
        private float _bounceScale = 1.0f;
        private int _bounceAnim = 0;

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

            _particleTimer.Interval = 16;
            _particleTimer.Tick += (s, e) =>
            {
                bool needInvalidate = false;
                if (_particles.Count > 0)
                {
                    for (int i = _particles.Count - 1; i >= 0; i--)
                    {
                        var p = _particles[i];
                        p.X += p.Vx;
                        p.Y += p.Vy;
                        p.Vy += 0.08f; // Light gravity
                        p.Vx *= 0.94f; // Drag
                        p.Vy *= 0.94f;
                        p.Rotation += p.RotationSpeed;
                        p.Life++;
                        if (p.Life >= p.MaxLife)
                        {
                            _particles.RemoveAt(i);
                        }
                    }
                    needInvalidate = true;
                }

                if (_bounceAnim > 0)
                {
                    _bounceAnim--;
                    if (_bounceAnim > 10) _bounceScale = 1.0f + (20 - _bounceAnim) * 0.025f;
                    else _bounceScale = 1.0f + _bounceAnim * 0.025f;
                    needInvalidate = true;
                }
                else
                {
                    _bounceScale = 1.0f;
                }

                if (_particles.Count == 0 && _bounceAnim == 0)
                {
                    _particleTimer.Stop();
                }

                if (needInvalidate)
                {
                    Invalidate();
                }
            };

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

        public void TriggerStarBurst()
        {
            _particles.Clear();
            _bounceAnim = 20;

            float cx = Width / 2f;
            float cy = Height / 2f;
            Random rnd = new Random();

            Color[] starPalette = new Color[]
            {
                Color.FromArgb(255, 240, 75),   // Sparkling Gold
                Color.FromArgb(255, 60, 160),   // Hot Pink
                Color.FromArgb(255, 255, 255),  // Pure White
                Color.FromArgb(255, 130, 210),  // Light Pink
                Color.FromArgb(255, 195, 45),   // Amber
                Color.FromArgb(140, 230, 255)   // Cyan Sparkle
            };

            int count = 18;
            for (int i = 0; i < count; i++)
            {
                float angle = (float)(i * (2.0 * Math.PI / count) + (rnd.NextDouble() - 0.5) * 0.5);
                float speed = (float)(2.2 + rnd.NextDouble() * 3.6);
                _particles.Add(new StarParticle
                {
                    X = cx + (float)(Math.Cos(angle) * 4.0),
                    Y = cy + (float)(Math.Sin(angle) * 4.0),
                    Vx = (float)(Math.Cos(angle) * speed),
                    Vy = (float)(Math.Sin(angle) * speed) - 1.2f, // upward arc
                    Size = (float)(3.5 + rnd.NextDouble() * 4.5),
                    Rotation = (float)(rnd.NextDouble() * 360.0),
                    RotationSpeed = (float)((rnd.NextDouble() - 0.5) * 22.0),
                    Color = starPalette[rnd.Next(starPalette.Length)],
                    Life = 0,
                    MaxLife = 26 + rnd.Next(12)
                });
            }

            if (!_particleTimer.Enabled)
            {
                _particleTimer.Start();
            }
            Invalidate();
        }

        private bool _forceFilledHeart = false;
        [Category("Appearance")]
        [DefaultValue(false)]
        public bool ForceFilledHeart
        {
            get => _forceFilledHeart;
            set
            {
                if (_forceFilledHeart != value)
                {
                    _forceFilledHeart = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowCheckmark { get; set; } = true;

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
            if (newState)
            {
                TriggerStarBurst();
            }
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

            // Target heart size (scales naturally whether button is 24x24 or 56x56)
            float baseW = Math.Min(w * 0.70f, 22f);
            float baseH = baseW * 0.90f;
            float heartW = baseW * _bounceScale;
            float heartH = baseH * _bounceScale;

            if (_isPressed)
            {
                heartW *= 0.90f;
                heartH *= 0.90f;
            }
            else if (_isHovered)
            {
                heartW *= 1.08f;
                heartH *= 1.08f;
            }

            float heartX = (w - heartW) / 2f;
            float heartY = (h - heartH) / 2f;
            RectangleF heartBounds = new RectangleF(heartX, heartY, heartW, heartH);

            using (GraphicsPath heartPath = CreateHeartPath(heartBounds))
            {
                if (_isFavorite || _forceFilledHeart)
                {
                    // 1. Radiant Soft Neon Glow Halo behind heart
                    RectangleF glowBounds = new RectangleF(heartX - 3.0f, heartY - 3.0f, heartW + 6.0f, heartH + 6.0f);
                    using (GraphicsPath glowPath = CreateHeartPath(glowBounds))
                    using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(60, 255, 30, 110)))
                    {
                        g.FillPath(glowBrush, glowPath);
                    }

                    // 2. Hot Pink to Bright Crimson Red Gradient Fill matching Figma
                    Color topRuby = _isHovered ? Color.FromArgb(255, 60, 160) : Color.FromArgb(255, 20, 130);
                    Color bottomRuby = _isHovered ? Color.FromArgb(255, 30, 60) : Color.FromArgb(245, 0, 35);
                    using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                        heartBounds,
                        topRuby,
                        bottomRuby,
                        LinearGradientMode.Vertical))
                    {
                        g.FillPath(fillBrush, heartPath);
                    }

                    // 3. Crisp delicate high-contrast border
                    using (Pen borderPen = new Pen(Color.FromArgb(160, 255, 180, 210), 0.9f))
                    {
                        g.DrawPath(borderPen, heartPath);
                    }

                    // 4. Optional Green Checkmark (only when enabled and not force filled)
                    if (ShowCheckmark && !_forceFilledHeart)
                    {
                        PointF p1 = new PointF(heartX + heartW * 0.28f, heartY + heartH * 0.50f);
                        PointF p2 = new PointF(heartX + heartW * 0.44f, heartY + heartH * 0.67f);
                        PointF p3 = new PointF(heartX + heartW * 0.72f, heartY + heartH * 0.35f);

                        using (Pen shadowPen = new Pen(Color.FromArgb(120, 0, 0, 0), 2.2f)
                        {
                            StartCap = LineCap.Round,
                            EndCap = LineCap.Round,
                            LineJoin = LineJoin.Round
                        })
                        {
                            g.DrawLines(shadowPen, new PointF[] {
                                new PointF(p1.X, p1.Y + 0.6f),
                                new PointF(p2.X, p2.Y + 0.6f),
                                new PointF(p3.X, p3.Y + 0.6f)
                            });
                        }

                        Color tickColor = _isHovered ? Color.FromArgb(74, 255, 140) : Color.FromArgb(34, 220, 94);
                        using (Pen tickPen = new Pen(tickColor, 2.0f)
                        {
                            StartCap = LineCap.Round,
                            EndCap = LineCap.Round,
                            LineJoin = LineJoin.Round
                        })
                        {
                            g.DrawLines(tickPen, new PointF[] { p1, p2, p3 });
                        }
                    }
                }
                else
                {
                    // Solid filled purple heart with 70% opacity matching other control buttons (#32124B)
                    Color purpleFill = _isHovered ? Color.FromArgb(220, 70, 25, 105) : Color.FromArgb(178, 50, 18, 75);
                    using (SolidBrush fillBrush = new SolidBrush(purpleFill))
                    {
                        g.FillPath(fillBrush, heartPath);
                    }

                    // Subtle outline to maintain definition over dark album art
                    Color strokeColor = _isHovered ? Color.FromArgb(180, 120, 50, 160) : Color.FromArgb(100, 80, 35, 115);
                    using (Pen strokePen = new Pen(strokeColor, 1.0f) { LineJoin = LineJoin.Round })
                    {
                        g.DrawPath(strokePen, heartPath);
                    }
                }
            }

            // 5. Render Sparkle Star Particles
            if (_particles.Count > 0)
            {
                for (int i = 0; i < _particles.Count; i++)
                {
                    var p = _particles[i];
                    float lifeRatio = (float)p.Life / (float)p.MaxLife;
                    int alpha = (int)(255 * (1.0f - lifeRatio));
                    if (alpha < 0) alpha = 0;
                    if (alpha > 255) alpha = 255;
                    Color pColor = Color.FromArgb(alpha, p.Color.R, p.Color.G, p.Color.B);
                    float currentSize = p.Size * (1.0f - lifeRatio * 0.3f);
                    DrawSparkleStar(g, p.X, p.Y, currentSize, p.Rotation, pColor);
                }
            }
        }

        private static void DrawSparkleStar(Graphics g, float cx, float cy, float radius, float angle, Color color)
        {
            if (radius <= 0.5f) return;
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(angle);

            float rInner = radius * 0.28f;
            PointF[] pts = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                float r = (i % 2 == 0) ? radius : rInner;
                double a = i * Math.PI / 4.0;
                pts[i] = new PointF((float)(r * Math.Cos(a)), (float)(r * Math.Sin(a)));
            }

            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPolygon(brush, pts);
            }
            g.Restore(state);
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
                _particleTimer?.Stop();
                _particleTimer?.Dispose();
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

