using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class LoopButton : Control
    {
        private bool _isLooping = false;
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

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsLooping
        {
            get { return _isLooping; }
            set
            {
                if (_isLooping != value)
                {
                    _isLooping = value;
                    Invalidate();
                    if (LoopChanged != null)
                    {
                        LoopChanged(this, EventArgs.Empty);
                    }
                }
            }
        }

        [Category("Appearance")]
        public Color ActiveColor
        {
            get { return _activeColor; }
            set { _activeColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color InactiveColor
        {
            get { return _inactiveColor; }
            set { _inactiveColor = value; Invalidate(); }
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
                IsLooping = !IsLooping;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color drawColor;
            if (_isLooping)
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

            float left = 7.0f;
            float right = 19.0f;
            float top = 5.0f;
            float bottom = 15.0f;
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

                // Active loop indicator dot below
                if (_isLooping)
                {
                    float dotX = (Width / 2.0f) - 1.25f;
                    float dotY = Height - 3.5f;
                    g.FillEllipse(brush, dotX, dotY, 2.5f, 2.5f);
                }
            }
        }
    }
}
