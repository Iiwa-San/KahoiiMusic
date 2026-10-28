using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class MusicTrackBar : Control
    {
        private float _value = 0.0f; // 0.0 to 1.0
        private bool _isDragging = false;
        private bool _isHovered = false;

        private Color _playedColor = Color.FromArgb(235, 15, 15);     // Vibrant Red
        private Color _unplayedColor = Color.FromArgb(255, 255, 255); // Crisp White
        private int _trackHeight = 3;
        private int _thumbSize = 8;

        public event EventHandler<float> ValueChanged;
        public event EventHandler<float> SeekCommitted;

        public MusicTrackBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(420, 20);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        [Category("Behavior")]
        [DefaultValue(0.0f)]
        public float Value
        {
            get { return _value; }
            set
            {
                float clamped = Math.Max(0.0f, Math.Min(1.0f, value));
                if (Math.Abs(_value - clamped) > 0.0001f)
                {
                    _value = clamped;
                    Invalidate();
                    if (ValueChanged != null)
                    {
                        ValueChanged(this, _value);
                    }
                }
            }
        }

        [Category("Appearance")]
        public Color PlayedColor
        {
            get { return _playedColor; }
            set { _playedColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color UnplayedColor
        {
            get { return _unplayedColor; }
            set { _unplayedColor = value; Invalidate(); }
        }

        public bool IsDragging
        {
            get { return _isDragging; }
        }

        private int TrackMargin
        {
            get { return _thumbSize / 2 + 1; }
        }

        private int UsableWidth
        {
            get { return Width - 2 * TrackMargin; }
        }

        private float GetValueFromX(int x)
        {
            if (UsableWidth <= 0) return 0f;
            float v = (float)(x - TrackMargin) / UsableWidth;
            return Math.Max(0.0f, Math.Min(1.0f, v));
        }

        private int GetXFromValue(float v)
        {
            return TrackMargin + (int)(v * UsableWidth);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                Capture = true;
                Value = GetValueFromX(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                Value = GetValueFromX(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                Capture = false;
                if (SeekCommitted != null)
                {
                    SeekCommitted(this, _value);
                }
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
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackY = (Height - _trackHeight) / 2;
            int startX = TrackMargin;
            int endX = Width - TrackMargin;
            int thumbX = GetXFromValue(_value);

            // 1. Draw played track (Red)
            if (thumbX > startX)
            {
                using (Pen pen = new Pen(_playedColor, _trackHeight))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Flat;
                    g.DrawLine(pen, startX, trackY + _trackHeight / 2, thumbX, trackY + _trackHeight / 2);
                }
            }

            // 2. Draw unplayed track (White)
            if (thumbX < endX)
            {
                using (Pen pen = new Pen(_unplayedColor, _trackHeight))
                {
                    pen.StartCap = (thumbX <= startX) ? LineCap.Round : LineCap.Flat;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, thumbX, trackY + _trackHeight / 2, endX, trackY + _trackHeight / 2);
                }
            }

            // 3. Draw thumb knob (white circle with small dark center dot)
            int currentThumbSize = (_isHovered || _isDragging) ? _thumbSize + 2 : _thumbSize;
            int thumbRadius = currentThumbSize / 2;
            int thumbCenterY = Height / 2;
            Rectangle thumbRect = new Rectangle(thumbX - thumbRadius, thumbCenterY - thumbRadius, currentThumbSize, currentThumbSize);

            // Outer white fill
            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                g.FillEllipse(brush, thumbRect);
            }

            // Small center dot
            int dotSize = Math.Max(2, currentThumbSize / 3);
            int dotRadius = dotSize / 2;
            Rectangle dotRect = new Rectangle(thumbX - dotRadius, thumbCenterY - dotRadius, dotSize, dotSize);
            using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
            {
                g.FillEllipse(dotBrush, dotRect);
            }
        }
    }
}
