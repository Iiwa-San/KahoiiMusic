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

        [Category("Appearance")]
        public Color HoverColor
        {
            get { return _hoverColor; }
            set { _hoverColor = value; Invalidate(); }
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
            float padX = 2.5f;
            float padY = 2.0f;
            float availW = Width - 2 * padX;
            float availH = Height - 2 * padY;
            float sx = availW / 24.0f;
            float sy = availH / 20.0f;
            float ox = padX;
            float oy = padY;
            float lineWidth = Math.Max(1.6f, 1.6f * sy);

            using (Pen pen = new Pen(currentColor, lineWidth))
            using (SolidBrush brush = new SolidBrush(currentColor))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                // 1. Speaker body (cleanly inset)
                PointF[] pts = new PointF[]
                {
                    new PointF(ox + 1.5f * sx, oy + 6.5f * sy),
                    new PointF(ox + 5.0f * sx, oy + 6.5f * sy),
                    new PointF(ox + 10.0f * sx, oy + 2.0f * sy),
                    new PointF(ox + 10.0f * sx, oy + 18.0f * sy),
                    new PointF(ox + 5.0f * sx, oy + 13.5f * sy),
                    new PointF(ox + 1.5f * sx, oy + 13.5f * sy)
                };

                g.FillPolygon(brush, pts);

                // 2. Sound waves / Mute icon (cleanly inset, no clipping)
                if (_volume <= 0.001f)
                {
                    // Muted: Draw small 'x'
                    g.DrawLine(pen, ox + 13.5f * sx, oy + 6.5f * sy, ox + 20.0f * sx, oy + 13.5f * sy);
                    g.DrawLine(pen, ox + 20.0f * sx, oy + 6.5f * sy, ox + 13.5f * sx, oy + 13.5f * sy);
                }
                else
                {
                    // Small wave
                    g.DrawArc(pen, ox + 8.5f * sx, oy + 5.5f * sy, 8.5f * sx, 9.0f * sy, -40, 80);

                    // Large wave if volume > 0.4
                    if (_volume > 0.4f)
                    {
                        g.DrawArc(pen, ox + 9.5f * sx, oy + 2.5f * sy, 12.5f * sx, 15.0f * sy, -40, 80);
                    }
                }
            }
        }
    }
}
