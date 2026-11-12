using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class GradientPanel : EdgeTransparentPanel
    {
        private Color _topColor = Color.FromArgb(43, 16, 127);
        private Color _bottomColor = Color.FromArgb(255, 38, 128);
        private bool _isMaximizedTemplate = false;

        public event EventHandler TopColorChanged;
        public event EventHandler BottomColorChanged;

        public GradientPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsMaximizedTemplate
        {
            get => _isMaximizedTemplate;
            set
            {
                if (_isMaximizedTemplate != value)
                {
                    _isMaximizedTemplate = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        public Color TopColor
        {
            get { return _topColor; }
            set
            {
                if (_topColor != value)
                {
                    _topColor = value;
                    Invalidate();
                    if (TopColorChanged != null) TopColorChanged(this, EventArgs.Empty);
                }
            }
        }

        [Category("Appearance")]
        public Color BottomColor
        {
            get { return _bottomColor; }
            set
            {
                if (_bottomColor != value)
                {
                    _bottomColor = value;
                    Invalidate();
                    if (BottomColorChanged != null) BottomColorChanged(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            int w = ClientRectangle.Width;
            int h = ClientRectangle.Height;
            if (w <= 0 || h <= 0) return;

            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;

            if (_isMaximizedTemplate)
            {
                Rectangle gradientRect = new Rectangle(0, -1, w, h + 2);
                using (LinearGradientBrush lgb = new LinearGradientBrush(gradientRect, Color.Black, Color.Black, LinearGradientMode.Vertical))
                {
                    ColorBlend cb = new ColorBlend(4);
                    cb.Colors = new Color[] {
                        Color.FromArgb(32, 12, 75),       // Dark rich indigo top
                        Color.FromArgb(52, 20, 135),      // Vibrant purple
                        Color.FromArgb(185, 15, 140),     // Magenta transition
                        Color.FromArgb(250, 0, 145)       // Bright radiant hot pink at bottom
                    };
                    cb.Positions = new float[] { 0.0f, 0.28f, 0.70f, 1.0f };
                    lgb.InterpolationColors = cb;
                    e.Graphics.FillRectangle(lgb, ClientRectangle);
                }
                return;
            }

            if (BackColor == Color.Transparent)
            {
                base.OnPaintBackground(e);
                return;
            }

            Rectangle normRect = new Rectangle(0, -1, w, h + 2);
            using (LinearGradientBrush brush = new LinearGradientBrush(normRect, _topColor, _bottomColor, LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }
    }
}
