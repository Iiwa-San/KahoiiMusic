using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Models;

namespace KahoiiMusic.Controls
{
    public class LoopButton : Control
    {
        private LoopMode _mode = LoopMode.Off;
        private bool _isHovered = false;
        private bool _isPressed = false;

        private Color _inactiveColor = Color.FromArgb(220, 220, 220);
        private Color _inactiveHoverColor = Color.FromArgb(220, 220, 220);
        private Color _activeColor = Color.FromArgb(168, 85, 247);
        private Color _activeHoverColor = Color.FromArgb(192, 132, 252);

        public event EventHandler LoopChanged;

        public LoopButton()
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
        }

        [Category("Behavior")]
        [DefaultValue(LoopMode.Off)]
        public LoopMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    Invalidate();
                    LoopChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsLooping
        {
            get => _mode != LoopMode.Off;
            set
            {
                LoopMode target = value ? LoopMode.All : LoopMode.Off;
                if (_mode != target)
                {
                    _mode = target;
                    Invalidate();
                    LoopChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Appearance")]
        public Color ActiveColor
        {
            get => _activeColor;
            set { _activeColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color InactiveColor
        {
            get => _inactiveColor;
            set { _inactiveColor = value; Invalidate(); }
        }

        public void CycleNextMode()
        {
            switch (_mode)
            {
                case LoopMode.Off:
                    Mode = LoopMode.All;
                    break;
                case LoopMode.All:
                    Mode = LoopMode.One;
                    break;
                case LoopMode.One:
                    Mode = LoopMode.Shuffle;
                    break;
                case LoopMode.Shuffle:
                default:
                    Mode = LoopMode.Off;
                    break;
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
            if (_isPressed && e.Button == MouseButtons.Left)
            {
                _isPressed = false;
                CycleNextMode();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            bool isActive = _mode != LoopMode.Off;
            Color drawColor;
            if (isActive)
            {
                drawColor = _isHovered ? _activeHoverColor : _activeColor;
            }
            else
            {
                drawColor = _isHovered ? _inactiveHoverColor : _inactiveColor;
            }

            if (_isPressed)
            {
                drawColor = Color.FromArgb(Math.Max(0, drawColor.R - 30),
                                           Math.Max(0, drawColor.G - 30),
                                           Math.Max(0, drawColor.B - 30));
            }

            if (_mode == LoopMode.Shuffle)
            {
                // Draw Shuffle crossed arrows
                DrawShuffleIcon(g, drawColor);
            }
            else
            {
                // Draw Loop arrows (Off, All, One)
                DrawLoopIcon(g, drawColor, _mode == LoopMode.One);
            }

            // Indicator dot below when active
            if (isActive)
            {
                using (SolidBrush dotBrush = new SolidBrush(drawColor))
                {
                    float dotX = (Width / 2.0f) - 1.25f;
                    float dotY = Height - 3.5f;
                    g.FillEllipse(dotBrush, dotX, dotY, 2.5f, 2.5f);
                }
            }
        }

        private void DrawLoopIcon(Graphics g, Color drawColor, bool isRepeatOne)
        {
            float left = 6.5f;
            float right = 19.5f;
            float top = 4.5f;
            float bottom = 15.5f;
            float radius = 4.0f;

            using (Pen pen = new Pen(drawColor, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLine(left + radius, top, right, top);
                    path.AddArc(right - radius, top, radius * 2f, bottom - top, -90, 180);
                    path.AddLine(right, bottom, left + radius, bottom);
                    path.AddArc(left - radius, top, radius * 2f, bottom - top, 90, 180);
                    g.DrawPath(pen, path);
                }
            }

            using (SolidBrush brush = new SolidBrush(drawColor))
            {
                // Top-right arrowhead pointing right
                PointF[] arrow1 = new PointF[]
                {
                    new PointF(right + 2.5f, top),
                    new PointF(right - 1.5f, top - 3.2f),
                    new PointF(right - 1.5f, top + 3.2f)
                };
                g.FillPolygon(brush, arrow1);

                // Bottom-left arrowhead pointing left
                PointF[] arrow2 = new PointF[]
                {
                    new PointF(left - 2.5f, bottom),
                    new PointF(left + 1.5f, bottom - 3.2f),
                    new PointF(left + 1.5f, bottom + 3.2f)
                };
                g.FillPolygon(brush, arrow2);

                // If Repeat One, draw "1" in the center
                if (isRepeatOne)
                {
                    using (Font font = new Font("Segoe UI", 7.0f, FontStyle.Bold))
                    {
                        using (StringFormat sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        })
                        {
                            RectangleF textRect = new RectangleF(left - 2f, top - 2f, (right - left) + 4f, (bottom - top) + 4f);
                            g.DrawString("1", font, brush, textRect, sf);
                        }
                    }
                }
            }
        }

        private void DrawShuffleIcon(Graphics g, Color drawColor)
        {
            float left = 6.0f;
            float right = 20.0f;
            float top = 5.0f;
            float bottom = 15.0f;

            using (Pen pen = new Pen(drawColor, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                // Top line going to bottom
                using (GraphicsPath path1 = new GraphicsPath())
                {
                    path1.AddBezier(left, top, left + 5f, top, right - 5f, bottom, right, bottom);
                    g.DrawPath(pen, path1);
                }

                // Bottom line crossing to top
                using (GraphicsPath path2 = new GraphicsPath())
                {
                    path2.AddBezier(left, bottom, left + 4f, bottom, left + 5.5f, (top + bottom) / 2f + 1.5f, left + 6.5f, (top + bottom) / 2f + 1.5f);
                    g.DrawPath(pen, path2);
                }

                using (GraphicsPath path3 = new GraphicsPath())
                {
                    path3.AddBezier(right - 6.5f, (top + bottom) / 2f - 1.5f, right - 5.5f, (top + bottom) / 2f - 1.5f, right - 4f, top, right, top);
                    g.DrawPath(pen, path3);
                }
            }

            using (SolidBrush brush = new SolidBrush(drawColor))
            {
                // Arrowhead at (right, top) pointing right
                PointF[] arrowTop = new PointF[]
                {
                    new PointF(right + 2.5f, top),
                    new PointF(right - 1.5f, top - 3.0f),
                    new PointF(right - 1.5f, top + 3.0f)
                };
                g.FillPolygon(brush, arrowTop);

                // Arrowhead at (right, bottom) pointing right
                PointF[] arrowBottom = new PointF[]
                {
                    new PointF(right + 2.5f, bottom),
                    new PointF(right - 1.5f, bottom - 3.0f),
                    new PointF(right - 1.5f, bottom + 3.0f)
                };
                g.FillPolygon(brush, arrowBottom);
            }
        }
    }
}
