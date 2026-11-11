using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public enum SideTab
    {
        Queue = 0,
        Favorites = 1,
        History = 2,
        Saved = 3
    }

    public class SideMenuPanel : Panel
    {
        private const int WM_ERASEBKGND = 0x0014;
        private const int HeaderH = 0;
        private const int TabsH = 32;
        private const int SearchBoxH = 28;
        private const int RowH = 42;
        private const int ScrollBarW = 7;

        private SideTab _activeTab = SideTab.Queue;
        private List<PlaylistItem> _rawItems = new List<PlaylistItem>();
        private List<PlaylistItem> _filteredItems = new List<PlaylistItem>();
        private PlaylistSearchBox _searchBox;
        private string _searchQuery = string.Empty;

        private int _scrollOffset = 0;
        private int _hoveredRow = -1;
        private int _hoveredTab = -1;
        private bool _isHoveringClose = false;
        private bool _isDraggingScroll = false;
        private int _dragGrabOffsetY = 0;
        private bool _isHoveringScroll = false;

        private Rectangle _closeBtnRect = Rectangle.Empty;
        private Rectangle[] _tabRects = new Rectangle[4];

        public event EventHandler<PlaylistItem> TrackSelected;
        public event EventHandler CloseRequested;

        private AudioPlayerService _audioPlayer;
        public AudioPlayerService AudioPlayer
        {
            get => _audioPlayer;
            set
            {
                if (_audioPlayer != value)
                {
                    _audioPlayer = value;
                    if (_audioPlayer != null)
                    {
                        _audioPlayer.PlaylistLoaded += (s, list) =>
                        {
                            if (_activeTab == SideTab.Queue)
                            {
                                BeginInvoke(new Action(RefreshData));
                            }
                        };
                        _audioPlayer.TrackLoaded += (s, e) =>
                        {
                            BeginInvoke(new Action(Invalidate));
                        };
                    }
                }
            }
        }

        public SideTab ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab != value)
                {
                    _activeTab = value;
                    _scrollOffset = 0;
                    _searchBox?.Clear();
                    RefreshData();
                }
            }
        }

        public SideMenuPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(20, 16, 34);

            _searchBox = new PlaylistSearchBox("Tìm trong danh sách...");
            _searchBox.SearchTextChanged += (s, text) =>
            {
                _searchQuery = text?.Trim() ?? string.Empty;
                _scrollOffset = 0;
                ApplyFilter();
                Invalidate();
            };
            Controls.Add(_searchBox);
            RefreshData();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                RefreshData();
            }
        }

        public bool IsSearchFocused => _searchBox != null && _searchBox.ContainsFocus;

        public void ClearSearchFocus()
        {
            _searchBox?.RemoveFocus();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x02000000; // WS_CLIPCHILDREN
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        public void RefreshData()
        {
            try
            {
                switch (_activeTab)
                {
                    case SideTab.Queue:
                        var qList = _audioPlayer?.Playlist ?? _audioPlayer?.SessionPlaylist;
                        if (qList != null && qList.Count > 0)
                        {
                            _rawItems = new List<PlaylistItem>(qList);
                        }
                        else if (!string.IsNullOrEmpty(_audioPlayer?.CurrentVideoId))
                        {
                            _rawItems = new List<PlaylistItem>
                            {
                                new PlaylistItem
                                {
                                    Id = _audioPlayer.CurrentVideoId,
                                    Title = _audioPlayer.CurrentTitle ?? "Đang phát",
                                    Author = _audioPlayer.CurrentAuthor ?? "",
                                    DurationSeconds = (int)(_audioPlayer.TotalDuration.TotalSeconds)
                                }
                            };
                        }
                        else
                        {
                            _rawItems = new List<PlaylistItem>();
                        }
                        break;
                    case SideTab.Favorites:
                        _rawItems = FavoritesService.GetFavorites() ?? new List<PlaylistItem>();
                        break;
                    case SideTab.History:
                        _rawItems = HistoryService.GetHistory() ?? new List<PlaylistItem>();
                        break;
                    case SideTab.Saved:
                        _rawItems = SavedTracksService.Instance.GetSavedTracks() ?? new List<PlaylistItem>();
                        break;
                }
            }
            catch
            {
                _rawItems = new List<PlaylistItem>();
            }

            ApplyFilter();
            ClampScroll();
            Invalidate();
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            text = text.Replace('\u0111', 'd').Replace('\u0110', 'D');
            string normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            System.Text.StringBuilder sb = new System.Text.StringBuilder(normalized.Length);
            for (int i = 0; i < normalized.Length; i++)
            {
                char ch = normalized[i];
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                _filteredItems = new List<PlaylistItem>(_rawItems);
                return;
            }

            string cleanQuery = RemoveDiacritics(_searchQuery);
            _filteredItems = _rawItems.FindAll(x =>
            {
                if (string.IsNullOrEmpty(x.Title)) return false;
                if (x.Title.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (RemoveDiacritics(x.Title).IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (!string.IsNullOrEmpty(x.Author))
                {
                    if (x.Author.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (RemoveDiacritics(x.Author).IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
                return false;
            });
        }

        private int ListTop => 10 + TabsH + 8 + SearchBoxH + 10;
        private int ListHeight => Math.Max(10, Height - ListTop - 8);
        private int TotalListContentHeight => _filteredItems.Count * RowH;

        private void ClampScroll()
        {
            int maxScroll = Math.Max(0, TotalListContentHeight - ListHeight);
            _scrollOffset = Math.Max(0, Math.Min(maxScroll, _scrollOffset));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            _closeBtnRect = Rectangle.Empty;

            // 4 tabs layout directly at top
            int tabY = 10;
            int tabMargin = 8;
            int availTabW = Width - (tabMargin * 2);
            int tabSpacing = 4;
            int tabW = Math.Max(20, (availTabW - 3 * tabSpacing) / 4);
            for (int i = 0; i < 4; i++)
            {
                _tabRects[i] = new Rectangle(tabMargin + i * (tabW + tabSpacing), tabY, tabW, TabsH);
            }

            // Search box position below tabs
            if (_searchBox != null)
            {
                _searchBox.Location = new Point(10, tabY + TabsH + 8);
                _searchBox.Size = new Size(Math.Max(100, Width - 20), SearchBoxH);
            }

            ClampScroll();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDraggingScroll)
            {
                int listTop = ListTop;
                int listH = ListHeight;
                int trackY = listTop + 4;
                int trackH = listH - 8;
                float maxScroll = TotalListContentHeight - listH;
                float ratio = (float)listH / TotalListContentHeight;
                int thumbH = Math.Max(24, (int)(trackH * ratio));
                float availableTrackH = Math.Max(1, trackH - thumbH);

                float newThumbY = e.Y - _dragGrabOffsetY - trackY;
                float scrollRatio = Math.Max(0f, Math.Min(1f, newThumbY / availableTrackH));
                _scrollOffset = (int)Math.Round(scrollRatio * maxScroll);
                ClampScroll();
                Invalidate();
                return;
            }

            bool needRepaint = false;

            // Close button hover
            bool overClose = _closeBtnRect.Contains(e.Location);
            if (overClose != _isHoveringClose)
            {
                _isHoveringClose = overClose;
                needRepaint = true;
            }

            // Tab hover
            int prevTab = _hoveredTab;
            _hoveredTab = -1;
            for (int i = 0; i < 4; i++)
            {
                if (_tabRects[i].Contains(e.Location))
                {
                    _hoveredTab = i;
                    break;
                }
            }
            if (_hoveredTab != prevTab) needRepaint = true;

            // Scrollbar hover (generous 24px wide hit zone)
            bool overScroll = (e.X >= Width - ScrollBarW - 14 && e.X <= Width && TotalListContentHeight > ListHeight && e.Y >= ListTop && e.Y <= ListTop + ListHeight);
            if (overScroll != _isHoveringScroll)
            {
                _isHoveringScroll = overScroll;
                needRepaint = true;
            }

            // Row hover
            int prevRow = _hoveredRow;
            _hoveredRow = -1;
            if (!overScroll && e.Y >= ListTop && e.Y <= ListTop + ListHeight && e.X >= 8 && e.X <= Width - 16)
            {
                int relY = e.Y - ListTop + _scrollOffset;
                int rowIdx = relY / RowH;
                if (rowIdx >= 0 && rowIdx < _filteredItems.Count)
                {
                    _hoveredRow = rowIdx;
                }
            }
            if (_hoveredRow != prevRow) needRepaint = true;

            if (needRepaint) Invalidate();

            Cursor = (overClose || _hoveredTab >= 0 || _hoveredRow >= 0 || _isHoveringScroll || _isDraggingScroll) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_isDraggingScroll)
            {
                _isHoveringClose = false;
                _hoveredTab = -1;
                _hoveredRow = -1;
                _isHoveringScroll = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left) return;

            // Close button click
            if (_closeBtnRect.Contains(e.Location))
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Tab click
            for (int i = 0; i < 4; i++)
            {
                if (_tabRects[i].Contains(e.Location))
                {
                    ActiveTab = (SideTab)i;
                    return;
                }
            }

            // Scrollbar click & drag
            int listTop = ListTop;
            int listH = ListHeight;
            if (TotalListContentHeight > listH && e.X >= Width - ScrollBarW - 14 && e.X <= Width && e.Y >= listTop && e.Y <= listTop + listH)
            {
                int trackY = listTop + 4;
                int trackH = listH - 8;
                float ratio = (float)listH / TotalListContentHeight;
                int thumbH = Math.Max(24, (int)(trackH * ratio));
                float maxScroll = TotalListContentHeight - listH;
                float availableTrackH = Math.Max(1, trackH - thumbH);
                int thumbY = trackY + (int)((_scrollOffset / maxScroll) * availableTrackH);

                if (e.Y >= thumbY && e.Y <= thumbY + thumbH)
                {
                    _isDraggingScroll = true;
                    _dragGrabOffsetY = e.Y - thumbY;
                    Capture = true;
                }
                else
                {
                    // Click track -> jump thumb
                    float clickRatio = Math.Max(0f, Math.Min(1f, (float)(e.Y - trackY - (thumbH / 2)) / availableTrackH));
                    _scrollOffset = (int)Math.Round(clickRatio * maxScroll);
                    ClampScroll();
                    _isDraggingScroll = true;
                    _dragGrabOffsetY = thumbH / 2;
                    Capture = true;
                    Invalidate();
                }
                return;
            }

            // Row click
            if (e.Y >= ListTop && e.Y <= ListTop + ListHeight)
            {
                int relY = e.Y - ListTop + _scrollOffset;
                int rowIdx = relY / RowH;
                if (rowIdx >= 0 && rowIdx < _filteredItems.Count)
                {
                    PlaylistItem item = _filteredItems[rowIdx];
                    if (_activeTab == SideTab.Queue)
                    {
                        if (_audioPlayer != null)
                        {
                            if (_audioPlayer.CurrentQueueSource != ActiveQueueSource.Playlist || _audioPlayer.Playlist != _audioPlayer.SessionPlaylist)
                            {
                                int sIdx = _audioPlayer.SessionPlaylist?.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase)) ?? -1;
                                if (sIdx >= 0)
                                {
                                    _audioPlayer.RestoreSessionPlaylist(sIdx);
                                    Invalidate();
                                    return;
                                }
                            }
                            int qIdx = _audioPlayer.Playlist?.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase)) ?? -1;
                            if (qIdx >= 0)
                            {
                                _audioPlayer.PlayTrackAtIndex(qIdx);
                            }
                            else
                            {
                                TrackSelected?.Invoke(this, item);
                            }
                        }
                    }
                    else if (_activeTab == SideTab.Favorites)
                    {
                        int realIdx = _rawItems.IndexOf(item);
                        _audioPlayer?.PlayFavorites(_rawItems, realIdx >= 0 ? realIdx : 0);
                    }
                    else if (_activeTab == SideTab.History)
                    {
                        int realIdx = _rawItems.IndexOf(item);
                        _audioPlayer?.PlayHistory(_rawItems, realIdx >= 0 ? realIdx : 0);
                    }
                    else if (_activeTab == SideTab.Saved)
                    {
                        int realIdx = _rawItems.IndexOf(item);
                        _audioPlayer?.PlaySavedTracks(_rawItems, realIdx >= 0 ? realIdx : 0);
                    }
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDraggingScroll)
            {
                _isDraggingScroll = false;
                Capture = false;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _scrollOffset -= (e.Delta / 120) * 36;
            ClampScroll();
            Invalidate();
        }

        private static GraphicsPath RoundedRect(Rectangle rc, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rc.X, rc.Y, d, d, 180, 90);
            path.AddArc(rc.Right - d, rc.Y, d, d, 270, 90);
            path.AddArc(rc.Right - d, rc.Bottom - d, d, d, 0, 90);
            path.AddArc(rc.X, rc.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. Panel Background (Glassmorphism dark card)
            Rectangle cardRect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(cardRect, 14))
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(235, 22, 14, 38)))
                {
                    g.FillPath(bgBrush, path);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(60, 180, 80, 220), 1.2f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // 2. Tabs (Pill Buttons directly at top)
            string[] tabLabels = { "Hàng đợi", "Bộ sưu tập", "Lịch sử", "Đã tải" };
            for (int i = 0; i < 4; i++)
            {
                Rectangle tr = _tabRects[i];
                bool isCurTab = ((int)_activeTab == i);
                bool isHover = (_hoveredTab == i);

                Color bgColor = isCurTab ? Color.FromArgb(220, 75, 22, 105) : (isHover ? Color.FromArgb(180, 45, 28, 76) : Color.FromArgb(120, 30, 18, 54));
                Color borderColor = isCurTab ? Color.FromArgb(255, 45, 135) : (isHover ? Color.FromArgb(120, 180, 80, 220) : Color.FromArgb(60, 100, 50, 140));
                Color textColor = isCurTab ? Color.White : (isHover ? Color.FromArgb(240, 235, 250) : Color.FromArgb(160, 150, 180));
                FontStyle fs = isCurTab ? FontStyle.Bold : FontStyle.Regular;

                using (GraphicsPath tp = RoundedRect(tr, 8))
                {
                    using (SolidBrush tbBrush = new SolidBrush(bgColor))
                    {
                        g.FillPath(tbBrush, tp);
                    }
                    using (Pen tbPen = new Pen(borderColor, isCurTab ? 1.2f : 1f))
                    {
                        g.DrawPath(tbPen, tp);
                    }
                }

                using (Font tabFont = new Font("Segoe UI", 8.0f, fs))
                using (SolidBrush tabBrush = new SolidBrush(textColor))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(tabLabels[i], tabFont, tabBrush, tr, sf);
                }
            }

            // 4. Track List
            int listTop = ListTop;
            int listH = ListHeight;
            Rectangle listBounds = new Rectangle(6, listTop, Width - 12, listH);

            Region oldClip = g.Clip;
            g.SetClip(listBounds);

            string currentId = _audioPlayer?.CurrentVideoId;

            if (_filteredItems.Count == 0)
            {
                using (Font emptyFont = new Font("Segoe UI", 9.0f, FontStyle.Italic))
                using (SolidBrush emptyBrush = new SolidBrush(Color.FromArgb(130, 140, 165)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    string emptyMsg = "Danh sách trống";
                    if (!string.IsNullOrEmpty(_searchQuery)) emptyMsg = "Không tìm thấy kết quả phù hợp";
                    else if (_activeTab == SideTab.Queue) emptyMsg = "Chưa có bài hát trong hàng đợi";
                    else if (_activeTab == SideTab.Favorites) emptyMsg = "Chưa có bài hát yêu thích";
                    else if (_activeTab == SideTab.History) emptyMsg = "Lịch sử nghe nhạc trống";
                    else if (_activeTab == SideTab.Saved) emptyMsg = "Chưa có bài hát đã tải";

                    g.DrawString(emptyMsg, emptyFont, emptyBrush, listBounds, sf);
                }
            }
            else
            {
                using (Font idxFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (Font titleFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
                using (Font authorFont = new Font("Segoe UI", 7.8f, FontStyle.Regular))
                using (Font durFont = new Font("Segoe UI", 7.8f, FontStyle.Regular))
                using (StringFormat titleSf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                using (StringFormat durSf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                {
                    for (int i = 0; i < _filteredItems.Count; i++)
                    {
                        int rowY = listTop + (i * RowH) - _scrollOffset;
                        if (rowY + RowH < listTop || rowY > listTop + listH) continue;

                        PlaylistItem item = _filteredItems[i];
                        bool isPlaying = !string.IsNullOrEmpty(currentId) && string.Equals(item.Id, currentId, StringComparison.OrdinalIgnoreCase);
                        bool isHover = (i == _hoveredRow);

                        Rectangle rowRect = new Rectangle(8, rowY, Width - 16, RowH - 2);

                        // Row background
                        if (isPlaying)
                        {
                            using (GraphicsPath rp = RoundedRect(rowRect, 6))
                            using (SolidBrush activeBg = new SolidBrush(Color.FromArgb(50, 255, 60, 160)))
                            {
                                g.FillPath(activeBg, rp);
                            }
                            // Left accent line
                            using (SolidBrush bar = new SolidBrush(Color.FromArgb(255, 65, 160)))
                            {
                                g.FillRectangle(bar, rowRect.X, rowRect.Y + 4, 3, rowRect.Height - 8);
                            }
                        }
                        else if (isHover)
                        {
                            using (GraphicsPath rp = RoundedRect(rowRect, 6))
                            using (SolidBrush hovBg = new SolidBrush(Color.FromArgb(25, 255, 255, 255)))
                            {
                                g.FillPath(hovBg, rp);
                            }
                        }

                        // Index or Play indicator
                        int leftX = rowRect.X + 8;
                        if (isPlaying)
                        {
                            using (SolidBrush eqBrush = new SolidBrush(Color.FromArgb(255, 100, 180)))
                            {
                                g.FillRectangle(eqBrush, leftX, rowY + 18, 2, 8);
                                g.FillRectangle(eqBrush, leftX + 4, rowY + 14, 2, 12);
                                g.FillRectangle(eqBrush, leftX + 8, rowY + 16, 2, 10);
                            }
                            leftX += 16;
                        }
                        else
                        {
                            using (SolidBrush idxBrush = new SolidBrush(Color.FromArgb(120, 130, 160)))
                            {
                                g.DrawString((i + 1).ToString(), idxFont, idxBrush, new PointF(leftX, rowY + 11));
                            }
                            leftX += (i + 1) >= 10 ? 20 : 16;
                        }

                        // Duration on right
                        int durW = 42;
                        int durX = rowRect.Right - durW - 4;
                        string durText = !string.IsNullOrEmpty(item.FormattedDuration) ? item.FormattedDuration : "";
                        using (SolidBrush durBrush = new SolidBrush(Color.FromArgb(135, 140, 165)))
                        {
                            g.DrawString(durText, durFont, durBrush, new RectangleF(durX, rowY + 11, durW, 16), durSf);
                        }

                        // Title & Author
                        int textW = durX - leftX - 6;
                        if (textW > 20)
                        {
                            Color titleColor = isPlaying ? Color.FromArgb(255, 230, 245) : (isHover ? Color.White : Color.FromArgb(230, 230, 240));
                            Color authorColor = isPlaying ? Color.FromArgb(230, 180, 215) : Color.FromArgb(155, 155, 180);

                            using (SolidBrush titleBrush = new SolidBrush(titleColor))
                            {
                                g.DrawString(item.Title ?? "Unknown Title", titleFont, titleBrush, new RectangleF(leftX, rowY + 4, textW, 18), titleSf);
                            }
                            using (SolidBrush authorBrush = new SolidBrush(authorColor))
                            {
                                g.DrawString(item.Author ?? "Unknown Artist", authorFont, authorBrush, new RectangleF(leftX, rowY + 22, textW, 16), titleSf);
                            }
                        }
                    }
                }
            }

            g.Clip = oldClip;

            // 5. Scrollbar
            if (TotalListContentHeight > listH && listH > 0)
            {
                int trackX = Width - ScrollBarW - 4;
                int trackY = listTop + 4;
                int trackH = listH - 8;

                float ratio = (float)listH / TotalListContentHeight;
                int thumbH = Math.Max(24, (int)(trackH * ratio));
                float maxScroll = TotalListContentHeight - listH;
                float availableTrackH = Math.Max(1, trackH - thumbH);
                int thumbY = trackY + (int)((_scrollOffset / maxScroll) * availableTrackH);

                Color thumbColor = (_isDraggingScroll || _isHoveringScroll) ? Color.FromArgb(235, 255, 80, 160) : Color.FromArgb(90, 255, 255, 255);
                using (GraphicsPath thumbPath = RoundedRect(new Rectangle(trackX, thumbY, ScrollBarW, thumbH), 3))
                using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
                {
                    g.FillPath(thumbBrush, thumbPath);
                }
            }
        }
    }
}
