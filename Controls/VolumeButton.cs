using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class VolumeButton : Control
    {
        private float _volume = 1.0f;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private Color _iconColor = Color.White;
        private Color _hoverColor = Color.FromArgb(220, 220, 220);

        public VolumeButton()
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

        [Category("Appearance")]
        public float Volume
        {
            get { return _volume; }
            set
            {
                float clamped = Math.Max(0.0f, Math.Min(1.0f, value));
                if (Math.Abs(_volume - clamped) > 0.001f)
                {
                    _volume = clamped;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        public Color IconColor
        {
            get { return _iconColor; }
            set { _iconColor = value; Invalidate(); }
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Optional subtle circular hover background
            if (_isHovered || _isPressed)
            {
                using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(_isPressed ? 50 : 30, 255, 255, 255)))
                {
                    g.FillEllipse(hoverBg, 2, 0, Height - 1, Height - 1);
                }
            }

            Color currentColor = _isHovered ? _hoverColor : _iconColor;
            float lineWidth = 1.8f;

            using (Pen pen = new Pen(currentColor, lineWidth))
            using (SolidBrush brush = new SolidBrush(currentColor))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                // 1. Speaker body
                PointF[] pts = new PointF[]
                {
                    new PointF(3.0f, 8.5f),
                    new PointF(6.5f, 8.5f),
                    new PointF(11.5f, 4.5f),
                    new PointF(11.5f, 19.5f),
                    new PointF(6.5f, 15.5f),
                    new PointF(3.0f, 15.5f)
                };

                g.FillPolygon(brush, pts);

                // 2. Sound waves / Mute icon
                if (_volume <= 0.001f)
                {
                    // Muted: Draw small 'x'
                    g.DrawLine(pen, 15.0f, 9.0f, 21.0f, 15.0f);
                    g.DrawLine(pen, 21.0f, 9.0f, 15.0f, 15.0f);
                }
                else
                {
                    // Small wave
                    g.DrawArc(pen, 9.5f, 7.5f, 9.0f, 9.0f, -40, 80);

                    // Large wave if volume > 0.4
                    if (_volume > 0.4f)
                    {
                        g.DrawArc(pen, 10.5f, 4.5f, 13.0f, 15.0f, -40, 80);
                    }
                }
            }
        }
    }
}
