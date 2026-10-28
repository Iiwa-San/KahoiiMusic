using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class PlayPauseButton : Control
    {
        private bool _isPlaying = false;
        private bool _isHovered = false;
        private bool _isPressed = false;

        private Color _circleColor = Color.FromArgb(217, 217, 217);        // #D9D9D9
        private Color _circleHoverColor = Color.FromArgb(235, 235, 235);   // #EBEBEB
        private Color _circlePressedColor = Color.FromArgb(190, 190, 190); // #BEBEBE
        private Color _iconColor = Color.FromArgb(106, 0, 255);            // #6A00FF (Vibrant Purple)

        public event EventHandler<bool> PlayStateChanged;

        public PlayPauseButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(30, 30);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsPlaying
        {
            get { return _isPlaying; }
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    Invalidate();
                    if (PlayStateChanged != null)
                    {
                        PlayStateChanged(this, _isPlaying);
                    }
                }
            }
        }

        [Category("Appearance")]
        public Color CircleColor
        {
            get { return _circleColor; }
            set { _circleColor = value; Invalidate(); }
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

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            IsPlaying = !IsPlaying;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int diameter = Math.Min(Width, Height) - 2;
            int x = (Width - diameter) / 2;
            int y = (Height - diameter) / 2;
            Rectangle circleRect = new Rectangle(x, y, diameter, diameter);

            Color currentBg = _circleColor;
            if (_isPressed) currentBg = _circlePressedColor;
            else if (_isHovered) currentBg = _circleHoverColor;

            // 1. Draw circle background
            using (SolidBrush brush = new SolidBrush(currentBg))
            {
                g.FillEllipse(brush, circleRect);
            }

            // 2. Draw Icon (Purple Play triangle or Pause bars)
            float cx = circleRect.X + circleRect.Width / 2.0f;
            float cy = circleRect.Y + circleRect.Height / 2.0f;

            using (SolidBrush iconBrush = new SolidBrush(_iconColor))
            {
                if (!_isPlaying)
                {
                    // Triangle height 11px, width 10px, nudged +1px right for optical balance
                    float triW = 10f;
                    float triH = 11.5f;
                    float tx = cx - triW * 0.40f + 0.8f;
                    float ty = cy;

                    PointF[] points = new PointF[]
                    {
                        new PointF(tx, ty - triH / 2f),
                        new PointF(tx + triW, ty),
                        new PointF(tx, ty + triH / 2f)
                    };
                    g.FillPolygon(iconBrush, points);
                }
                else
                {
                    // Pause bars
                    float barW = 3.5f;
                    float barH = 12f;
                    float gap = 3.5f;

                    float b1X = cx - gap / 2.0f - barW;
                    float b2X = cx + gap / 2.0f;
                    float barY = cy - barH / 2.0f;

                    g.FillRectangle(iconBrush, b1X, barY, barW, barH);
                    g.FillRectangle(iconBrush, b2X, barY, barW, barH);
                }
            }
        }
    }
}
