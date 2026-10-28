using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class VolumePopup : Control
    {
        private float _volume = 1.0f;
        private float _lastNonZeroVolume = 1.0f;
        private bool _isDragging = false;
        private bool _isHovered = false;

        private Color _cardBgColor = Color.FromArgb(128, 24, 24, 24); // 50% opacity
        private Color _cardBorderColor = Color.FromArgb(50, 255, 255, 255);
        private Color _filledColor = Color.FromArgb(235, 15, 15);     // Red
        private Color _unfilledColor = Color.FromArgb(255, 255, 255); // White

        private const int TrackTop = 26;
        private const int TrackBottom = 104;
        private const int TrackHeight = TrackBottom - TrackTop; // 78px
        private const int ThumbSize = 10;

        public event EventHandler<float> VolumeChanged;

        public VolumePopup()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(36, 136);
            DoubleBuffered = true;
            Visible = false; // Initially hidden until volume button is clicked
        }

        [Category("Behavior")]
        public float Volume
        {
            get { return _volume; }
            set
            {
                float clamped = Math.Max(0.0f, Math.Min(1.0f, value));
                if (Math.Abs(_volume - clamped) > 0.001f)
                {
                    _volume = clamped;
                    if (_volume > 0.01f)
                    {
                        _lastNonZeroVolume = _volume;
                    }
                    Invalidate();
                    if (VolumeChanged != null)
                    {
                        VolumeChanged(this, _volume);
                    }
                }
            }
        }

        public void ToggleMute()
        {
            if (_volume > 0.001f)
            {
                _lastNonZeroVolume = _volume;
                Volume = 0.0f;
            }
            else
            {
                Volume = _lastNonZeroVolume > 0.05f ? _lastNonZeroVolume : 0.5f;
            }
        }

        private float GetVolumeFromY(int y)
        {
            float v = (float)(TrackBottom - y) / TrackHeight;
            return Math.Max(0.0f, Math.Min(1.0f, v));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                // Bottom speaker icon click (mute/unmute)
                if (e.Y >= 108)
                {
                    ToggleMute();
                    return;
                }

                // Vertical slider click/drag
                _isDragging = true;
                Capture = true;
                Volume = GetVolumeFromY(e.Y);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                Volume = GetVolumeFromY(e.Y);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                Capture = false;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float delta = (e.Delta > 0) ? 0.05f : -0.05f;
            Volume += delta;
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. Draw rounded card container
            int cornerRadius = 8;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath cardPath = new GraphicsPath())
            {
                cardPath.AddArc(rect.X, rect.Y, cornerRadius, cornerRadius, 180, 90);
                cardPath.AddArc(rect.Right - cornerRadius, rect.Y, cornerRadius, cornerRadius, 270, 90);
                cardPath.AddArc(rect.Right - cornerRadius, rect.Bottom - cornerRadius, cornerRadius, cornerRadius, 0, 90);
                cardPath.AddArc(rect.X, rect.Bottom - cornerRadius, cornerRadius, cornerRadius, 90, 90);
                cardPath.CloseFigure();

                using (SolidBrush bgBrush = new SolidBrush(_cardBgColor))
                {
                    g.FillPath(bgBrush, cardPath);
                }

                using (Pen borderPen = new Pen(_cardBorderColor, 1.0f))
                {
                    g.DrawPath(borderPen, cardPath);
                }
            }

            // 2. Volume Percentage text at top
            int pct = (int)Math.Round(_volume * 100);
            string pctText = pct + "%";
            using (Font font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(220, 220, 220)))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                Rectangle textRect = new Rectangle(0, 4, Width, 18);
                g.DrawString(pctText, font, textBrush, textRect, sf);
            }

            // 3. Vertical Track Bar
            int trackX = Width / 2;
            int trackWidth = 3;
            int thumbY = TrackBottom - (int)(_volume * TrackHeight);

            // Unfilled track (top part - white)
            if (thumbY > TrackTop)
            {
                using (Pen pen = new Pen(_unfilledColor, trackWidth))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Flat;
                    g.DrawLine(pen, trackX, TrackTop, trackX, thumbY);
                }
            }

            // Filled track (bottom part - red)
            if (thumbY < TrackBottom)
            {
                using (Pen pen = new Pen(_filledColor, trackWidth))
                {
                    pen.StartCap = LineCap.Flat;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, trackX, thumbY, trackX, TrackBottom);
                }
            }

            // 4. Scrubber Thumb knob
            int currentThumb = (_isDragging || _isHovered) ? ThumbSize + 2 : ThumbSize;
            int radius = currentThumb / 2;
            Rectangle thumbRect = new Rectangle(trackX - radius, thumbY - radius, currentThumb, currentThumb);

            using (SolidBrush thumbBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(thumbBrush, thumbRect);
            }

            int dotSize = Math.Max(2, currentThumb / 3);
            int dotRadius = dotSize / 2;
            Rectangle dotRect = new Rectangle(trackX - dotRadius, thumbY - dotRadius, dotSize, dotSize);
            using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
            {
                g.FillEllipse(dotBrush, dotRect);
            }

            // 5. Small Speaker icon at bottom (click to mute)
            using (SolidBrush iconBrush = new SolidBrush(Color.FromArgb(180, 180, 180)))
            using (Pen iconPen = new Pen(Color.FromArgb(180, 180, 180), 1.2f))
            {
                iconPen.StartCap = LineCap.Round;
                iconPen.EndCap = LineCap.Round;

                // Tiny speaker shape centered at Y = 118
                PointF[] pts = new PointF[]
                {
                    new PointF(13f, 117f),
                    new PointF(15.5f, 117f),
                    new PointF(18.5f, 114f),
                    new PointF(18.5f, 122f),
                    new PointF(15.5f, 119f),
                    new PointF(13f, 119f)
                };
                g.FillPolygon(iconBrush, pts);

                if (_volume <= 0.001f)
                {
                    // Tiny 'x'
                    g.DrawLine(iconPen, 20.5f, 116.5f, 23.5f, 119.5f);
                    g.DrawLine(iconPen, 23.5f, 116.5f, 20.5f, 119.5f);
                }
                else
                {
                    // Tiny wave arc
                    g.DrawArc(iconPen, 18f, 115.5f, 4f, 5f, -40, 80);
                }
            }
        }
    }
}
