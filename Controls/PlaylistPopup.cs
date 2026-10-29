using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public enum MenuTab
    {
        Playlist,
        History
    }

    public class PlaylistPopup : Form
    {
        private MenuTab _activeTab = MenuTab.Playlist;
        private List<PlaylistItem> _playlistItems = new List<PlaylistItem>();
        private List<PlaylistItem> _historyItems = new List<PlaylistItem>();

        private int _currentPlaylistIndex = -1;
        private int _hoveredRowIndex = -1;
        private int _hoveredTabIndex = -1; // 0: Playlist, 1: History
        private bool _isHoveringClose = false;
        private bool _isLoading = false;

        private int _scrollOffset = 0;
        private bool _isDraggingScroll = false;
        private bool _isHoveringScroll = false;
        private int _dragGrabOffsetY = 0;

        private const int HeaderHeight = 42;
        private const int RowHeight = 36;
        private const int ScrollBarWidth = 6;

        public event EventHandler<PlaylistItem> TrackSelected;
        public event EventHandler CloseRequested;

        public AudioPlayerService AudioPlayer { get; set; }

        public bool IsLoading
        {
            get { return _isLoading; }
        }

        public MenuTab ActiveTab
        {
            get { return _activeTab; }
            set
            {
                if (_activeTab != value)
                {
                    _activeTab = value;
                    _scrollOffset = 0;
                    ClampScroll();
                    Invalidate();
                }
            }
        }

        private List<PlaylistItem> ActiveItems => (_activeTab == MenuTab.Playlist) ? _playlistItems : _historyItems;

        private DateTime _lastDeactivatedTime = DateTime.MinValue;

        public bool JustDeactivated
        {
            get { return (DateTime.UtcNow - _lastDeactivatedTime).TotalMilliseconds < 250; }
        }

        public PlaylistPopup()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(20, 18, 30);
            Size = new Size(380, 270);
            DoubleBuffered = true;
            KeyPreview = true;

            _historyItems = HistoryService.GetHistory();
            HistoryService.HistoryChanged += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            _historyItems = HistoryService.GetHistory();
                            if (_activeTab == MenuTab.History) ClampScroll();
                            Invalidate();
                        }));
                    }
                    catch { }
                }
                else
                {
                    _historyItems = HistoryService.GetHistory();
                }
            };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshHistory();
            UpdateRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated)
            {
                UpdateRegion();
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _lastDeactivatedTime = DateTime.UtcNow;
            if (Owner != null && !Owner.IsDisposed)
            {
                try { Owner.Activate(); } catch { }
            }
            Hide();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Hide();
            }
            else if (e.KeyCode == Keys.Down)
            {
                _scrollOffset += RowHeight;
                ClampScroll();
                UpdateHover(PointToClient(Cursor.Position));
                Invalidate();
            }
            else if (e.KeyCode == Keys.Up)
            {
                _scrollOffset -= RowHeight;
                ClampScroll();
                UpdateHover(PointToClient(Cursor.Position));
                Invalidate();
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                _scrollOffset += RowHeight * 4;
                ClampScroll();
                UpdateHover(PointToClient(Cursor.Position));
                Invalidate();
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                _scrollOffset -= RowHeight * 4;
                ClampScroll();
                UpdateHover(PointToClient(Cursor.Position));
                Invalidate();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                RefreshHistory();
                ClampScroll();
                Invalidate();
            }
            else
            {
                ThumbnailManager.ClearMemory();
                MemoryOptimizer.TrimMemoryLight();

                if (Owner != null && !Owner.IsDisposed)
                {
                    try { Owner.Activate(); } catch { }
                }
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RefreshHistory()
        {
            _historyItems = HistoryService.GetHistory();
            if (AudioPlayer != null)
            {
                foreach (var hItem in _historyItems)
                {
                    if (string.IsNullOrWhiteSpace(hItem.Title) || hItem.Title == "None" || hItem.Title == "NA" || hItem.Title.StartsWith("Bài "))
                    {
                        if (AudioPlayer.Playlist != null)
                        {
                            var pMatch = AudioPlayer.Playlist.Find(x => string.Equals(x.Id, hItem.Id, StringComparison.OrdinalIgnoreCase));
                            if (pMatch != null && !string.IsNullOrWhiteSpace(pMatch.Title) && pMatch.Title != "None" && pMatch.Title != "NA")
                            {
                                hItem.Title = pMatch.Title;
                                HistoryService.UpdateTitle(hItem.Id, pMatch.Title);
                                continue;
                            }
                        }
                        AudioPlayer.EnsureTrackTitleResolved(hItem.Id);
                    }
                }
            }
            if (_activeTab == MenuTab.History)
            {
                ClampScroll();
            }
            Invalidate();
        }

        private void UpdateRegion()
        {
            if (Width > 0 && Height > 0)
            {
                using (GraphicsPath path = GetRoundedRect(new Rectangle(0, 0, Width, Height), 10))
                {
                    Region = new Region(path);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        public void StartLoading()
        {
            _isLoading = true;
            _playlistItems.Clear();
            _currentPlaylistIndex = -1;
            if (_activeTab == MenuTab.Playlist) _scrollOffset = 0;
            Invalidate();
        }

        public void SetPlaylist(List<PlaylistItem> items, int currentIndex = 0)
        {
            _isLoading = false;
            _playlistItems = items ?? new List<PlaylistItem>();
            _currentPlaylistIndex = currentIndex;
            if (_activeTab == MenuTab.Playlist)
            {
                _scrollOffset = 0;
                EnsureCurrentVisible();
            }
            Invalidate();
        }

        public void SetCurrentIndex(int currentIndex)
        {
            _currentPlaylistIndex = currentIndex;
            if (_activeTab == MenuTab.Playlist)
            {
                EnsureCurrentVisible();
            }
            Invalidate();
        }

        public void Clear()
        {
            _isLoading = false;
            _playlistItems.Clear();
            _currentPlaylistIndex = -1;
            if (_activeTab == MenuTab.Playlist) _scrollOffset = 0;
            Invalidate();
        }

        private void EnsureCurrentVisible()
        {
            if (_activeTab != MenuTab.Playlist) return;
            if (_currentPlaylistIndex < 0 || _currentPlaylistIndex >= _playlistItems.Count) return;
            int viewHeight = Height - HeaderHeight - 8;
            int itemTop = _currentPlaylistIndex * RowHeight;
            int itemBottom = itemTop + RowHeight;

            if (itemTop < _scrollOffset)
            {
                _scrollOffset = itemTop;
            }
            else if (itemBottom > _scrollOffset + viewHeight)
            {
                _scrollOffset = itemBottom - viewHeight;
            }
            ClampScroll();
        }

        private void ClampScroll()
        {
            int totalHeight = ActiveItems.Count * RowHeight;
            int viewHeight = Height - HeaderHeight - 8;
            int maxScroll = Math.Max(0, totalHeight - viewHeight);
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, maxScroll));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _scrollOffset -= (e.Delta / 120) * (RowHeight * 2);
            ClampScroll();
            UpdateHover(PointToClient(Cursor.Position));
            Invalidate();
        }

        private Rectangle GetThumbRect()
        {
            int totalContentHeight = ActiveItems.Count * RowHeight;
            Rectangle listArea = new Rectangle(6, HeaderHeight + 2, Width - 12, Height - HeaderHeight - 8);
            if (totalContentHeight <= listArea.Height || listArea.Height <= 0)
            {
                return Rectangle.Empty;
            }

            int scrollTrackH = listArea.Height;
            int minThumbH = 24;
            int thumbH = Math.Max(minThumbH, (int)((float)listArea.Height / totalContentHeight * scrollTrackH));
            int maxScroll = totalContentHeight - listArea.Height;
            int thumbY = listArea.Top + (int)((float)_scrollOffset / maxScroll * (scrollTrackH - thumbH));

            int thumbW = (_isDraggingScroll || _isHoveringScroll) ? 8 : 6;
            int thumbX = Width - 8 - (thumbW / 2);

            return new Rectangle(thumbX, thumbY, thumbW, thumbH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDraggingScroll)
            {
                Rectangle listArea = new Rectangle(6, HeaderHeight + 2, Width - 12, Height - HeaderHeight - 8);
                int totalContentHeight = ActiveItems.Count * RowHeight;
                if (totalContentHeight > listArea.Height && listArea.Height > 0)
                {
                    int scrollTrackH = listArea.Height;
                    int minThumbH = 24;
                    int thumbH = Math.Max(minThumbH, (int)((float)listArea.Height / totalContentHeight * scrollTrackH));
                    int maxScroll = totalContentHeight - listArea.Height;

                    int targetThumbY = e.Location.Y - _dragGrabOffsetY;
                    int minThumbY = listArea.Top;
                    int maxThumbY = listArea.Top + scrollTrackH - thumbH;
                    targetThumbY = Math.Max(minThumbY, Math.Min(maxThumbY, targetThumbY));

                    float ratio = (scrollTrackH - thumbH > 0) ? (float)(targetThumbY - minThumbY) / (scrollTrackH - thumbH) : 0f;
                    _scrollOffset = (int)(ratio * maxScroll);
                    ClampScroll();
                    Invalidate();
                }
                return;
            }

            UpdateHover(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDraggingScroll)
            {
                _isDraggingScroll = false;
                Capture = false;
                UpdateHover(e.Location);
                Invalidate();
            }
        }

        private void UpdateHover(Point pt)
        {
            // Close button hover
            Rectangle closeRect = new Rectangle(Width - 32, 10, 22, 22);
            bool oldCloseHover = _isHoveringClose;
            _isHoveringClose = closeRect.Contains(pt);

            // Tab headers hover
            Rectangle tab1Rect = new Rectangle(10, 7, 136, 28);
            Rectangle tab2Rect = new Rectangle(152, 7, 106, 28);
            int oldHoverTab = _hoveredTabIndex;
            if (tab1Rect.Contains(pt)) _hoveredTabIndex = 0;
            else if (tab2Rect.Contains(pt)) _hoveredTabIndex = 1;
            else _hoveredTabIndex = -1;

            // Scrollbar hover
            Rectangle listArea = new Rectangle(6, HeaderHeight + 2, Width - 12, Height - HeaderHeight - 8);
            Rectangle scrollHitArea = new Rectangle(Width - 18, listArea.Top, 18, listArea.Height);
            bool oldScrollHover = _isHoveringScroll;
            _isHoveringScroll = scrollHitArea.Contains(pt);

            // Row hover
            int oldHover = _hoveredRowIndex;
            if (!_isHoveringScroll && pt.Y >= HeaderHeight && pt.Y <= Height - 6 && pt.X >= 8 && pt.X <= Width - 18)
            {
                int relY = pt.Y - HeaderHeight + _scrollOffset;
                int idx = relY / RowHeight;
                _hoveredRowIndex = (idx >= 0 && idx < ActiveItems.Count) ? idx : -1;
            }
            else
            {
                _hoveredRowIndex = -1;
            }

            if (oldHover != _hoveredRowIndex || oldCloseHover != _isHoveringClose || oldHoverTab != _hoveredTabIndex || oldScrollHover != _isHoveringScroll)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_isDraggingScroll)
            {
                _hoveredRowIndex = -1;
                _hoveredTabIndex = -1;
                _isHoveringClose = false;
                _isHoveringScroll = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            Rectangle tab1Rect = new Rectangle(10, 7, 136, 28);
            Rectangle tab2Rect = new Rectangle(152, 7, 106, 28);
            Rectangle closeRect = new Rectangle(Width - 32, 10, 22, 22);

            if (e.Button == MouseButtons.Right)
            {
                // Right click on History Tab -> Show History Settings Context Menu
                if (tab2Rect.Contains(e.Location))
                {
                    ShowHistoryTabContextMenu(PointToScreen(new Point(tab2Rect.Left, tab2Rect.Bottom + 2)));
                    return;
                }

                // Right click on a song item
                int clickedIndex = GetSongIndexAt(e.Location);
                if (clickedIndex >= 0 && clickedIndex < ActiveItems.Count)
                {
                    ShowSongContextMenu(clickedIndex, PointToScreen(e.Location));
                    return;
                }

                return;
            }

            if (e.Button != MouseButtons.Left) return;

            // 1. Close button clicked
            if (closeRect.Contains(e.Location))
            {
                Hide();
                return;
            }

            // 2. Tab 1 clicked (Danh sách phát)
            if (tab1Rect.Contains(e.Location))
            {
                if (_activeTab != MenuTab.Playlist)
                {
                    _activeTab = MenuTab.Playlist;
                    _scrollOffset = 0;
                    ClampScroll();
                    Invalidate();
                }
                return;
            }

            // 3. Tab 2 clicked (Lịch sử)
            if (tab2Rect.Contains(e.Location))
            {
                if (_activeTab != MenuTab.History)
                {
                    _activeTab = MenuTab.History;
                    _scrollOffset = 0;
                    RefreshHistory();
                    ClampScroll();
                    Invalidate();
                }
                return;
            }

            // 4. Scrollbar interaction
            Rectangle listArea = new Rectangle(6, HeaderHeight + 2, Width - 12, Height - HeaderHeight - 8);
            int totalContentHeight = ActiveItems.Count * RowHeight;
            if (totalContentHeight > listArea.Height && listArea.Height > 0)
            {
                Rectangle scrollHitArea = new Rectangle(Width - 18, listArea.Top, 18, listArea.Height);
                if (scrollHitArea.Contains(e.Location))
                {
                    Rectangle thumbRect = GetThumbRect();
                    int scrollTrackH = listArea.Height;
                    int minThumbH = 24;
                    int thumbH = Math.Max(minThumbH, (int)((float)listArea.Height / totalContentHeight * scrollTrackH));
                    int maxScroll = totalContentHeight - listArea.Height;

                    if (thumbRect.Contains(e.Location))
                    {
                        _isDraggingScroll = true;
                        _dragGrabOffsetY = e.Location.Y - thumbRect.Y;
                        Capture = true;
                        Invalidate();
                        return;
                    }
                    else
                    {
                        // Jump thumb to clicked position
                        int targetThumbY = e.Location.Y - (thumbH / 2);
                        int minThumbY = listArea.Top;
                        int maxThumbY = listArea.Top + scrollTrackH - thumbH;
                        targetThumbY = Math.Max(minThumbY, Math.Min(maxThumbY, targetThumbY));

                        float ratio = (scrollTrackH - thumbH > 0) ? (float)(targetThumbY - minThumbY) / (scrollTrackH - thumbH) : 0f;
                        _scrollOffset = (int)(ratio * maxScroll);
                        ClampScroll();

                        _isDraggingScroll = true;
                        _dragGrabOffsetY = thumbH / 2;
                        Capture = true;
                        Invalidate();
                        return;
                    }
                }
            }

            // 5. Song row clicked
            if (_hoveredRowIndex >= 0 && _hoveredRowIndex < ActiveItems.Count)
            {
                if (_activeTab == MenuTab.Playlist)
                {
                    _currentPlaylistIndex = _hoveredRowIndex;
                    Invalidate();
                    TrackSelected?.Invoke(this, _playlistItems[_currentPlaylistIndex]);
                }
                else
                {
                    PlaylistItem historyItem = _historyItems[_hoveredRowIndex];
                    if (AudioPlayer != null)
                    {
                        AudioPlayer.LoadAndPlay(historyItem.Url);
                    }
                    Invalidate();
                }
            }
        }

        private int GetSongIndexAt(Point pt)
        {
            if (pt.Y >= HeaderHeight && pt.Y <= Height - 6 && pt.X >= 8 && pt.X <= Width - 18)
            {
                int relY = pt.Y - HeaderHeight + _scrollOffset;
                int idx = relY / RowHeight;
                if (idx >= 0 && idx < ActiveItems.Count) return idx;
            }
            return -1;
        }

        private void ShowSongContextMenu(int index, Point screenLocation)
        {
            if (index < 0 || index >= ActiveItems.Count) return;
            PlaylistItem item = ActiveItems[index];

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(24, 24, 28);
            menu.ForeColor = Color.FromArgb(235, 235, 235);
            menu.Renderer = new Form1.DarkMenuRenderer();
            menu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            if (_activeTab == MenuTab.History)
            {
                var itemAdd = new ToolStripMenuItem("Thêm vào danh sách phát");
                itemAdd.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                itemAdd.Click += (s, e) =>
                {
                    if (AudioPlayer != null)
                    {
                        AudioPlayer.AddToPlaylist(item);
                        Invalidate();
                    }
                };

                var itemPlay = new ToolStripMenuItem("Phát ngay");
                itemPlay.Click += (s, e) =>
                {
                    if (AudioPlayer != null)
                    {
                        AudioPlayer.LoadAndPlay(item.Url);
                    }
                };

                var itemDelete = new ToolStripMenuItem("Xóa khỏi lịch sử");
                itemDelete.Click += (s, e) =>
                {
                    HistoryService.RemoveTrack(item.Id);
                    RefreshHistory();
                };

                menu.Items.Add(itemAdd);
                menu.Items.Add(itemPlay);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(itemDelete);
            }
            else
            {
                var itemPlay = new ToolStripMenuItem("Phát ngay");
                itemPlay.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                itemPlay.Click += (s, e) =>
                {
                    if (AudioPlayer != null)
                    {
                        AudioPlayer.PlayTrackAtIndex(index);
                    }
                };

                var itemRemove = new ToolStripMenuItem("Xóa khỏi danh sách phát");
                itemRemove.Click += (s, e) =>
                {
                    if (AudioPlayer != null)
                    {
                        AudioPlayer.RemoveFromPlaylist(index);
                        Invalidate();
                    }
                };

                menu.Items.Add(itemPlay);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(itemRemove);
            }

            menu.Show(screenLocation);
        }

        private void ShowHistoryTabContextMenu(Point screenLocation)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(24, 24, 28);
            menu.ForeColor = Color.FromArgb(235, 235, 235);
            menu.Renderer = new Form1.DarkMenuRenderer();
            menu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            var itemClean = new ToolStripMenuItem("Xóa file tạm (Temp) khi thoát");
            itemClean.CheckOnClick = true;
            itemClean.Checked = HistoryService.AutoCleanTempOnExit;
            itemClean.Click += (s, e) =>
            {
                HistoryService.AutoCleanTempOnExit = !HistoryService.AutoCleanTempOnExit;
                itemClean.Checked = HistoryService.AutoCleanTempOnExit;
            };

            var itemClearAll = new ToolStripMenuItem("Xóa toàn bộ lịch sử");
            itemClearAll.Click += (s, e) =>
            {
                HistoryService.ClearHistory();
                RefreshHistory();
            };

            menu.Items.Add(itemClean);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemClearAll);

            menu.Show(screenLocation);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;

            // 1. Frosted Dark Glass Body
            using (GraphicsPath cardPath = GetRoundedRect(new Rectangle(0, 0, w - 1, h - 1), 10))
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(242, 18, 16, 26)))
                {
                    g.FillPath(bgBrush, cardPath);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(45, 255, 255, 255), 1))
                {
                    g.DrawPath(borderPen, cardPath);
                }
            }

            // 2. Tab Navigation Header
            Rectangle tab1Rect = new Rectangle(10, 7, 136, 28);
            Rectangle tab2Rect = new Rectangle(152, 7, 106, 28);

            DrawTab(g, tab1Rect, "Danh sách phát", _playlistItems.Count, _activeTab == MenuTab.Playlist, _hoveredTabIndex == 0);
            DrawTab(g, tab2Rect, "Lịch sử", _historyItems.Count, _activeTab == MenuTab.History, _hoveredTabIndex == 1);

            // Close button "✕"
            Color closeColor = _isHoveringClose ? Color.FromArgb(255, 120, 120) : Color.FromArgb(150, 150, 160);
            using (Font closeFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (SolidBrush closeBrush = new SolidBrush(closeColor))
            using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("✕", closeFont, closeBrush, new RectangleF(w - 32, 10, 22, 22), sf);
            }

            // Header separator line
            using (Pen sepPen = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
            {
                g.DrawLine(sepPen, 10, HeaderHeight, w - 10, HeaderHeight);
            }

            // 3. Items List
            if (_activeTab == MenuTab.Playlist && _isLoading)
            {
                using (Font emptyFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (Font hintFont = new Font("Segoe UI", 8.5f))
                using (SolidBrush b1 = new SolidBrush(Color.FromArgb(210, 168, 255)))
                using (SolidBrush b2 = new SolidBrush(Color.FromArgb(160, 160, 175)))
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Đang tải danh sách phát...", emptyFont, b1, new RectangleF(0, HeaderHeight + 40, w, 24), sf);
                    g.DrawString("Vui lòng đợi trong giây lát...", hintFont, b2, new RectangleF(0, HeaderHeight + 66, w, 20), sf);
                }
                return;
            }

            List<PlaylistItem> currentList = ActiveItems;
            if (currentList.Count == 0)
            {
                // Empty state
                using (Font emptyFont = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (Font hintFont = new Font("Segoe UI", 8f))
                using (SolidBrush b1 = new SolidBrush(Color.FromArgb(180, 180, 190)))
                using (SolidBrush b2 = new SolidBrush(Color.FromArgb(120, 120, 130)))
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    if (_activeTab == MenuTab.Playlist)
                    {
                        g.DrawString("Chưa có danh sách phát", emptyFont, b1, new RectangleF(0, HeaderHeight + 40, w, 24), sf);
                        g.DrawString("Dán link YouTube playlist hoặc chuột phải vào Lịch sử để thêm", hintFont, b2, new RectangleF(0, HeaderHeight + 66, w, 20), sf);
                    }
                    else
                    {
                        g.DrawString("Chưa có lịch sử phát nhạc", emptyFont, b1, new RectangleF(0, HeaderHeight + 40, w, 24), sf);
                        g.DrawString("Các bài hát đã nghe sẽ được lưu tự động tại đây", hintFont, b2, new RectangleF(0, HeaderHeight + 66, w, 20), sf);
                    }
                }
                return;
            }

            // Set clip region for scrollable rows
            Rectangle listArea = new Rectangle(6, HeaderHeight + 2, w - 12, h - HeaderHeight - 8);
            g.SetClip(listArea);

            using (Font numFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (Font titleFont = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (Font durFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (SolidBrush normalText = new SolidBrush(Color.FromArgb(235, 235, 240)))
            using (SolidBrush activeText = new SolidBrush(Color.White))
            using (SolidBrush dimText = new SolidBrush(Color.FromArgb(140, 140, 150)))
            using (SolidBrush activeIndicatorBrush = new SolidBrush(Color.FromArgb(168, 85, 247)))
            using (StringFormat sfNum = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                int totalCount = currentList.Count;
                int indexColW = (totalCount >= 1000) ? 34 : ((totalCount >= 100) ? 27 : 18);
                int thumbSize = 26;
                int thumbXOffset = indexColW + 6;
                int titleXOffset = thumbXOffset + thumbSize + 8;

                for (int i = 0; i < currentList.Count; i++)
                {
                    int rowY = HeaderHeight + 2 + (i * RowHeight) - _scrollOffset;
                    if (rowY + RowHeight < HeaderHeight || rowY > h) continue;

                    Rectangle rowRect = new Rectangle(8, rowY, w - 16, RowHeight - 2);
                    PlaylistItem item = currentList[i];

                    bool isActive = false;
                    if (_activeTab == MenuTab.Playlist)
                    {
                        isActive = (i == _currentPlaylistIndex);
                    }
                    else
                    {
                        isActive = (AudioPlayer != null && !string.IsNullOrEmpty(AudioPlayer.CurrentVideoId) && string.Equals(item.Id, AudioPlayer.CurrentVideoId, StringComparison.OrdinalIgnoreCase));
                    }

                    bool isHover = (i == _hoveredRowIndex);

                    // Row background
                    if (isActive)
                    {
                        using (GraphicsPath rp = GetRoundedRect(rowRect, 6))
                        using (SolidBrush activeBg = new SolidBrush(Color.FromArgb(45, 168, 85, 247)))
                        using (Pen activeBorder = new Pen(Color.FromArgb(80, 168, 85, 247), 1))
                        {
                            g.FillPath(activeBg, rp);
                            g.DrawPath(activeBorder, rp);
                        }
                    }
                    else if (isHover)
                    {
                        using (GraphicsPath rp = GetRoundedRect(rowRect, 6))
                        using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(25, 255, 255, 255)))
                        {
                            g.FillPath(hoverBg, rp);
                        }
                    }

                    // 1. Playing icon or Index number (right-aligned to adapt to 2, 3, 4 digit numbers)
                    Rectangle numRect = new Rectangle(rowRect.X + 2, rowRect.Y + 8, indexColW, 18);
                    if (isActive)
                    {
                        int arrowMidX = numRect.Right - (indexColW >= 27 ? 10 : 8);
                        Point[] playArrow = new Point[]
                        {
                            new Point(arrowMidX - 4, rowRect.Y + 12),
                            new Point(arrowMidX + 4, rowRect.Y + 17),
                            new Point(arrowMidX - 4, rowRect.Y + 22)
                        };
                        g.FillPolygon(activeIndicatorBrush, playArrow);
                    }
                    else
                    {
                        string num = (i + 1).ToString();
                        g.DrawString(num, numFont, dimText, numRect, sfNum);
                    }

                    // 2. Micro Thumbnail (26x26 rounded box, dynamically offset after index column)
                    int thumbX = rowRect.X + thumbXOffset;
                    int thumbY = rowRect.Y + 4;
                    Rectangle thumbRect = new Rectangle(thumbX, thumbY, thumbSize, thumbSize);

                    using (GraphicsPath tp = GetRoundedRect(thumbRect, 4))
                    {
                        Bitmap thumb = ThumbnailManager.GetThumbnail(item.Id);
                        if (thumb != null)
                        {
                            GraphicsState gs = g.Save();
                            g.SetClip(tp, CombineMode.Intersect);
                            g.DrawImage(thumb, thumbRect);
                            g.Restore(gs);

                            using (Pen p = new Pen(Color.FromArgb(40, 255, 255, 255), 1))
                            {
                                g.DrawPath(p, tp);
                            }
                        }
                        else
                        {
                            using (SolidBrush b = new SolidBrush(Color.FromArgb(36, 28, 44)))
                            using (Pen p = new Pen(Color.FromArgb(45, 255, 255, 255), 1))
                            {
                                g.FillPath(b, tp);
                                g.DrawPath(p, tp);
                            }
                            using (Font noteFont = new Font("Segoe UI", 7.5f))
                            using (SolidBrush noteBrush = new SolidBrush(Color.FromArgb(140, 120, 160)))
                            using (StringFormat sfNote = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("♪", noteFont, noteBrush, thumbRect, sfNote);
                            }

                            ThumbnailManager.QueueLoad(item.Id, () =>
                            {
                                if (IsHandleCreated)
                                {
                                    try { BeginInvoke(new Action(Invalidate)); } catch { }
                                }
                            });
                        }

                        if (isActive)
                        {
                            using (Pen activeBorderPen = new Pen(Color.FromArgb(192, 132, 252), 1.5f))
                            {
                                g.DrawPath(activeBorderPen, tp);
                            }
                        }
                    }

                    // 3. Title (starting right after the micro thumbnail)
                    int titleX = rowRect.X + titleXOffset;
                    int titleW = Math.Max(50, rowRect.Right - 46 - titleX - 4);
                    RectangleF titleRect = new RectangleF(titleX, rowRect.Y + 8, titleW, 20);

                    using (StringFormat sfTitle = new StringFormat() { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
                    {
                        string displayTitle = (!string.IsNullOrWhiteSpace(item.Title) && item.Title.Trim() != "None" && item.Title.Trim() != "NA")
                            ? item.Title
                            : string.Format("Bài {0}", i + 1);
                        g.DrawString(displayTitle, titleFont, isActive ? activeText : normalText, titleRect, sfTitle);
                    }

                    // Duration
                    RectangleF durRect = new RectangleF(rowRect.Right - 46, rowRect.Y + 9, 42, 18);
                    using (StringFormat sfDur = new StringFormat() { Alignment = StringAlignment.Far })
                    {
                        g.DrawString(item.FormattedDuration, durFont, dimText, durRect, sfDur);
                    }
                }
            }

            g.ResetClip();

            // 4. Custom Interactive Scrollbar
            int totalContentHeight = currentList.Count * RowHeight;
            if (totalContentHeight > listArea.Height && listArea.Height > 0)
            {
                int railX = w - 8;
                using (Pen railPen = new Pen(Color.FromArgb(20, 255, 255, 255), 2))
                {
                    g.DrawLine(railPen, railX, listArea.Top + 2, railX, listArea.Bottom - 2);
                }

                Rectangle thumbRect = GetThumbRect();
                if (!thumbRect.IsEmpty)
                {
                    Color thumbColor;
                    if (_isDraggingScroll)
                    {
                        thumbColor = Color.FromArgb(215, 175, 255);
                    }
                    else if (_isHoveringScroll)
                    {
                        thumbColor = Color.FromArgb(192, 132, 252);
                    }
                    else
                    {
                        thumbColor = Color.FromArgb(100, 255, 255, 255);
                    }

                    using (GraphicsPath tp = GetRoundedRect(thumbRect, thumbRect.Width / 2))
                    using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
                    {
                        g.FillPath(thumbBrush, tp);
                    }
                }
            }
        }

        private void DrawTab(Graphics g, Rectangle rect, string text, int count, bool isActive, bool isHovered)
        {
            if (isActive)
            {
                using (GraphicsPath p = GetRoundedRect(rect, 6))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(50, 168, 85, 247)))
                using (Pen pen = new Pen(Color.FromArgb(90, 168, 85, 247), 1))
                {
                    g.FillPath(b, p);
                    g.DrawPath(pen, p);
                }

                // Bottom active indicator line
                using (SolidBrush lineBrush = new SolidBrush(Color.FromArgb(192, 132, 252)))
                {
                    g.FillRectangle(lineBrush, rect.X + 12, rect.Bottom - 2, rect.Width - 24, 2);
                }
            }
            else if (isHovered)
            {
                using (GraphicsPath p = GetRoundedRect(rect, 6))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(25, 255, 255, 255)))
                {
                    g.FillPath(b, p);
                }
            }

            using (Font f = new Font("Segoe UI", 9f, isActive ? FontStyle.Bold : FontStyle.Regular))
            using (SolidBrush tb = new SolidBrush(isActive ? Color.White : (isHovered ? Color.FromArgb(220, 220, 230) : Color.FromArgb(160, 160, 175))))
            using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                string displayText = (count > 0) ? string.Format("{0} ({1})", text, count) : text;
                g.DrawString(displayText, f, tb, rect, sf);
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
