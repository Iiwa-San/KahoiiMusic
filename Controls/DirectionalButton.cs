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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float padX = 3.5f;
            float padY = 3.5f;
            float w = Width - 2 * padX;
            float h = Height - 2 * padY;

            if (_isPressed)
            {
                g.TranslateTransform(0, 1);
            }

            using (GraphicsPath path = new GraphicsPath())
            {
                float r = 3.5f; // corner radius

                if (_direction == DirectionType.Previous)
                {
                    float leftX = padX + 1.0f;
                    float rightX = padX + w - 1.0f;
                    float topY = padY + 1.0f;
                    float bottomY = padY + h - 1.0f;
                    float midY = padY + h / 2.0f;

                    // Top-right corner arc
                    path.AddArc(rightX - 2 * r, topY, 2 * r, 2 * r, 270, 90);
                    // Bottom-right corner arc
                    path.AddArc(rightX - 2 * r, bottomY - 2 * r, 2 * r, 2 * r, 0, 90);
                    // Left apex arc
                    path.AddArc(leftX, midY - r, 2 * r, 2 * r, 90, 180);
                    path.CloseFigure();
                }
                else
                {
                    float leftX = padX + 1.0f;
                    float rightX = padX + w - 1.0f;
                    float topY = padY + 1.0f;
                    float bottomY = padY + h - 1.0f;
                    float midY = padY + h / 2.0f;

                    // Top-left corner arc
                    path.AddArc(leftX, topY, 2 * r, 2 * r, 180, 90);
                    // Right apex arc
                    path.AddArc(rightX - 2 * r, midY - r, 2 * r, 2 * r, 270, 180);
                    // Bottom-left corner arc
                    path.AddArc(leftX, bottomY - 2 * r, 2 * r, 2 * r, 90, 90);
                    path.CloseFigure();
                }

                if (_isHovered)
                {
                    using (SolidBrush hoverBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    {
                        g.FillPath(hoverBrush, path);
                    }
                }

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
