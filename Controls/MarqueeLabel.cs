using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class MarqueeLabel : Control
    {
        private string _text = string.Empty;
        private Color _textColor = Color.White;
        private Color _shadowColor = Color.FromArgb(140, 0, 0, 0);
        private float _scrollOffset = 0f;
        private float _scrollSpeed = 1.0f;
        private int _pauseCounter = 60; // ~1.8s pause at start
        private const int PauseTicks = 60;
        private const float SeparatorGap = 60f;
        private Timer _scrollTimer;
        private float _measuredTextWidth = 0f;

        public MarqueeLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            _scrollTimer = new Timer();
            _scrollTimer.Interval = 30; // ~33 FPS for silky smooth marquee
            _scrollTimer.Tick += ScrollTimer_Tick;

            BackColor = Color.Transparent;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            Size = new Size(430, 22);
        }

        [Category("Appearance")]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public override string Text
        {
            get { return _text; }
            set
            {
                string newVal = value ?? string.Empty;
                if (_text != newVal)
                {
                    _text = newVal;
                    _scrollOffset = 0f;
                    _pauseCounter = PauseTicks;
                    RecalculateTextWidth();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        public Color TextColor
        {
            get { return _textColor; }
            set
            {
                if (_textColor != value)
                {
                    _textColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        public Color ShadowColor
        {
            get { return _shadowColor; }
            set
            {
                if (_shadowColor != value)
                {
                    _shadowColor = value;
                    Invalidate();
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateTextWidth();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RecalculateTextWidth();
        }

        private void RecalculateTextWidth()
        {
            if (_scrollTimer == null) return;

            if (string.IsNullOrEmpty(_text))
            {
                _measuredTextWidth = 0f;
                _scrollTimer.Stop();
                return;
            }

            using (Bitmap bmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                SizeF size = g.MeasureString(_text, Font, PointF.Empty, StringFormat.GenericTypographic);
                _measuredTextWidth = size.Width;
            }

            if (_measuredTextWidth > Width)
            {
                if (!_scrollTimer.Enabled)
                {
                    _scrollTimer.Start();
                }
            }
            else
            {
                _scrollTimer.Stop();
                _scrollOffset = 0f;
            }
        }

        private void ScrollTimer_Tick(object sender, EventArgs e)
        {
            if (_measuredTextWidth <= Width || string.IsNullOrEmpty(_text))
            {
                _scrollTimer.Stop();
                _scrollOffset = 0f;
                Invalidate();
                return;
            }

            if (_pauseCounter > 0)
            {
                _pauseCounter--;
                return;
            }

            _scrollOffset += _scrollSpeed;
            float totalCycle = _measuredTextWidth + SeparatorGap;
            if (_scrollOffset >= totalCycle)
            {
                _scrollOffset = 0f;
                _pauseCounter = PauseTicks; // Pause again at start
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (string.IsNullOrEmpty(_text)) return;

            int w = Width;
            int h = Height;

            using (SolidBrush shadowBrush = new SolidBrush(_shadowColor))
            using (SolidBrush textBrush = new SolidBrush(_textColor))
            {
                // If text fits comfortably within control, draw centered statically
                if (_measuredTextWidth <= w)
                {
                    using (StringFormat sfCenter = new StringFormat()
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap
                    })
                    {
                        RectangleF rectShadow = new RectangleF(1, 1, w, h);
                        RectangleF rectText = new RectangleF(0, 0, w, h);
                        g.DrawString(_text, Font, shadowBrush, rectShadow, sfCenter);
                        g.DrawString(_text, Font, textBrush, rectText, sfCenter);
                    }
                }
                else
                {
                    // Overflowing text: Draw marquee with smooth edge fading
                    GraphicsState state = g.Save();
                    g.SetClip(new Rectangle(0, 0, w, h));

                    float y = (h - Font.GetHeight(g)) / 2f;
                    float cycle = _measuredTextWidth + SeparatorGap;

                    using (StringFormat sfLeft = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap })
                    {
                        // Draw multiple copies across the view to loop seamlessly
                        for (float startX = -_scrollOffset; startX < w; startX += cycle)
                        {
                            if (startX + _measuredTextWidth > 0)
                            {
                                g.DrawString(_text, Font, shadowBrush, startX + 1f, y + 1f, sfLeft);
                                g.DrawString(_text, Font, textBrush, startX, y, sfLeft);
                            }
                        }
                    }

                    g.Restore(state);

                    // Soft edge fade on left and right borders (16px) for an ultra-premium feel
                    int fadeWidth = 16;
                    if (w > fadeWidth * 2)
                    {
                        // Left edge fade out
                        using (LinearGradientBrush leftFade = new LinearGradientBrush(
                            new Rectangle(0, 0, fadeWidth, h),
                            Color.FromArgb(180, 0, 0, 0),
                            Color.Transparent,
                            LinearGradientMode.Horizontal))
                        {
                            // Soft vignette
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_scrollTimer != null)
                {
                    _scrollTimer.Stop();
                    _scrollTimer.Dispose();
                    _scrollTimer = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
