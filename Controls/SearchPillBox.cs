using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class SearchPillBox : Control
    {
        internal class CueTextBox : TextBox
        {
            private const int WM_PAINT = 0xF;
            public string CueBannerText { get; set; } = string.Empty;
            public Color CueColor { get; set; } = Color.FromArgb(145, 135, 175);

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg == WM_PAINT && string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(CueBannerText))
                {
                    using (Graphics g = Graphics.FromHwnd(Handle))
                    {
                        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding;
                        Color col = Focused ? Color.FromArgb(115, 105, 140) : CueColor;
                        TextRenderer.DrawText(g, CueBannerText, Font, new Rectangle(2, 0, Width - 4, Height), col, flags);
                    }
                }
            }
        }

        private CueTextBox _textBox;
        private string _placeholderText = "Tìm bài hát, nghệ sĩ hoặc dán link...";
        private Color _pillBgColor = Color.FromArgb(28, 23, 53);
        private Color _borderColorIdle = Color.FromArgb(60, 48, 96);
        private Color _borderColorFocused = Color.FromArgb(255, 45, 133);
        private Color _textColor = Color.White;

        private System.Windows.Forms.Timer _debounceTimer;
        private Rectangle _clearBtnRect = Rectangle.Empty;
        private Rectangle _submitBtnRect = Rectangle.Empty;
        private Rectangle _pasteBtnRect = Rectangle.Empty;
        private bool _isHoveringClear = false;
        private bool _isHoveringSubmit = false;
        private bool _isHoveringPaste = false;

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
            Size = new Size(380, 36);
            DoubleBuffered = true;

            _debounceTimer = new System.Windows.Forms.Timer();
            _debounceTimer.Interval = 260;
            _debounceTimer.Tick += DebounceTimer_Tick;

            _textBox = new CueTextBox();
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.BackColor = _pillBgColor;
            _textBox.ForeColor = _textColor;
            _textBox.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            _textBox.TextAlign = HorizontalAlignment.Left;
            _textBox.CueBannerText = _placeholderText;

            _textBox.HandleCreated += (s, e) => UpdateCueBanner();
            _textBox.GotFocus += (s, e) => { _textBox.Invalidate(); Invalidate(); };
            _textBox.LostFocus += (s, e) => { _textBox.Invalidate(); Invalidate(); };
            _textBox.KeyDown += TextBox_KeyDown;
            _textBox.TextChanged += TextBox_TextChanged;

            Controls.Add(_textBox);
            UpdateLayoutPositions();
        }

        private void UpdateCueBanner()
        {
            if (_textBox != null)
            {
                _textBox.CueBannerText = _placeholderText ?? string.Empty;
                _textBox.Invalidate();
            }
        }

        [Category("Appearance")]
        public string PlaceholderText
        {
            get => _placeholderText;
            set
            {
                _placeholderText = value ?? string.Empty;
                UpdateCueBanner();
            }
        }

        [Category("Appearance")]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public override string Text
        {
            get => _textBox?.Text ?? string.Empty;
            set
            {
                if (_textBox != null)
                {
                    _textBox.Text = value ?? string.Empty;
                    UpdateLayoutPositions();
                    Invalidate();
                }
            }
        }

        public bool HasRealText => !string.IsNullOrWhiteSpace(_textBox?.Text);

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
            UpdateLayoutPositions();
            Invalidate();

            string raw = _textBox.Text;
            string query = raw.Trim();

            if (string.IsNullOrEmpty(query))
            {
                _debounceTimer.Stop();
                SearchCleared?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (IsUrl(query))
            {
                _debounceTimer.Stop();
                SearchCleared?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Word / Phrase search: require at least 2 characters before suggesting
            if (query.Length < 2)
            {
                _debounceTimer.Stop();
                SearchCleared?.Invoke(this, EventArgs.Empty);
                return;
            }

            // If user just typed a space, a word was completed -> trigger quickly (120ms)
            if (raw.EndsWith(" "))
            {
                _debounceTimer.Stop();
                _debounceTimer.Interval = 100;
                _debounceTimer.Start();
            }
            else
            {
                // In the middle of typing a word: wait 260ms before querying suggestions
                _debounceTimer.Stop();
                _debounceTimer.Interval = 260;
                _debounceTimer.Start();
            }
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            if (HasRealText && !IsUrl(_textBox.Text))
            {
                string query = _textBox.Text.Trim();
                if (query.Length >= 2)
                {
                    SearchQueryDebounced?.Invoke(this, query);
                }
            }
        }

        public void RemoveFocus()
        {
            if (_textBox != null)
            {
                _textBox.SelectionLength = 0;
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

            int leftPadding = 34; // space for magnifying glass
            int pasteW = (Width >= 280) ? 44 : 0;
            int rightControlsWidth = 28 + (pasteW > 0 ? pasteW + 6 : 0); // space for submit arrow (➔) and paste pill

            if (HasRealText)
            {
                rightControlsWidth += 24; // space for clear button (✕)
            }

            int tbHeight = _textBox.PreferredHeight;
            int tbY = Math.Max(2, (Height - tbHeight) / 2);
            _textBox.Location = new Point(leftPadding, tbY);
            _textBox.Width = Math.Max(10, Width - leftPadding - rightControlsWidth);

            int curX = Width - 8;

            // Paste button ("Dán") on the far right if width >= 280
            if (pasteW > 0)
            {
                int pasteH = Math.Min(22, Height - 8);
                curX -= pasteW;
                _pasteBtnRect = new Rectangle(curX, (Height - pasteH) / 2, pasteW, pasteH);
                curX -= 6;
            }
            else
            {
                _pasteBtnRect = Rectangle.Empty;
            }

            // Submit arrow button (7px right margin)
            int btnH = Math.Min(22, Height - 8);
            curX -= (btnH + 2);
            _submitBtnRect = new Rectangle(curX, (Height - btnH) / 2, btnH, btnH);

            // Clear button
            if (HasRealText)
            {
                int clrH = Math.Min(18, Height - 10);
                curX -= (clrH + 4);
                _clearBtnRect = new Rectangle(curX, (Height - clrH) / 2, clrH, clrH);
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
            bool hoverPaste = _pasteBtnRect.Contains(e.Location);

            if (hoverClear != _isHoveringClear || hoverSubmit != _isHoveringSubmit || hoverPaste != _isHoveringPaste)
            {
                _isHoveringClear = hoverClear;
                _isHoveringSubmit = hoverSubmit;
                _isHoveringPaste = hoverPaste;
                Cursor = (hoverClear || hoverSubmit || hoverPaste) ? Cursors.Hand : Cursors.IBeam;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isHoveringClear || _isHoveringSubmit || _isHoveringPaste)
            {
                _isHoveringClear = false;
                _isHoveringSubmit = false;
                _isHoveringPaste = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_pasteBtnRect.Contains(e.Location))
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            string clip = Clipboard.GetText().Trim();
                            if (!string.IsNullOrEmpty(clip))
                            {
                                Text = clip;
                                RemoveFocus();
                                if (IsUrl(clip))
                                {
                                    UrlSubmitted?.Invoke(this, clip);
                                }
                                else
                                {
                                    SearchSubmitted?.Invoke(this, clip);
                                }
                            }
                        }
                    }
                    catch { }
                    return;
                }
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

            Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);
            int d = Math.Max(4, rect.Height);

            bool isFocused = _textBox.Focused;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(rect.X, rect.Y, d, d, 90, 180);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
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
            DrawMagnifyingGlass(g, 13, (Height - 12) / 2, isFocused || HasRealText);

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

            // Draw Paste Button ("Dán")
            if (!_pasteBtnRect.IsEmpty)
            {
                using (GraphicsPath pastePath = GetRoundedRect(_pasteBtnRect, _pasteBtnRect.Height / 2))
                {
                    Color bgCol = _isHoveringPaste ? Color.FromArgb(220, 255, 45, 133) : Color.FromArgb(70, 160, 60, 200);
                    Color borderCol = _isHoveringPaste ? Color.FromArgb(255, 80, 180) : Color.FromArgb(90, 180, 90, 220);
                    using (SolidBrush bgBrush = new SolidBrush(bgCol))
                    {
                        g.FillPath(bgBrush, pastePath);
                    }
                    using (Pen bPen = new Pen(borderCol, 1f))
                    {
                        g.DrawPath(bPen, pastePath);
                    }
                    using (Font pFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (SolidBrush pBrush = new SolidBrush(Color.White))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("Dán", pFont, pBrush, _pasteBtnRect, sf);
                    }
                }
            }
        }

        private static GraphicsPath GetRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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
