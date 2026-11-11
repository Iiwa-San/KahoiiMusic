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
        public Color CircleHoverColor
        {
            get { return _circleHoverColor; }
            set { _circleHoverColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color CirclePressedColor
        {
            get { return _circlePressedColor; }
            set { _circlePressedColor = value; Invalidate(); }
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

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool UseThemeGradient { get; set; } = false;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int diameter = Math.Min(Width, Height) - 2;
            int x = (Width - diameter) / 2;
            int y = (Height - diameter) / 2;
            Rectangle circleRect = new Rectangle(x, y, diameter, diameter);

            if (UseThemeGradient)
            {
                // 70% opacity (alpha 178) for cyber translucent aesthetic, brightens on hover
                Color topColor = _isHovered ? Color.FromArgb(230, 145, 25, 185) : Color.FromArgb(178, 105, 12, 150);
                Color bottomColor = _isHovered ? Color.FromArgb(255, 50, 155) : Color.FromArgb(178, 235, 0, 130);

                using (LinearGradientBrush grad = new LinearGradientBrush(circleRect, topColor, bottomColor, LinearGradientMode.Vertical))
                {
                    g.FillEllipse(grad, circleRect);
                }

                using (Pen borderPen = new Pen(Color.FromArgb(85, 255, 180, 225), 1.0f))
                {
                    g.DrawEllipse(borderPen, circleRect);
                }
            }
            else
            {
                Color currentBg = _circleColor;
                if (_isPressed) currentBg = _circlePressedColor;
                else if (_isHovered) currentBg = _circleHoverColor;

                using (SolidBrush brush = new SolidBrush(currentBg))
                {
                    g.FillEllipse(brush, circleRect);
                }
            }

            // 2. Draw Icon (White triangle or Pause bars when using theme gradient)
            float cx = circleRect.X + circleRect.Width / 2.0f;
            float cy = circleRect.Y + circleRect.Height / 2.0f;
            Color currentIconColor = UseThemeGradient 
                ? (_isHovered ? Color.White : Color.FromArgb(220, 255, 255, 255)) 
                : _iconColor;

            using (SolidBrush iconBrush = new SolidBrush(currentIconColor))
            {
                if (!_isPlaying)
                {
                    // Scale triangle proportionally with circle diameter
                    float triW = Math.Max(12f, diameter * 0.38f);
                    float triH = Math.Max(13f, diameter * 0.42f);
                    float tx = cx - triW * 0.36f;
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
                    // Scale pause bars proportionally with circle diameter
                    float barW = Math.Max(4f, diameter * 0.12f);
                    float barH = Math.Max(14f, diameter * 0.42f);
                    float gap = Math.Max(4f, diameter * 0.12f);

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
