using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class SearchPillBox : Control
    {
        private TextBox _textBox;
        private string _placeholderText = "link youtube...";
        private Color _pillColor = Color.Black;
        private Color _placeholderColor = Color.FromArgb(128, 128, 138);
        private Color _focusedTextColor = Color.White;
        private Color _unfocusedTextColor = Color.FromArgb(165, 165, 175);
        private bool _isPlaceholderActive = true;
        private bool _alreadyFocused = false;

        public event EventHandler<string> UrlSubmitted;

        public SearchPillBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(360, 20);
            DoubleBuffered = true;

            _textBox = new TextBox();
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.BackColor = Color.Black;
            _textBox.ForeColor = _placeholderColor;
            _textBox.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            _textBox.Text = _placeholderText;
            _textBox.TextAlign = HorizontalAlignment.Left;

            _textBox.GotFocus += TextBox_GotFocus;
            _textBox.MouseDown += TextBox_MouseDown;
            _textBox.LostFocus += TextBox_LostFocus;
            _textBox.KeyDown += TextBox_KeyDown;

            Controls.Add(_textBox);
            UpdateTextBoxPosition();
        }

        [Category("Appearance")]
        public string PlaceholderText
        {
            get { return _placeholderText; }
            set
            {
                _placeholderText = value;
                if (_isPlaceholderActive)
                {
                    _textBox.Text = _placeholderText;
                    _textBox.ForeColor = _placeholderColor;
                }
            }
        }

        [Category("Appearance")]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public override string Text
        {
            get
            {
                return !_isPlaceholderActive ? _textBox.Text : string.Empty;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _isPlaceholderActive = true;
                    _textBox.Text = _placeholderText;
                    _textBox.ForeColor = _placeholderColor;
                }
                else
                {
                    _isPlaceholderActive = false;
                    _textBox.Text = value;
                    _textBox.ForeColor = _textBox.Focused ? _focusedTextColor : _unfocusedTextColor;
                }
            }
        }

        public bool HasRealText
        {
            get
            {
                return !_isPlaceholderActive && !string.IsNullOrEmpty(_textBox.Text);
            }
        }

        private void TextBox_GotFocus(object sender, EventArgs e)
        {
            if (_isPlaceholderActive)
            {
                _isPlaceholderActive = false;
                _textBox.Text = string.Empty;
                _textBox.ForeColor = _focusedTextColor;
            }
            else
            {
                _textBox.ForeColor = _focusedTextColor;
                if (!_alreadyFocused)
                {
                    _alreadyFocused = true;
                    BeginInvoke(new Action(() =>
                    {
                        if (_textBox.Focused)
                        {
                            _textBox.SelectAll();
                        }
                    }));
                }
            }
        }

        private void TextBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (!_alreadyFocused)
            {
                _alreadyFocused = true;
                if (_isPlaceholderActive)
                {
                    _isPlaceholderActive = false;
                    _textBox.Text = string.Empty;
                    _textBox.ForeColor = _focusedTextColor;
                }
                else
                {
                    _textBox.ForeColor = _focusedTextColor;
                    BeginInvoke(new Action(() =>
                    {
                        if (_textBox.Focused)
                        {
                            _textBox.SelectAll();
                        }
                    }));
                }
            }
        }

        private void TextBox_LostFocus(object sender, EventArgs e)
        {
            _alreadyFocused = false;
            if (string.IsNullOrWhiteSpace(_textBox.Text) || _isPlaceholderActive)
            {
                _isPlaceholderActive = true;
                _textBox.Text = _placeholderText;
                _textBox.ForeColor = _placeholderColor;
            }
            else
            {
                _textBox.ForeColor = _unfocusedTextColor;
            }
            _textBox.SelectionLength = 0;
        }

        public void RemoveFocus()
        {
            _alreadyFocused = false;
            _textBox.SelectionLength = 0;
            if (string.IsNullOrWhiteSpace(_textBox.Text) || _isPlaceholderActive)
            {
                _isPlaceholderActive = true;
                _textBox.Text = _placeholderText;
                _textBox.ForeColor = _placeholderColor;
            }
            else
            {
                _textBox.ForeColor = _unfocusedTextColor;
            }

            Form parent = FindForm();
            if (parent != null)
            {
                parent.ActiveControl = null;
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string url = Text.Trim();
                RemoveFocus();
                if (UrlSubmitted != null)
                {
                    UrlSubmitted(this, url);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateTextBoxPosition();
        }

        private void UpdateTextBoxPosition()
        {
            if (_textBox != null)
            {
                int padX = 14;
                int tbHeight = _textBox.PreferredHeight;
                int tbY = (Height - tbHeight) / 2;
                _textBox.Location = new Point(padX, tbY);
                _textBox.Width = Math.Max(10, Width - 2 * padX);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw rounded pill background
            int radius = Height;
            Rectangle rect = new Rectangle(0, 0, Width, Height);

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(rect.X, rect.Y, radius, radius, 90, 180);
                path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 180);
                path.CloseFigure();

                using (SolidBrush brush = new SolidBrush(_pillColor))
                {
                    g.FillPath(brush, path);
                }
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            _textBox.Focus();
        }
    }
}
