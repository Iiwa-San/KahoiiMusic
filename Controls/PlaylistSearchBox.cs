using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class PlaylistSearchBox : Panel
    {
        private SearchPillBox.CueTextBox _textBox;
        private Label _lblClear;
        private bool _isFocused = false;
        private string _cueBanner = "";
        private readonly Color _pillBgColor = Color.FromArgb(28, 23, 53);

        public event EventHandler<string> SearchTextChanged;

        public string Query
        {
            get => _textBox?.Text?.Trim() ?? string.Empty;
            set
            {
                if (_textBox != null) _textBox.Text = value ?? string.Empty;
            }
        }

        public void Clear()
        {
            if (_textBox != null) _textBox.Text = string.Empty;
        }

        public PlaylistSearchBox(string cueBanner)
        {
            _cueBanner = cueBanner;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;

            _textBox = new SearchPillBox.CueTextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = _pillBgColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.75F, FontStyle.Regular),
                CueBannerText = _cueBanner,
                CueColor = Color.FromArgb(145, 135, 175)
            };

            _lblClear = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 140, 175),
                BackColor = Color.Transparent,
                Size = new Size(16, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _lblClear.MouseEnter += (s, e) => _lblClear.ForeColor = Color.FromArgb(255, 110, 185);
            _lblClear.MouseLeave += (s, e) => _lblClear.ForeColor = Color.FromArgb(150, 140, 175);
            _lblClear.Click += (s, e) =>
            {
                _textBox.Text = string.Empty;
                _textBox.Focus();
            };

            _textBox.GotFocus += (s, e) =>
            {
                _isFocused = true;
                Invalidate();
            };
            _textBox.LostFocus += (s, e) =>
            {
                _isFocused = false;
                Invalidate();
            };
            _textBox.TextChanged += (s, e) =>
            {
                _lblClear.Visible = !string.IsNullOrEmpty(_textBox.Text);
                LayoutControls();
                SearchTextChanged?.Invoke(this, _textBox.Text.Trim());
            };

            this.Controls.Add(_textBox);
            this.Controls.Add(_lblClear);

            this.Resize += (s, e) => LayoutControls();
            this.MouseDown += (s, e) => _textBox.Focus();
            LayoutControls();
        }

        public void SetCueBanner(string banner)
        {
            _cueBanner = banner;
            if (_textBox != null)
            {
                _textBox.CueBannerText = banner;
                _textBox.Invalidate();
            }
        }

        public void RemoveFocus()
        {
            if (_textBox != null && _textBox.Focused)
            {
                Form f = this.FindForm();
                if (f != null) f.ActiveControl = null;
            }
        }

        private void LayoutControls()
        {
            int clearW = (_lblClear != null && _lblClear.Visible) ? 20 : 6;
            if (_textBox != null)
            {
                _textBox.Width = Math.Max(20, this.Width - 28 - clearW);
                _textBox.Location = new Point(27, Math.Max(2, (this.Height - _textBox.PreferredHeight) / 2));
            }
            if (_lblClear != null)
            {
                _lblClear.Location = new Point(this.Width - 23, Math.Max(2, (this.Height - 16) / 2));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);
            int d = Math.Max(4, rect.Height);

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(rect.X, rect.Y, d, d, 90, 180);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
                path.CloseFigure();

                // Fill capsule background
                using (SolidBrush brush = new SolidBrush(_pillBgColor))
                {
                    g.FillPath(brush, path);
                }

                // Draw glowing or subtle cyber gradient border
                Color gradStart = _isFocused ? Color.FromArgb(180, 110, 255) : Color.FromArgb(120, 150, 95, 230);
                Color gradEnd = _isFocused ? Color.FromArgb(255, 45, 133) : Color.FromArgb(160, 245, 45, 125);
                float penWidth = _isFocused ? 1.6f : 1.1f;
                using (LinearGradientBrush gradBrush = new LinearGradientBrush(rect, gradStart, gradEnd, LinearGradientMode.Horizontal))
                using (Pen pen = new Pen(gradBrush, penWidth))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Draw crisp vector magnifying glass icon
            Color iconColor = _isFocused ? Color.FromArgb(255, 120, 185) : Color.FromArgb(170, 155, 195);
            using (Pen iconPen = new Pen(iconColor, 1.5f))
            {
                float cy = Height / 2f;
                float r = 3.5f;
                float cx = 13.5f;
                float cyCircle = cy - 1.2f;
                g.DrawEllipse(iconPen, cx - r, cyCircle - r, r * 2f, r * 2f);
                g.DrawLine(iconPen, cx + 2.4f, cyCircle + 2.4f, cx + 5.5f, cyCircle + 5.5f);
            }
        }
    }
}
