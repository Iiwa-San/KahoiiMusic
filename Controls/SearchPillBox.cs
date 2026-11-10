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
        private string _placeholderText = "Tìm bài hát, dán link YouTube...";
        private Color _pillBgColor = Color.FromArgb(28, 23, 53);
        private Color _borderColorIdle = Color.FromArgb(60, 48, 96);
        private Color _borderColorFocused = Color.FromArgb(255, 45, 133);
        private Color _placeholderColor = Color.FromArgb(135, 125, 160);
        private Color _textColor = Color.White;
        private bool _isPlaceholderActive = true;
        private bool _alreadyFocused = false;

        private System.Windows.Forms.Timer _debounceTimer;
        private Rectangle _clearBtnRect;
        private Rectangle _submitBtnRect;
        private Rectangle _ytBadgeRect;
        private bool _isHoveringClear = false;
        private bool _isHoveringSubmit = false;

        public event EventHandler<string> UrlSubmitted;
        public event EventHandler<string> SearchSubmitted;
        public event EventHandler<string> SearchQueryDebounced;
        public event EventHandler SearchCleared;

        public SearchPillBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(380, 24);
            DoubleBuffered = true;

            _debounceTimer = new System.Windows.Forms.Timer();
            _debounceTimer.Interval = 320;
            _debounceTimer.Tick += DebounceTimer_Tick;

            _textBox = new TextBox();
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.BackColor = _pillBgColor;
            _textBox.ForeColor = _placeholderColor;
            _textBox.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            _textBox.Text = _placeholderText;
            _textBox.TextAlign = HorizontalAlignment.Left;

            _textBox.GotFocus += TextBox_GotFocus;
            _textBox.MouseDown += TextBox_MouseDown;
            _textBox.LostFocus += TextBox_LostFocus;
            _textBox.KeyDown += TextBox_KeyDown;
            _textBox.TextChanged += TextBox_TextChanged;

            Controls.Add(_textBox);
            UpdateLayoutPositions();
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
                    _textBox.ForeColor = _textColor;
                }
                Invalidate();
            }
        }

        public bool HasRealText
        {
            get
            {
                return !_isPlaceholderActive && !string.IsNullOrWhiteSpace(_textBox.Text);
            }
        }

        private bool IsUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            return t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("youtu.be/", StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("www.youtube.com/", StringComparison.OrdinalIgnoreCase);
        }

        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            Invalidate();
            if (_isPlaceholderActive) return;

            string query = _textBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                _debounceTimer.Stop();
                SearchCleared?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (!IsUrl(query))
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
            else
            {
                _debounceTimer.Stop();
                SearchCleared?.Invoke(this, EventArgs.Empty);
            }
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            if (HasRealText && !IsUrl(_textBox.Text))
            {
                SearchQueryDebounced?.Invoke(this, _textBox.Text.Trim());
            }
        }

        private void TextBox_GotFocus(object sender, EventArgs e)
        {
            if (_isPlaceholderActive)
            {
                _isPlaceholderActive = false;
                _textBox.Text = string.Empty;
                _textBox.ForeColor = _textColor;
            }
            else
            {
                _textBox.ForeColor = _textColor;
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
            Invalidate();
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
                    _textBox.ForeColor = _textColor;
                }
                else
                {
                    _textBox.ForeColor = _textColor;
                    BeginInvoke(new Action(() =>
                    {
                        if (_textBox.Focused)
                        {
                            _textBox.SelectAll();
                        }
                    }));
                }
            }
            Invalidate();
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
                _textBox.ForeColor = Color.FromArgb(200, 195, 220);
            }
            _textBox.SelectionLength = 0;
            Invalidate();
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
                _textBox.ForeColor = Color.FromArgb(200, 195, 220);
            }

            Form parent = FindForm();
            if (parent != null)
            {
                parent.ActiveControl = null;
            }
            Invalidate();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _debounceTimer.Stop();
                string query = Text.Trim();
                if (IsUrl(query))
                {
                    RemoveFocus();
                    UrlSubmitted?.Invoke(this, query);
                }
                else if (!string.IsNullOrEmpty(query))
                {
                    SearchSubmitted?.Invoke(this, query);
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                _debounceTimer.Stop();
                RemoveFocus();
                SearchCleared?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayoutPositions();
        }

        private void UpdateLayoutPositions()
        {
            if (_textBox == null) return;

            int leftPadding = 30; // space for magnifying glass
            int rightControlsWidth = 26; // space for submit arrow

            if (HasRealText)
            {
                rightControlsWidth += 22; // space for clear button
            }

            if (Width >= 300)
            {
                rightControlsWidth += 56; // space for "YouTube" badge
            }

            int tbHeight = _textBox.PreferredHeight;
            int tbY = (Height - tbHeight) / 2;
            _textBox.Location = new Point(leftPadding, tbY);
            _textBox.Width = Math.Max(10, Width - leftPadding - rightControlsWidth);

            int curX = Width - 8;

            // Submit arrow button (7px right margin)
            curX -= 20;
            _submitBtnRect = new Rectangle(curX, (Height - 18) / 2, 18, 18);

            // YouTube badge
            if (Width >= 300)
            {
                curX -= 52;
                _ytBadgeRect = new Rectangle(curX, (Height - 16) / 2, 48, 16);
            }
            else
            {
                _ytBadgeRect = Rectangle.Empty;
            }

            // Clear button
            if (HasRealText)
            {
                curX -= 20;
                _clearBtnRect = new Rectangle(curX, (Height - 16) / 2, 16, 16);
            }
            else
            {
                _clearBtnRect = Rectangle.Empty;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hoverClear = _clearBtnRect.Contains(e.Location);
            bool hoverSubmit = _submitBtnRect.Contains(e.Location);

            if (hoverClear != _isHoveringClear || hoverSubmit != _isHoveringSubmit)
            {
                _isHoveringClear = hoverClear;
                _isHoveringSubmit = hoverSubmit;
                Cursor = (hoverClear || hoverSubmit) ? Cursors.Hand : Cursors.IBeam;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isHoveringClear || _isHoveringSubmit)
            {
                _isHoveringClear = false;
                _isHoveringSubmit = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_clearBtnRect.Contains(e.Location) && HasRealText)
                {
                    Text = string.Empty;
                    SearchCleared?.Invoke(this, EventArgs.Empty);
                    _textBox.Focus();
                    return;
                }
                if (_submitBtnRect.Contains(e.Location))
                {
                    string query = Text.Trim();
                    if (!string.IsNullOrEmpty(query))
                    {
                        if (IsUrl(query))
                        {
                            RemoveFocus();
                            UrlSubmitted?.Invoke(this, query);
                        }
                        else
                        {
                            SearchSubmitted?.Invoke(this, query);
                        }
                    }
                    return;
                }
            }
            _textBox.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int radius = Height;
            Rectangle rect = new Rectangle(1, 1, Width - 2, Height - 2);

            bool isFocused = _textBox.Focused;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(rect.X, rect.Y, radius, radius, 90, 180);
                path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 180);
                path.CloseFigure();

                // Fill pill background
                using (SolidBrush brush = new SolidBrush(_pillBgColor))
                {
                    g.FillPath(brush, path);
                }

                // Draw glowing or subtle border
                Color borderColor = isFocused ? _borderColorFocused : _borderColorIdle;
                float penWidth = isFocused ? 1.8f : 1.0f;
                using (Pen pen = new Pen(borderColor, penWidth))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Draw Magnifying Glass Icon (Vector)
            DrawMagnifyingGlass(g, 11, (Height - 12) / 2, isFocused || HasRealText);

            // Draw "YouTube" Badge
            if (!_ytBadgeRect.IsEmpty)
            {
                using (GraphicsPath bPath = new GraphicsPath())
                {
                    int bRadius = _ytBadgeRect.Height;
                    bPath.AddArc(_ytBadgeRect.X, _ytBadgeRect.Y, bRadius, bRadius, 90, 180);
                    bPath.AddArc(_ytBadgeRect.Right - bRadius, _ytBadgeRect.Y, bRadius, bRadius, 270, 180);
                    bPath.CloseFigure();

                    using (SolidBrush bBrush = new SolidBrush(Color.FromArgb(40, 255, 45, 133)))
                    {
                        g.FillPath(bBrush, bPath);
                    }
                    using (Pen bPen = new Pen(Color.FromArgb(90, 255, 45, 133), 1f))
                    {
                        g.DrawPath(bPen, bPath);
                    }
                }
                using (Font bFont = new Font("Segoe UI", 7.0f, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(255, 100, 165)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("YouTube", bFont, textBrush, _ytBadgeRect, sf);
                }
            }

            // Draw Clear button (✕)
            if (HasRealText && !_clearBtnRect.IsEmpty)
            {
                Color clrColor = _isHoveringClear ? Color.White : Color.FromArgb(150, 140, 175);
                using (Font cFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                using (SolidBrush brush = new SolidBrush(clrColor))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("✕", cFont, brush, _clearBtnRect, sf);
                }
            }

            // Draw Submit Arrow Button (➔)
            if (!_submitBtnRect.IsEmpty)
            {
                Color arrColor = _isHoveringSubmit ? Color.FromArgb(255, 45, 133) : Color.FromArgb(160, 150, 190);
                if (isFocused || HasRealText) arrColor = _isHoveringSubmit ? Color.White : Color.FromArgb(255, 45, 133);

                using (Font aFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
                using (SolidBrush brush = new SolidBrush(arrColor))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("➔", aFont, brush, _submitBtnRect, sf);
                }
            }
        }

        private void DrawMagnifyingGlass(Graphics g, int x, int y, bool active)
        {
            Color iconColor = active ? Color.FromArgb(255, 45, 133) : Color.FromArgb(130, 120, 155);
            using (Pen pen = new Pen(iconColor, 1.8f))
            {
                // Circle
                g.DrawEllipse(pen, x, y, 7.5f, 7.5f);
                // Handle
                g.DrawLine(pen, x + 6.2f, y + 6.2f, x + 10.5f, y + 10.5f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_debounceTimer != null)
                {
                    _debounceTimer.Stop();
                    _debounceTimer.Dispose();
                    _debounceTimer = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
