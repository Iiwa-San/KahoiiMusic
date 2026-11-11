using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public enum DirectionType
    {
        Previous,
        Next
    }

    public class DirectionalButton : Control
    {
        private DirectionType _direction = DirectionType.Previous;
        private bool _isHovered = false;
        private bool _isPressed = false;

        private Color _outlineColor = Color.White;
        private float _lineWidth = 2.0f;

        public DirectionalButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(24, 24);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        [Category("Appearance")]
        [DefaultValue(DirectionType.Previous)]
        public DirectionType Direction
        {
            get { return _direction; }
            set
            {
                _direction = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool FillShape { get; set; } = false;

        [Category("Appearance")]
        public Color FillColor { get; set; } = Color.Transparent;

        [Category("Appearance")]
        public Color OutlineColor
        {
            get { return _outlineColor; }
            set
            {
                _outlineColor = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(2.0f)]
        public float LineWidth
        {
            get { return _lineWidth; }
            set
            {
                _lineWidth = value;
                Invalidate();
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

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool UseThemeGradient { get; set; } = false;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_isPressed)
            {
                g.TranslateTransform(0, 1);
            }

            // Equilateral triangle dimensions
            float maxH = Height - 8f;
            float maxW = Width - 8f;
            float s = Math.Min(maxH, maxW / 0.866f); // Side length
            float hAlt = s * 0.866f;                 // Altitude

            float cx = Width / 2f;
            float cy = Height / 2f;

            using (GraphicsPath path = new GraphicsPath())
            {
                float r = Math.Max(3f, s * 0.16f); // Rounded corner radius

                if (_direction == DirectionType.Previous)
                {
                    float leftX = cx - hAlt * 0.52f;
                    float rightX = cx + hAlt * 0.48f;
                    float topY = cy - s * 0.5f;
                    float bottomY = cy + s * 0.5f;
                    float midY = cy;

                    path.AddArc(rightX - 2 * r, topY, 2 * r, 2 * r, 270, 90);
                    path.AddArc(rightX - 2 * r, bottomY - 2 * r, 2 * r, 2 * r, 0, 90);
                    path.AddArc(leftX, midY - r, 2 * r, 2 * r, 90, 180);
                    path.CloseFigure();
                }
                else
                {
                    float leftX = cx - hAlt * 0.48f;
                    float rightX = cx + hAlt * 0.52f;
                    float topY = cy - s * 0.5f;
                    float bottomY = cy + s * 0.5f;
                    float midY = cy;

                    path.AddArc(leftX, topY, 2 * r, 2 * r, 180, 90);
                    path.AddArc(rightX - 2 * r, midY - r, 2 * r, 2 * r, 270, 180);
                    path.AddArc(leftX, bottomY - 2 * r, 2 * r, 2 * r, 90, 90);
                    path.CloseFigure();
                }

                RectangleF bounds = path.GetBounds();

                if (UseThemeGradient)
                {
                    Color topColor = _isHovered ? Color.FromArgb(145, 25, 185) : Color.FromArgb(105, 12, 150);
                    Color bottomColor = _isHovered ? Color.FromArgb(255, 50, 155) : Color.FromArgb(235, 0, 130);

                    using (LinearGradientBrush grad = new LinearGradientBrush(bounds, topColor, bottomColor, LinearGradientMode.Vertical))
                    {
                        g.FillPath(grad, path);
                    }

                    using (Pen borderPen = new Pen(Color.FromArgb(100, 255, 180, 225), 1.0f))
                    {
                        borderPen.LineJoin = LineJoin.Round;
                        g.DrawPath(borderPen, path);
                    }
                }
                else
                {
                    if (FillShape && FillColor != Color.Transparent)
                    {
                        using (SolidBrush fillBrush = new SolidBrush(FillColor))
                        {
                            g.FillPath(fillBrush, path);
                        }
                    }

                    if (_isHovered)
                    {
                        using (SolidBrush hoverBrush = new SolidBrush(Color.FromArgb(60, 255, 45, 135)))
                        {
                            g.FillPath(hoverBrush, path);
                        }
                    }

                    if (!FillShape || _outlineColor != Color.Transparent)
                    {
                        using (Pen pen = new Pen(_outlineColor, _lineWidth))
                        {
                            pen.LineJoin = LineJoin.Round;
                            pen.StartCap = LineCap.Round;
                            pen.EndCap = LineCap.Round;
                            g.DrawPath(pen, path);
                        }
                    }
                }
            }
        }
    }
}
