using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public enum RomanizationDisplayMode
    {
        Original = 0,      // Chỉ hiển thị chữ gốc
        Dual = 1,          // Song ngữ: Chữ gốc ở trên, phiên âm Latinh (Romaji/Pinyin) ở dưới
        RomanizedOnly = 2  // Chỉ hiển thị phiên âm Latinh
    }

    public class LyricsPanel : Control
    {
        private LyricsData _lyrics;
        private TimeSpan _currentPosition = TimeSpan.Zero;
        private TimeSpan _totalDuration = TimeSpan.Zero;
        private int _currentIndex = -1;
        private float _currentScrollY = 0f;
        private float _targetScrollY = 0f;
        private readonly Timer _scrollTimer;

        private bool _isUserScrolling = false;
        private DateTime _lastUserScrollTime = DateTime.MinValue;
        private int _hoveredLineIndex = -1;
        private bool _isRomaziBtnHovered = false;
        private bool _isReloadBtnHovered = false;

        private RomanizationDisplayMode _romanizationMode = RomanizationDisplayMode.Dual;
        private int _delayOffsetMs = 220;
        private Rectangle _romaziBtnRect = Rectangle.Empty;
        private Rectangle _reloadBtnRect = Rectangle.Empty;
        private KahoiiMusic.Services.DynamicThemePalette _theme = KahoiiMusic.Services.ColorExtractorService.GetDefaultTheme();

        public void SetTheme(KahoiiMusic.Services.DynamicThemePalette theme)
        {
            if (theme != null)
            {
                _theme = theme;
                Invalidate();
            }
        }

        public event Action<TimeSpan> SeekRequested;
        public event EventHandler ReloadRequested;

        private class LayoutLine
        {
            public int Index;
            public LyricLine Model;
            public string DisplayMainText;
            public string DisplaySubText;
            public float Y;
            public float Height;
            public float MainTextHeight;
        }

        private readonly List<LayoutLine> _layoutLines = new List<LayoutLine>();
        private float _totalContentHeight = 0f;
        private bool _isLoading = false;
        private Timer _dotTimer;
        private int _dotCount = 0;

        public LyricsPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            DoubleBuffered = true;
            Size = new Size(292, 190);

            try
            {
                _delayOffsetMs = HistoryService.LyricsDelayOffsetMs;
                _romanizationMode = (RomanizationDisplayMode)HistoryService.LyricsRomanizationMode;
            }
            catch { }

            _scrollTimer = new Timer();
            _scrollTimer.Interval = 15; // ~60 FPS
            _scrollTimer.Tick += ScrollTimer_Tick;

            _dotTimer = new Timer();
            _dotTimer.Interval = 400;
            _dotTimer.Tick += (s, e) =>
            {
                _dotCount++;
                if (_isLoading) Invalidate();
            };

            var cms = new ContextMenuStrip();
            var miReload = new ToolStripMenuItem("↻ Tải lại lời bài hát (Làm mới cache)");
            miReload.Click += (s, e) => ReloadRequested?.Invoke(this, EventArgs.Empty);
            cms.Items.Add(miReload);
            ContextMenuStrip = cms;
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    if (_isLoading)
                    {
                        if (!_dotTimer.Enabled) _dotTimer.Start();
                    }
                    else
                    {
                        if (_dotTimer.Enabled) _dotTimer.Stop();
                    }
                    Invalidate();
                }
            }
        }

        public void RebuildAndRefresh()
        {
            RebuildLayout();
            RecalculateTargetScroll();
            Invalidate();
        }

        [Category("Behavior")]
        [DefaultValue(220)]
        public int DelayOffsetMs
        {
            get { return _delayOffsetMs; }
            set
            {
                if (_delayOffsetMs != value)
                {
                    _delayOffsetMs = value;
                    UpdateActiveLine();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(RomanizationDisplayMode.Dual)]
        public RomanizationDisplayMode RomanizationMode
        {
            get { return _romanizationMode; }
            set
            {
                if (_romanizationMode != value)
                {
                    _romanizationMode = value;
                    try { HistoryService.LyricsRomanizationMode = (int)value; } catch { }
                    RebuildLayout();
                    RecalculateTargetScroll();
                    Invalidate();
                }
            }
        }

        [Category("Data")]
        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (_currentIndex != value)
                {
                    _currentIndex = value;
                    RecalculateTargetScroll();
                    Invalidate();
                }
            }
        }

        [Category("Data")]
        public LyricsData Lyrics
        {
            get { return _lyrics; }
            set
            {
                _lyrics = value;
                _isUserScrolling = false;
                RebuildLayout();

                if (_lyrics != null && _lyrics.HasLyrics && _lyrics.IsSynced && _currentPosition > TimeSpan.Zero)
                {
                    // Instant target jump: align directly with current audio position!
                    TimeSpan effectivePos = _currentPosition - TimeSpan.FromMilliseconds(_delayOffsetMs);
                    if (effectivePos < TimeSpan.Zero) effectivePos = TimeSpan.Zero;

                    int targetIdx = -1;
                    for (int i = 0; i < _lyrics.Lines.Count; i++)
                    {
                        if (_lyrics.Lines[i].Timestamp <= effectivePos) targetIdx = i;
                        else break;
                    }
                    _currentIndex = targetIdx;
                    RecalculateTargetScroll();
                    _currentScrollY = _targetScrollY; // Direct jump, no initial scrolling lag!
                }
                else
                {
                    _currentIndex = -1;
                    _currentScrollY = 0f;
                    _targetScrollY = 0f;
                    RecalculateTargetScroll();
                }
                Invalidate();
            }
        }

        [Category("Data")]
        public TimeSpan TotalDuration
        {
            get { return _totalDuration; }
            set
            {
                _totalDuration = value;
            }
        }

        [Category("Data")]
        public TimeSpan CurrentPosition
        {
            get { return _currentPosition; }
            set
            {
                _currentPosition = value;
                UpdateActiveLine();
            }
        }

        private void ScrollTimer_Tick(object sender, EventArgs e)
        {
            // Cooldown check: restore auto-scroll after 2 seconds of mouse inactivity
            if (_isUserScrolling && (DateTime.UtcNow - _lastUserScrollTime).TotalMilliseconds > 2000)
            {
                _isUserScrolling = false;
                RecalculateTargetScroll();
            }

            float diff = _targetScrollY - _currentScrollY;
            if (Math.Abs(diff) > 0.3f)
            {
                _currentScrollY += diff * 0.22f;
                Invalidate();
            }
            else
            {
                _currentScrollY = _targetScrollY;
                if (!_isUserScrolling)
                {
                    _scrollTimer.Stop();
                }
                Invalidate();
            }
        }

        private void UpdateActiveLine()
        {
            if (_lyrics == null || !_lyrics.HasLyrics || !_lyrics.IsSynced || _lyrics.Lines == null || _lyrics.Lines.Count == 0)
            {
                return;
            }

            // If user was scrolling and 2s passed, resume auto-tracking
            if (_isUserScrolling && (DateTime.UtcNow - _lastUserScrollTime).TotalMilliseconds > 2000)
            {
                _isUserScrolling = false;
            }

            // Apply compensation for Bluetooth audio / buffer latency
            TimeSpan effectivePos = _currentPosition - TimeSpan.FromMilliseconds(_delayOffsetMs);
            if (effectivePos < TimeSpan.Zero) effectivePos = TimeSpan.Zero;

            int newIdx = -1;
            for (int i = 0; i < _lyrics.Lines.Count; i++)
            {
                if (_lyrics.Lines[i].Timestamp <= effectivePos)
                {
                    newIdx = i;
                }
                else
                {
                    break;
                }
            }

            if (newIdx != _currentIndex)
            {
                _currentIndex = newIdx;
                if (!_isUserScrolling)
                {
                    RecalculateTargetScroll();
                }
                Invalidate();
            }
        }

        private void RecalculateTargetScroll()
        {
            if (_layoutLines.Count == 0) return;

            // Pin active line firmly at upper-middle center (38% from top)
            float viewFocusY = Height * 0.38f;

            if (_currentIndex >= 0 && _currentIndex < _layoutLines.Count)
            {
                var line = _layoutLines[_currentIndex];
                float lineCenter = line.Y + line.Height / 2f;
                _targetScrollY = viewFocusY - lineCenter;
            }
            else
            {
                // Before track begins, seat line 0 nicely near focus center
                var firstLine = _layoutLines[0];
                _targetScrollY = viewFocusY - (firstLine.Y + firstLine.Height / 2f);
            }

            if (!_scrollTimer.Enabled)
            {
                _scrollTimer.Start();
            }
        }

        private void RebuildLayout()
        {
            _layoutLines.Clear();
            _totalContentHeight = 0f;

            if (_lyrics == null || !_lyrics.HasLyrics || _lyrics.Lines == null || _lyrics.Lines.Count == 0)
            {
                return;
            }

            float fontScale = GetFontScale();
            using (Bitmap dummyBmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(dummyBmp))
            using (Font activeFont = new Font("Segoe UI", 11.5f * fontScale, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 8.5f * fontScale, FontStyle.Italic))
            {
                float curY = 12f;
                int maxTextWidth = Math.Max(100, Width - 24);

                for (int i = 0; i < _lyrics.Lines.Count; i++)
                {
                    var model = _lyrics.Lines[i];
                    string mainText = string.IsNullOrWhiteSpace(model.Text) ? "♪" : model.Text.Trim();
                    string subText = null;

                    if (_romanizationMode == RomanizationDisplayMode.RomanizedOnly && !string.IsNullOrWhiteSpace(model.RomanizedText))
                    {
                        mainText = model.RomanizedText.Trim();
                    }
                    else if (_romanizationMode == RomanizationDisplayMode.Dual && !string.IsNullOrWhiteSpace(model.RomanizedText))
                    {
                        subText = model.RomanizedText.Trim();
                    }

                    SizeF mainSize = g.MeasureString(mainText, activeFont, maxTextWidth);
                    float h = Math.Max(32f * fontScale, mainSize.Height + 8f * fontScale);

                    if (!string.IsNullOrEmpty(subText))
                    {
                        SizeF subSize = g.MeasureString(subText, subFont, maxTextWidth);
                        h += subSize.Height + 4f * fontScale;
                    }

                    _layoutLines.Add(new LayoutLine
                    {
                        Index = i,
                        Model = model,
                        DisplayMainText = mainText,
                        DisplaySubText = subText,
                        Y = curY,
                        Height = h,
                        MainTextHeight = mainSize.Height
                    });

                    curY += h;
                }

                _totalContentHeight = curY + 20f;
            }

            RecalculateTargetScroll();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _romaziBtnRect = new Rectangle(Width - 94, 6, 88, 20);
            RebuildLayout();
            RecalculateTargetScroll();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _isUserScrolling = true;
            _lastUserScrollTime = DateTime.UtcNow;

            float step = (e.Delta > 0 ? 1 : -1) * 45f;
            _targetScrollY += step;

            // Constrain manual scroll limits
            float maxScroll = 60f;
            float minScroll = -(Math.Max(0, _totalContentHeight - Height / 2f));
            if (_targetScrollY > maxScroll) _targetScrollY = maxScroll;
            if (_targetScrollY < minScroll) _targetScrollY = minScroll;

            if (!_scrollTimer.Enabled)
            {
                _scrollTimer.Start();
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Check Romaji/Pinyin button hover
            bool overBtn = HasRomanizationSupport && _romaziBtnRect.Contains(e.Location);
            if (overBtn != _isRomaziBtnHovered)
            {
                _isRomaziBtnHovered = overBtn;
                Invalidate();
            }

            // Check Reload button hover
            bool overReload = _reloadBtnRect.Width > 0 && _reloadBtnRect.Contains(e.Location);
            if (overReload != _isReloadBtnHovered)
            {
                _isReloadBtnHovered = overReload;
                Invalidate();
            }

            int hovered = -1;
            float mouseY = e.Y - _currentScrollY;

            for (int i = 0; i < _layoutLines.Count; i++)
            {
                var line = _layoutLines[i];
                if (mouseY >= line.Y && mouseY <= line.Y + line.Height)
                {
                    hovered = i;
                    break;
                }
            }

            if (hovered != _hoveredLineIndex)
            {
                _hoveredLineIndex = hovered;
                Invalidate();
            }

            Cursor = (overBtn || overReload || (hovered >= 0 && _lyrics != null && _lyrics.HasLyrics)) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredLineIndex = -1;
            _isRomaziBtnHovered = false;
            _isReloadBtnHovered = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            // 0. Click Reload button
            if (_reloadBtnRect.Width > 0 && _reloadBtnRect.Contains(e.Location))
            {
                ReloadRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            // 1. Click Romaji / Pinyin pill button
            if (HasRomanizationSupport && _romaziBtnRect.Contains(e.Location))
            {
                // Cycle: Original (0) -> Dual (1) -> RomanizedOnly (2) -> Original (0)
                int nextMode = ((int)_romanizationMode + 1) % 3;
                RomanizationMode = (RomanizationDisplayMode)nextMode;
                return;
            }

            // 2. Click line to seek
            if (_lyrics != null && _lyrics.HasLyrics)
            {
                float mouseY = e.Y - _currentScrollY;
                for (int i = 0; i < _layoutLines.Count; i++)
                {
                    var line = _layoutLines[i];
                    if (mouseY >= line.Y && mouseY <= line.Y + line.Height)
                    {
                        if (line.Model.Timestamp > TimeSpan.Zero)
                        {
                            SeekRequested?.Invoke(line.Model.Timestamp);
                        }
                        _isUserScrolling = false;
                        _currentIndex = i;
                        RecalculateTargetScroll();
                        Invalidate();
                        break;
                    }
                }
            }
        }

        private bool HasRomanizationSupport
        {
            get
            {
                return _lyrics != null && !string.IsNullOrEmpty(_lyrics.DetectedLanguage);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float fontScale = GetFontScale();

            // Loading state
            if (_isLoading)
            {
                using (Font font = new Font("Segoe UI", 10.0f * fontScale, FontStyle.Regular))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(210, 245, 220, 255)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    string dots = new string('.', (_dotCount % 3) + 1);
                    g.DrawString("Đang tải lời bài hát" + dots, font, brush, ClientRectangle, sf);
                }
                return;
            }

            // Empty state
            if (_lyrics == null || !_lyrics.HasLyrics || _layoutLines.Count == 0)
            {
                using (Font font = new Font("Segoe UI", 9.5f * fontScale, FontStyle.Italic))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(140, 220, 215, 235)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Không có lời bài hát", font, brush, ClientRectangle, sf);
                }
                return;
            }

            int panelH = Height;
            int panelW = Width;

            // Draw Lyrics Lines
            using (Font activeFont = new Font("Segoe UI", 11.5f * fontScale, FontStyle.Bold))
            using (Font nearFont = new Font("Segoe UI", 10.0f * fontScale, FontStyle.Regular))
            using (Font farFont = new Font("Segoe UI", 9.0f * fontScale, FontStyle.Regular))
            using (Font activeSubFont = new Font("Segoe UI", 8.5f * fontScale, FontStyle.Italic))
            using (Font inactiveSubFont = new Font("Segoe UI", 8.0f * fontScale, FontStyle.Italic))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            {
                for (int i = 0; i < _layoutLines.Count; i++)
                {
                    var item = _layoutLines[i];
                    float renderY = item.Y + _currentScrollY;

                    // Clip check
                    if (renderY + item.Height < -20 || renderY > panelH + 20) continue;

                    int dist = (_lyrics != null && _lyrics.IsSynced && _currentIndex >= 0) ? Math.Abs(i - _currentIndex) : 0;
                    bool isActive = (_lyrics != null && _lyrics.IsSynced && i == _currentIndex);
                    bool isHovered = (i == _hoveredLineIndex);

                    Font drawFont;
                    Color textColor;
                    Color subColor;
                    Color glowColor = Color.Empty;

                    if (isActive)
                    {
                        // "Nhạc đến đâu lyrics sáng đến đó"
                        drawFont = activeFont;
                        textColor = Color.White;
                        glowColor = Color.FromArgb(140, _theme.AccentColor); // Theme glow
                        subColor = Color.FromArgb(235, 255, 255, 255);
                    }
                    else if (_lyrics == null || !_lyrics.IsSynced)
                    {
                        // Clean reading mode for unsynced plain text
                        drawFont = isHovered ? activeFont : nearFont;
                        textColor = isHovered ? Color.White : Color.FromArgb(215, 230, 240);
                        subColor = Color.FromArgb(160, 185, 215);
                    }
                    else if (dist == 1)
                    {
                        drawFont = nearFont;
                        textColor = isHovered ? Color.White : Color.FromArgb(180, 230, 225, 240);
                        subColor = Color.FromArgb(140, 200, 195, 220);
                    }
                    else
                    {
                        drawFont = farFont;
                        float distFromCenter = Math.Abs(renderY + item.Height / 2f - panelH * 0.38f);
                        int alpha = Math.Max(35, (int)(135 - (distFromCenter / (panelH * 0.5f)) * 95));
                        textColor = isHovered ? Color.FromArgb(220, 255, 255, 255) : Color.FromArgb(alpha, 210, 205, 225);
                        subColor = Color.FromArgb(Math.Max(25, alpha - 20), 190, 185, 215);
                    }

                    RectangleF textRect = new RectangleF(10f, renderY, panelW - 16f, item.Height);

                    // Glow for active line
                    if (isActive && glowColor != Color.Empty)
                    {
                        using (SolidBrush glowBrush = new SolidBrush(glowColor))
                        {
                            RectangleF glowRect = new RectangleF(textRect.X - 1, textRect.Y - 1, textRect.Width, textRect.Height);
                            g.DrawString(item.DisplayMainText, drawFont, glowBrush, glowRect, sf);
                            glowRect.Offset(2, 2);
                            g.DrawString(item.DisplayMainText, drawFont, glowBrush, glowRect, sf);
                        }

                        // Left vertical active accent pill
                        using (SolidBrush pillBrush = new SolidBrush(_theme.AccentColor))
                        {
                            FillRoundedRect(g, pillBrush, 2, renderY + 4 * fontScale, 3 * fontScale, 14 * fontScale, 1.5f);
                        }
                    }

                    // Draw main line text
                    using (SolidBrush brush = new SolidBrush(textColor))
                    {
                        g.DrawString(item.DisplayMainText, drawFont, brush, textRect, sf);
                    }

                    // Draw Romanized subtext if in Dual mode
                    if (!string.IsNullOrEmpty(item.DisplaySubText))
                    {
                        float subY = renderY + item.MainTextHeight + 2f;
                        RectangleF subRect = new RectangleF(10f, subY, panelW - 16f, item.Height - item.MainTextHeight);
                        using (SolidBrush subBrush = new SolidBrush(subColor))
                        {
                            g.DrawString(item.DisplaySubText, isActive ? activeSubFont : inactiveSubFont, subBrush, subRect, sf);
                        }
                    }
                }
            }

            // Draw Romaji / Pinyin Toggle Pill Button in Top Right
            if (HasRomanizationSupport)
            {
                DrawRomanizationButton(g);
            }

            // Draw "non-synced" badge beside Romaji button if lyrics are not audio-synchronized
            if (_lyrics != null && !_lyrics.IsSynced)
            {
                DrawNonSyncedBadge(g);
            }

            // Draw Reload / Refresh button "↻" (allows user to re-fetch/clean cache anytime)
            if (_lyrics != null && _lyrics.HasLyrics)
            {
                DrawReloadButton(g);
            }
        }

        private void DrawReloadButton(Graphics g)
        {
            int btnW = 22;
            int btnH = 20;
            int btnY = 6;
            int rightBoundary = Width - 10;

            if (HasRomanizationSupport && _romaziBtnRect.Width > 0)
            {
                rightBoundary = Math.Min(rightBoundary, _romaziBtnRect.Left - 6);
            }
            if (_lyrics != null && !_lyrics.IsSynced)
            {
                int badgeW = 78;
                int badgeX = HasRomanizationSupport ? (_romaziBtnRect.Left - badgeW - 6) : (Width - badgeW - 10);
                rightBoundary = Math.Min(rightBoundary, badgeX - 6);
            }

            int btnX = rightBoundary - btnW;
            _reloadBtnRect = new Rectangle(btnX, btnY, btnW, btnH);

            Color borderColor = _isReloadBtnHovered ? Color.FromArgb(220, 200, 240) : Color.FromArgb(100, 160, 140, 200);
            Color fillColor = _isReloadBtnHovered ? Color.FromArgb(230, 60, 35, 90) : Color.FromArgb(200, 32, 20, 52);
            Color textCol = _isReloadBtnHovered ? Color.White : Color.FromArgb(210, 215, 235);

            using (GraphicsPath btnPath = CreateRoundedRectanglePath(_reloadBtnRect, 10))
            {
                using (SolidBrush fillBrush = new SolidBrush(fillColor))
                {
                    g.FillPath(fillBrush, btnPath);
                }
                using (Pen borderPen = new Pen(borderColor, 1.0f))
                {
                    g.DrawPath(borderPen, btnPath);
                }
            }

            using (Font font = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(textCol))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("↻", font, textBrush, _reloadBtnRect, sf);
            }
        }

        private void DrawNonSyncedBadge(Graphics g)
        {
            int badgeW = 78;
            int badgeH = 20;
            int badgeX = HasRomanizationSupport ? (_romaziBtnRect.Left - badgeW - 6) : (Width - badgeW - 10);
            int badgeY = 6;
            Rectangle badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);

            using (GraphicsPath path = CreateRoundedRectanglePath(badgeRect, 10))
            using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(200, 32, 20, 52)))
            using (Pen borderPen = new Pen(Color.FromArgb(140, 255, 175, 75), 1.0f))
            using (Font font = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(245, 235, 225)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
                g.DrawString("non-synced", font, textBrush, badgeRect, sf);
            }
        }

        private void DrawRomanizationButton(Graphics g)
        {
            if (_romaziBtnRect.Width <= 0)
            {
                _romaziBtnRect = new Rectangle(Width - 94, 6, 88, 20);
            }

            bool isJa = (_lyrics.DetectedLanguage == "ja");
            string btnLabel;
            Color borderColor;
            Color fillColor;
            Color textCol;

            if (_romanizationMode == RomanizationDisplayMode.Original)
            {
                btnLabel = isJa ? "Aa Romaji" : "Aa Pinyin";
                borderColor = _isRomaziBtnHovered ? Color.FromArgb(200, 255, 255, 255) : Color.FromArgb(100, 220, 220, 240);
                fillColor = _isRomaziBtnHovered ? Color.FromArgb(220, 45, 25, 75) : Color.FromArgb(200, 32, 18, 55);
                textCol = _isRomaziBtnHovered ? Color.White : Color.FromArgb(200, 220, 220, 240);
            }
            else if (_romanizationMode == RomanizationDisplayMode.Dual)
            {
                btnLabel = isJa ? "Romaji • 2 dòng" : "Pinyin • 2 dòng";
                borderColor = _theme.AccentColor;
                fillColor = _isRomaziBtnHovered
                    ? Color.FromArgb(230, KahoiiMusic.Services.ColorExtractorService.Lerp(_theme.AccentColor, Color.Black, 0.6f))
                    : Color.FromArgb(210, KahoiiMusic.Services.ColorExtractorService.Lerp(_theme.AccentColor, Color.Black, 0.75f));
                textCol = Color.White;
            }
            else
            {
                btnLabel = isJa ? "Romaji thuần" : "Pinyin thuần";
                borderColor = Color.FromArgb(0, 225, 255);
                fillColor = _isRomaziBtnHovered ? Color.FromArgb(230, 15, 65, 95) : Color.FromArgb(210, 15, 45, 75);
                textCol = Color.White;
            }

            using (GraphicsPath btnPath = CreateRoundedRectanglePath(_romaziBtnRect, 10))
            {
                using (SolidBrush fillBrush = new SolidBrush(fillColor))
                {
                    g.FillPath(fillBrush, btnPath);
                }
                using (Pen pen = new Pen(borderColor, 1.2f))
                {
                    g.DrawPath(pen, btnPath);
                }
            }

            using (Font btnFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(textCol))
            using (StringFormat btnSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(btnLabel, btnFont, textBrush, _romaziBtnRect, btnSf);
            }
        }

        private void FillRoundedRect(Graphics g, Brush brush, float x, float y, float width, float height, float radius)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                float d = radius * 2f;
                path.AddArc(x, y, d, d, 180, 90);
                path.AddArc(x + width - d, y, d, d, 270, 90);
                path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
                path.AddArc(x, y + height - d, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }

        private GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private const int WM_ERASEBKGND = 0x0014;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        private float GetFontScale()
        {
            float scaleH = 1.0f + Math.Max(0f, (Height - 200f) / 450f) * 0.70f;
            float scaleW = 1.0f + Math.Max(0f, (Width - 280f) / 500f) * 0.70f;
            float scale = Math.Min(scaleH, scaleW);
            return Math.Max(1.0f, Math.Min(1.80f, scale));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_scrollTimer != null)
                {
                    _scrollTimer.Stop();
                    _scrollTimer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
