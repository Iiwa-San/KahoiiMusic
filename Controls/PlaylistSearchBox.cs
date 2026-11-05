using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class PlaylistSearchBox : Panel
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        private TextBox _textBox;
        private Label _lblClear;
        private bool _isFocused = false;
        private string _cueBanner = "";

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
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(28, 25, 42);

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(28, 25, 42),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                Location = new Point(22, 3),
                Height = 16
            };

            _lblClear = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 150, 165),
                Size = new Size(16, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _lblClear.MouseEnter += (s, e) => _lblClear.ForeColor = Color.FromArgb(255, 120, 120);
            _lblClear.MouseLeave += (s, e) => _lblClear.ForeColor = Color.FromArgb(150, 150, 165);
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
                SearchTextChanged?.Invoke(this, _textBox.Text.Trim());
            };

            this.Controls.Add(_textBox);
            this.Controls.Add(_lblClear);

            this.HandleCreated += (s, e) => UpdateCueBanner();
            this.Resize += (s, e) => LayoutControls();
            this.MouseDown += (s, e) => _textBox.Focus();
            LayoutControls();
        }

        public void SetCueBanner(string banner)
        {
            _cueBanner = banner;
            UpdateCueBanner();
        }

        private void UpdateCueBanner()
        {
            if (_textBox != null && _textBox.IsHandleCreated && !string.IsNullOrEmpty(_cueBanner))
            {
                try
                {
                    SendMessage(_textBox.Handle, EM_SETCUEBANNER, 1, _cueBanner);
                }
                catch { }
            }
        }

        private void LayoutControls()
        {
            int clearW = _lblClear.Visible ? 18 : 6;
            _textBox.Width = Math.Max(20, this.Width - 26 - clearW);
            _lblClear.Location = new Point(this.Width - 19, (this.Height - 16) / 2);
            _textBox.Location = new Point(22, (this.Height - _textBox.PreferredHeight) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw rounded border
            Color borderColor = _isFocused ? Color.FromArgb(168, 85, 247) : Color.FromArgb(45, 255, 255, 255);
            using (GraphicsPath path = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 4))
            using (Pen pen = new Pen(borderColor, 1))
            {
                g.DrawPath(pen, path);
            }

            // Draw search icon 🔍
            using (Font iconFont = new Font("Segoe UI", 7.5F))
            using (SolidBrush iconBrush = new SolidBrush(Color.FromArgb(150, 150, 165)))
            {
                g.DrawString("🔍", iconFont, iconBrush, 4, (Height - 14) / 2);
            }
        }

        private static GraphicsPath GetRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) return path;
            int maxRadius = Math.Min(r.Width, r.Height) / 2;
            radius = Math.Max(1, Math.Min(radius, maxRadius));
            int d = radius * 2;
            if (d > 0 && r.Width >= d && r.Height >= d)
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
            }
            else
            {
                path.AddRectangle(r);
            }
            return path;
        }
    }
}
