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
        Collection,
        History,
        Saved
    }

    public class PlaylistPopup : Form
    {
        private MenuTab _activeTab = MenuTab.Playlist;
        private List<PlaylistItem> _playlistItems = new List<PlaylistItem>();
        private List<PlaylistItem> _historyItems = new List<PlaylistItem>();
        private List<PlaylistItem> _savedItems = new List<PlaylistItem>();
        private List<UserPlaylist> _collectionPlaylists = new List<UserPlaylist>();

        private int _currentPlaylistIndex = -1;

        public List<PlaylistItem> PlaylistItems => _playlistItems;
        public int CurrentPlaylistIndex => _currentPlaylistIndex;
        private int _hoveredRowIndex = -1;
        private int _hoveredTabIndex = -1; // 0: Playlist, 1: Collection, 2: History, 3: Saved
        private bool _isHoveringClose = false;
        private bool _isLoading = false;

        private PlaylistSearchBox _searchTracksBox;
        private PlaylistSearchBox _searchCollectionsBox;
        private PlaylistSearchBox _searchCollectionTracksBox;

        private string _searchTracksQuery = string.Empty;
        private string _searchCollectionsQuery = string.Empty;
        private string _searchCollectionTracksQuery = string.Empty;

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

        private static bool MatchesFilter(string source, string query)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(query)) return false;
            if (source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            string cleanSource = RemoveDiacritics(source);
            string cleanQuery = RemoveDiacritics(query);
            return cleanSource.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private List<PlaylistItem> FilteredItems
        {
            get
            {
                var raw = ActiveItems;
                if (string.IsNullOrWhiteSpace(_searchTracksQuery)) return raw;
                return raw.FindAll(x => MatchesFilter(x.Title, _searchTracksQuery) || MatchesFilter(x.Author, _searchTracksQuery));
            }
        }

        private List<UserPlaylist> FilteredCollectionPlaylists
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_searchCollectionsQuery)) return _collectionPlaylists;
                return _collectionPlaylists.FindAll(x => MatchesFilter(x.Name, _searchCollectionsQuery));
            }
        }

        private List<PlaylistItem> GetFilteredCollectionTracks(UserPlaylist curPlaylist)
        {
            if (curPlaylist == null || curPlaylist.Items == null) return new List<PlaylistItem>();
            if (string.IsNullOrWhiteSpace(_searchCollectionTracksQuery)) return curPlaylist.Items;
            return curPlaylist.Items.FindAll(x => MatchesFilter(x.Title, _searchCollectionTracksQuery) || MatchesFilter(x.Author, _searchCollectionTracksQuery));
        }

        // Scrolling for Playlist, History, Saved
        private int _scrollOffset = 0;
        private bool _isDraggingScroll = false;
        private bool _isHoveringScroll = false;
        private int _dragGrabOffsetY = 0;

        // Collection tab state (2 vertical columns)
        private const int LeftColWidth = 152;
        private const int CollectionRowHeight = 44;
        private const int CollectionTrackRowHeight = 34;

        private int _selectedPlaylistIndex = 0;
        private int _hoveredPlaylistIndex = -1;
        private int _hoveredCollectionTrackIndex = -1;
        private int _collectionLeftScroll = 0;
        private int _collectionRightScroll = 0;
        private bool _isHoveringNewPlaylistBtn = false;
        private bool _isHoveringPlayAllBtn = false;
        private bool _isHoveringDownloadAllBtn = false;

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
                    _collectionLeftScroll = 0;
                    _collectionRightScroll = 0;
                    UpdateSearchBoxesVisibility();
                    ClampScroll();
                    ClampCollectionScroll();
                    Invalidate();
                }
            }
        }

        private List<PlaylistItem> ActiveItems
        {
            get
            {
                if (_activeTab == MenuTab.Playlist) return _playlistItems;
                if (_activeTab == MenuTab.History) return _historyItems;
                if (_activeTab == MenuTab.Saved) return _savedItems;
                return _playlistItems;
            }
        }

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
            Size = new Size(460, 340);
            DoubleBuffered = true;
            KeyPreview = true;

            _historyItems = HistoryService.GetHistory();
            _savedItems = SavedTracksService.Instance.GetSavedTracks();
            _collectionPlaylists = PlaylistCollectionService.Instance.GetPlaylists();

            _searchTracksBox = new PlaylistSearchBox("Tìm kiếm bài hát trong danh sách...");
            _searchTracksBox.Location = new Point(10, HeaderHeight + 5);
            _searchTracksBox.Size = new Size(Width - 20, 24);
            _searchTracksBox.SearchTextChanged += (s, q) =>
            {
                _searchTracksQuery = q;
                _scrollOffset = 0;
                ClampScroll();
                Invalidate();
            };
            Controls.Add(_searchTracksBox);

            _searchCollectionsBox = new PlaylistSearchBox("Tìm danh sách...");
            _searchCollectionsBox.Location = new Point(8, HeaderHeight + 29);
            _searchCollectionsBox.Size = new Size(LeftColWidth, 22);
            _searchCollectionsBox.SearchTextChanged += (s, q) =>
            {
                _searchCollectionsQuery = q;
                _collectionLeftScroll = 0;
                ClampCollectionScroll();
                Invalidate();
            };
            _searchCollectionsBox.Visible = false;
            Controls.Add(_searchCollectionsBox);

            int rightColX = 8 + LeftColWidth + 8;
            int rightColW = Width - rightColX - 8;
            _searchCollectionTracksBox = new PlaylistSearchBox("Tìm bài hát...");
            _searchCollectionTracksBox.Location = new Point(rightColX, HeaderHeight + 29);
            _searchCollectionTracksBox.Size = new Size(rightColW, 22);
            _searchCollectionTracksBox.SearchTextChanged += (s, q) =>
            {
                _searchCollectionTracksQuery = q;
                _collectionRightScroll = 0;
                ClampCollectionScroll();
                Invalidate();
            };
            _searchCollectionTracksBox.Visible = false;
            Controls.Add(_searchCollectionTracksBox);

            UpdateSearchBoxesVisibility();

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

            SavedTracksService.Instance.SavedTracksChanged += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            _savedItems = SavedTracksService.Instance.GetSavedTracks();
                            if (_activeTab == MenuTab.Saved) ClampScroll();
                            Invalidate();
                        }));
                    }
                    catch { }
                }
                else
                {
                    _savedItems = SavedTracksService.Instance.GetSavedTracks();
                }
            };

            PlaylistCollectionService.Instance.PlaylistsChanged += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            _collectionPlaylists = PlaylistCollectionService.Instance.GetPlaylists();
                            if (_selectedPlaylistIndex >= _collectionPlaylists.Count)
                            {
                                _selectedPlaylistIndex = Math.Max(0, _collectionPlaylists.Count - 1);
                            }
                            ClampCollectionScroll();
                            Invalidate();
                        }));
                    }
                    catch { }
                }
                else
                {
                    _collectionPlaylists = PlaylistCollectionService.Instance.GetPlaylists();
                }
            };
        }

                private void UpdateSearchBoxesVisibility()
        {
            bool isCol = (_activeTab == MenuTab.Collection);
            if (_searchTracksBox != null)
            {
                _searchTracksBox.Visible = !isCol;
                if (isCol)
                {
                    _searchTracksBox.Clear();
                    _searchTracksQuery = string.Empty;
                }
            }
            if (_searchCollectionsBox != null)
            {
                _searchCollectionsBox.Visible = isCol;
                if (!isCol)
                {
                    _searchCollectionsBox.Clear();
                    _searchCollectionsQuery = string.Empty;
                }
            }
            if (_searchCollectionTracksBox != null)
            {
                _searchCollectionTracksBox.Visible = isCol;
                if (!isCol)
                {
                    _searchCollectionTracksBox.Clear();
                    _searchCollectionTracksQuery = string.Empty;
                }
            }
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
                return cp;
            }
        }

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                Point clientPt = PointToClient(Cursor.Position);
                bool inSearch = (_searchTracksBox != null && _searchTracksBox.Visible && _searchTracksBox.Bounds.Contains(clientPt)) ||
                                (_searchCollectionsBox != null && _searchCollectionsBox.Visible && _searchCollectionsBox.Bounds.Contains(clientPt)) ||
                                (_searchCollectionTracksBox != null && _searchCollectionTracksBox.Visible && _searchCollectionTracksBox.Bounds.Contains(clientPt));
                if (inSearch)
                {
                    base.WndProc(ref m);
                    return;
                }
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateSearchBoxesVisibility();
            RefreshHistory();
            RefreshSaved();
            RefreshPlaylists();
            UpdateRegion();
        }

        public void RefreshPlaylists()
        {
            _collectionPlaylists = PlaylistCollectionService.Instance.GetPlaylists();
            if (_selectedPlaylistIndex >= _collectionPlaylists.Count)
            {
                _selectedPlaylistIndex = Math.Max(0, _collectionPlaylists.Count - 1);
            }
            ClampCollectionScroll();
            Invalidate();
        }

        public void RefreshSaved()
        {
            _savedItems = SavedTracksService.Instance.GetSavedTracks();
            if (_activeTab == MenuTab.Saved) ClampScroll();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_searchTracksBox != null)
            {
                _searchTracksBox.Size = new Size(Width - 20, 24);
            }
            int leftColW = LeftColWidth;
            int rightColX = 8 + leftColW + 8;
            int rightColW = Width - rightColX - 8;
            if (_searchCollectionsBox != null)
            {
                _searchCollectionsBox.Location = new Point(8, HeaderHeight + 29);
                _searchCollectionsBox.Size = new Size(leftColW, 22);
            }
            if (_searchCollectionTracksBox != null)
            {
                _searchCollectionTracksBox.Location = new Point(rightColX, HeaderHeight + 29);
                _searchCollectionTracksBox.Size = new Size(rightColW, 22);
            }
            UpdateRegion();
            ClampScroll();
            ClampCollectionScroll();
            Invalidate();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _lastDeactivatedTime = DateTime.UtcNow;
            if (Owner is Form1 f1 && f1.IsFormClosed) { Hide(); return; }
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
                if (_activeTab == MenuTab.Collection)
                {
                    _collectionRightScroll += CollectionTrackRowHeight;
                    ClampCollectionScroll();
                }
                else
                {
                    _scrollOffset += RowHeight;
                    ClampScroll();
                }
                UpdateHover(PointToClient(Cursor.Position));
                Invalidate();
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (_activeTab == MenuTab.Collection)
                {
                    _collectionRightScroll -= CollectionTrackRowHeight;
                    ClampCollectionScroll();
                }
                else
                {
                    _scrollOffset -= RowHeight;
                    ClampScroll();
                }
                UpdateHover(PointToClient(Cursor.Position));
                Invalidate();
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                if (_activeTab == MenuTab.Collection)
                {
                    _collectionRightScroll += CollectionTrackRowHeight * 4;
                    ClampCollectionScroll();
                }
                else
                {
                    _scrollOffset += RowHeight * 4;
                    ClampScroll();
                }
                UpdateHover(PointToClient(Cursor.Position));
                Invalidate();
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                if (_activeTab == MenuTab.Collection)
                {
                    _collectionRightScroll -= CollectionTrackRowHeight * 4;
                    ClampCollectionScroll();
                }
                else
                {
                    _scrollOffset -= RowHeight * 4;
                    ClampScroll();
                }
                UpdateHover(PointToClient(Cursor.Position));
                Invalidate();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                UpdateSearchBoxesVisibility();
                RefreshHistory();
                RefreshSaved();
                RefreshPlaylists();
                ClampScroll();
                ClampCollectionScroll();
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
            if (_activeTab == MenuTab.Collection) return;
            int idx = -1;
            string curId = AudioPlayer?.CurrentVideoId;
            var list = ActiveItems;
            if (!string.IsNullOrEmpty(curId) && list != null && list.Count > 0)
            {
                idx = list.FindIndex(x => string.Equals(x.Id, curId, StringComparison.OrdinalIgnoreCase));
            }
            if (idx < 0) return;
            int viewHeight = Height - HeaderHeight - 8;
            int itemTop = idx * RowHeight;
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
            if (_activeTab == MenuTab.Collection) return;
            int listTop = HeaderHeight + 33;
            int totalHeight = FilteredItems.Count * RowHeight;
            int viewHeight = Height - listTop - 6;
            int maxScroll = Math.Max(0, totalHeight - viewHeight);
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, maxScroll));
        }

        private void ClampCollectionScroll()
        {
            int leftListTop = HeaderHeight + 55;
            int listH = Height - leftListTop - 6;

            // Clamp left scroll
            int leftTotalH = FilteredCollectionPlaylists.Count * CollectionRowHeight;
            int maxLeftScroll = Math.Max(0, leftTotalH - listH);
            _collectionLeftScroll = Math.Max(0, Math.Min(_collectionLeftScroll, maxLeftScroll));

            // Clamp right scroll
            UserPlaylist curP = GetSelectedCollectionPlaylist();
            var colTracks = GetFilteredCollectionTracks(curP);
            int trackCount = colTracks.Count;
            int rightTotalH = trackCount * CollectionTrackRowHeight;
            int maxRightScroll = Math.Max(0, rightTotalH - listH);
            _collectionRightScroll = Math.Max(0, Math.Min(_collectionRightScroll, maxRightScroll));
        }

        private UserPlaylist GetSelectedCollectionPlaylist()
        {
            if (_collectionPlaylists != null && _selectedPlaylistIndex >= 0 && _selectedPlaylistIndex < _collectionPlaylists.Count)
            {
                return _collectionPlaylists[_selectedPlaylistIndex];
            }
            return null;
        }

        // Tab Rectangles
        private Rectangle GetTabRect(int index)
        {
            switch (index)
            {
                case 0: return new Rectangle(8, 7, 120, 28);   // Danh sách phát
                case 1: return new Rectangle(132, 7, 98, 28);  // Bộ sưu tập
                case 2: return new Rectangle(234, 7, 82, 28);  // Lịch sử
                case 3: return new Rectangle(320, 7, 82, 28);  // Đã tải
                default: return Rectangle.Empty;
            }
        }

        private Rectangle GetNewPlaylistBtnRect()
        {
            return new Rectangle(8 + LeftColWidth - 44, HeaderHeight + 4, 40, 20);
        }

        private Rectangle GetPlayAllBtnRect()
        {
            return new Rectangle(Width - 100, HeaderHeight + 4, 46, 20);
        }

        private Rectangle GetDownloadAllBtnRect()
        {
            return new Rectangle(Width - 50, HeaderHeight + 4, 42, 20);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (_activeTab == MenuTab.Collection)
            {
                if (e.X < 8 + LeftColWidth + 6)
                {
                    _collectionLeftScroll -= (e.Delta / 120) * (CollectionRowHeight * 2);
                }
                else
                {
                    _collectionRightScroll -= (e.Delta / 120) * (CollectionTrackRowHeight * 2);
                }
                ClampCollectionScroll();
                UpdateHover(PointToClient(Cursor.Position));
                Invalidate();
                return;
            }

            _scrollOffset -= (e.Delta / 120) * (RowHeight * 2);
            ClampScroll();
            UpdateHover(PointToClient(Cursor.Position));
            Invalidate();
        }

        private Rectangle GetThumbRect()
        {
            if (_activeTab == MenuTab.Collection) return Rectangle.Empty;

            int listTop = HeaderHeight + 33;
            int totalContentHeight = FilteredItems.Count * RowHeight;
            Rectangle listArea = new Rectangle(6, listTop, Width - 12, Height - listTop - 6);
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

            if (_isDraggingScroll && _activeTab != MenuTab.Collection)
            {
                int listTop = HeaderHeight + 33;
                Rectangle listArea = new Rectangle(6, listTop, Width - 12, Height - listTop - 6);
                int totalContentHeight = FilteredItems.Count * RowHeight;
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
            int oldHoverTab = _hoveredTabIndex;
            _hoveredTabIndex = -1;
            for (int t = 0; t < 4; t++)
            {
                if (GetTabRect(t).Contains(pt))
                {
                    _hoveredTabIndex = t;
                    break;
                }
            }

            if (_activeTab == MenuTab.Collection)
            {
                // Collection tab hover states
                bool oldNewPl = _isHoveringNewPlaylistBtn;
                bool oldPlayAll = _isHoveringPlayAllBtn;
                bool oldDlAll = _isHoveringDownloadAllBtn;
                int oldPlHover = _hoveredPlaylistIndex;
                int oldTrackHover = _hoveredCollectionTrackIndex;

                _isHoveringNewPlaylistBtn = GetNewPlaylistBtnRect().Contains(pt);
                _isHoveringPlayAllBtn = GetPlayAllBtnRect().Contains(pt);
                _isHoveringDownloadAllBtn = GetDownloadAllBtnRect().Contains(pt);

                // Left column playlist items hover
                int colListTop = HeaderHeight + 55;
                int listBottom = Height - 6;
                int leftColRight = 8 + LeftColWidth;

                var filteredPlaylists = FilteredCollectionPlaylists;
                if (pt.X >= 8 && pt.X <= leftColRight && pt.Y >= colListTop && pt.Y <= listBottom)
                {
                    int relY = pt.Y - colListTop + _collectionLeftScroll;
                    int idx = relY / CollectionRowHeight;
                    _hoveredPlaylistIndex = (idx >= 0 && idx < filteredPlaylists.Count) ? idx : -1;
                }
                else
                {
                    _hoveredPlaylistIndex = -1;
                }

                // Right column track items hover
                UserPlaylist curP = GetSelectedCollectionPlaylist();
                var filteredTracks = GetFilteredCollectionTracks(curP);
                int trackCount = filteredTracks.Count;
                int rightColLeft = leftColRight + 8;

                if (pt.X >= rightColLeft && pt.X <= Width - 8 && pt.Y >= colListTop && pt.Y <= listBottom)
                {
                    int relY = pt.Y - colListTop + _collectionRightScroll;
                    int idx = relY / CollectionTrackRowHeight;
                    _hoveredCollectionTrackIndex = (idx >= 0 && idx < trackCount) ? idx : -1;
                }
                else
                {
                    _hoveredCollectionTrackIndex = -1;
                }

                if (oldCloseHover != _isHoveringClose || oldHoverTab != _hoveredTabIndex ||
                    oldNewPl != _isHoveringNewPlaylistBtn || oldPlayAll != _isHoveringPlayAllBtn ||
                    oldDlAll != _isHoveringDownloadAllBtn || oldPlHover != _hoveredPlaylistIndex ||
                    oldTrackHover != _hoveredCollectionTrackIndex)
                {
                    Invalidate();
                }
                return;
            }

            // Scrollbar hover for standard tabs
            int listTop = HeaderHeight + 33;
            Rectangle listArea = new Rectangle(6, listTop, Width - 12, Height - listTop - 6);
            Rectangle scrollHitArea = new Rectangle(Width - 18, listArea.Top, 18, listArea.Height);
            bool oldScrollHover = _isHoveringScroll;
            _isHoveringScroll = scrollHitArea.Contains(pt);

            // Row hover
            int oldHover = _hoveredRowIndex;
            if (!_isHoveringScroll && pt.Y >= listTop && pt.Y <= Height - 6 && pt.X >= 8 && pt.X <= Width - 18)
            {
                int relY = pt.Y - listTop + _scrollOffset;
                int idx = relY / RowHeight;
                _hoveredRowIndex = (idx >= 0 && idx < FilteredItems.Count) ? idx : -1;
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
                _hoveredPlaylistIndex = -1;
                _hoveredCollectionTrackIndex = -1;
                _isHoveringNewPlaylistBtn = false;
                _isHoveringPlayAllBtn = false;
                _isHoveringDownloadAllBtn = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            Rectangle tab0Rect = GetTabRect(0); // Danh sách phát
            Rectangle tab1Rect = GetTabRect(1); // Bộ sưu tập
            Rectangle tab2Rect = GetTabRect(2); // Lịch sử
            Rectangle tab3Rect = GetTabRect(3); // Đã tải
            Rectangle closeRect = new Rectangle(Width - 32, 10, 22, 22);

            if (e.Button == MouseButtons.Right)
            {
                // Right click on Tab 0 (Danh sách phát) -> Context menu: Lưu vào bộ sưu tập, Tải toàn bộ, Xóa
                if (tab0Rect.Contains(e.Location))
                {
                    ShowPlaylistTabContextMenu(PointToScreen(new Point(tab0Rect.Left, tab0Rect.Bottom + 2)));
                    return;
                }

                // Right click on Tab 1 (Bộ sưu tập) -> Context menu: Tạo mới, Mở thư mục
                if (tab1Rect.Contains(e.Location))
                {
                    ShowCollectionTabContextMenu(PointToScreen(new Point(tab1Rect.Left, tab1Rect.Bottom + 2)));
                    return;
                }

                // Right click on Tab 2 (Lịch sử) -> Context menu: Xóa toàn bộ lịch sử
                if (tab2Rect.Contains(e.Location))
                {
                    ShowHistoryTabContextMenu(PointToScreen(new Point(tab2Rect.Left, tab2Rect.Bottom + 2)));
                    return;
                }

                // Right click in Collection tab
                if (_activeTab == MenuTab.Collection)
                {
                    int colListTop = HeaderHeight + 55;
                    int listBottom = Height - 6;
                    int leftColRight = 8 + LeftColWidth;

                    // Right click on Left Column playlist item
                    if (e.X >= 8 && e.X <= leftColRight && e.Y >= colListTop && e.Y <= listBottom)
                    {
                        int relY = e.Y - colListTop + _collectionLeftScroll;
                        int idx = relY / CollectionRowHeight;
                        var filteredPlaylists = FilteredCollectionPlaylists;
                        if (idx >= 0 && idx < filteredPlaylists.Count)
                        {
                            UserPlaylist clickedPl = filteredPlaylists[idx];
                            _selectedPlaylistIndex = _collectionPlaylists.FindIndex(p => p.Id == clickedPl.Id);
                            Invalidate();
                            ShowUserPlaylistContextMenu(clickedPl, PointToScreen(e.Location));
                            return;
                        }
                    }

                    // Right click on Right Column track item
                    UserPlaylist curP = GetSelectedCollectionPlaylist();
                    if (curP != null && curP.Items != null)
                    {
                        int rightColLeft = leftColRight + 8;
                        if (e.X >= rightColLeft && e.X <= Width - 8 && e.Y >= colListTop && e.Y <= listBottom)
                        {
                            int relY = e.Y - colListTop + _collectionRightScroll;
                            int idx = relY / CollectionTrackRowHeight;
                            var filteredTracks = GetFilteredCollectionTracks(curP);
                            if (idx >= 0 && idx < filteredTracks.Count)
                            {
                                PlaylistItem clickedTrack = filteredTracks[idx];
                                int realTrackIdx = curP.Items.FindIndex(t => string.Equals(t.Id, clickedTrack.Id, StringComparison.OrdinalIgnoreCase));
                                ShowCollectionTrackContextMenu(curP, realTrackIdx >= 0 ? realTrackIdx : 0, PointToScreen(e.Location));
                                return;
                            }
                        }
                    }

                    return;
                }

                // Right click on a song item in standard tabs (Playlist, History, Saved)
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

            // 2. Tab 0 clicked (Danh sách phát)
            if (tab0Rect.Contains(e.Location))
            {
                if (_activeTab != MenuTab.Playlist)
                {
                    ActiveTab = MenuTab.Playlist;
                }
                return;
            }

            // 3. Tab 1 clicked (Bộ sưu tập)
            if (tab1Rect.Contains(e.Location))
            {
                if (_activeTab != MenuTab.Collection)
                {
                    ActiveTab = MenuTab.Collection;
                    RefreshPlaylists();
                }
                return;
            }

            // 4. Tab 2 clicked (Lịch sử)
            if (tab2Rect.Contains(e.Location))
            {
                if (_activeTab != MenuTab.History)
                {
                    ActiveTab = MenuTab.History;
                    RefreshHistory();
                }
                return;
            }

            // 5. Tab 3 clicked (Đã tải - Saved)
            if (tab3Rect.Contains(e.Location))
            {
                if (_activeTab != MenuTab.Saved)
                {
                    ActiveTab = MenuTab.Saved;
                    RefreshSaved();
                }
                return;
            }

            // 6. Interactions inside Collection Tab
            if (_activeTab == MenuTab.Collection)
            {
                // New Playlist button
                if (GetNewPlaylistBtnRect().Contains(e.Location))
                {
                    PromptCreateNewPlaylist();
                    return;
                }

                UserPlaylist curP = GetSelectedCollectionPlaylist();

                // Play All button
                if (curP != null && GetPlayAllBtnRect().Contains(e.Location))
                {
                    PlayUserPlaylist(curP, 0);
                    return;
                }

                // Download All button
                if (curP != null && GetDownloadAllBtnRect().Contains(e.Location))
                {
                    DownloadUserPlaylist(curP);
                    return;
                }

                int colListTop = HeaderHeight + 55;
                int listBottom = Height - 6;
                int leftColRight = 8 + LeftColWidth;

                // Left column playlist clicked
                if (e.X >= 8 && e.X <= leftColRight && e.Y >= colListTop && e.Y <= listBottom)
                {
                    int relY = e.Y - colListTop + _collectionLeftScroll;
                    int idx = relY / CollectionRowHeight;
                    var filteredPlaylists = FilteredCollectionPlaylists;
                    if (idx >= 0 && idx < filteredPlaylists.Count)
                    {
                        UserPlaylist clickedPl = filteredPlaylists[idx];
                        _selectedPlaylistIndex = _collectionPlaylists.FindIndex(p => p.Id == clickedPl.Id);
                        _collectionRightScroll = 0;
                        ClampCollectionScroll();
                        Invalidate();
                    }
                    return;
                }

                // Right column track clicked -> Play this track from user playlist!
                if (curP != null && curP.Items != null)
                {
                    int rightColLeft = leftColRight + 8;
                    if (e.X >= rightColLeft && e.X <= Width - 8 && e.Y >= colListTop && e.Y <= listBottom)
                    {
                        int relY = e.Y - colListTop + _collectionRightScroll;
                        int idx = relY / CollectionTrackRowHeight;
                        var filteredTracks = GetFilteredCollectionTracks(curP);
                        if (idx >= 0 && idx < filteredTracks.Count)
                        {
                            PlaylistItem clickedTrack = filteredTracks[idx];
                            int realTrackIdx = curP.Items.FindIndex(t => string.Equals(t.Id, clickedTrack.Id, StringComparison.OrdinalIgnoreCase));
                            PlayUserPlaylist(curP, realTrackIdx >= 0 ? realTrackIdx : 0);
                        }
                    }
                }
                return;
            }

            // 7. Scrollbar interaction for standard tabs
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

            // 8. Song row clicked in standard tabs
            var filteredItems = FilteredItems;
            if (_hoveredRowIndex >= 0 && _hoveredRowIndex < filteredItems.Count)
            {
                PlaylistItem clickedItem = filteredItems[_hoveredRowIndex];
                if (clickedItem != null)
                {
                    if (_activeTab == MenuTab.Playlist)
                    {
                        int realIdx = _playlistItems.IndexOf(clickedItem);
                        _currentPlaylistIndex = realIdx >= 0 ? realIdx : _hoveredRowIndex;
                        Invalidate();
                        TrackSelected?.Invoke(this, clickedItem);
                    }
                    else if (_activeTab == MenuTab.History)
                    {
                        if (AudioPlayer != null)
                        {
                            int realIdx = _historyItems.IndexOf(clickedItem);
                            AudioPlayer.PlayHistory(_historyItems, realIdx >= 0 ? realIdx : 0);
                        }
                        Invalidate();
                    }
                    else if (_activeTab == MenuTab.Saved)
                    {
                        if (AudioPlayer != null)
                        {
                            int realIdx = _savedItems.IndexOf(clickedItem);
                            AudioPlayer.PlaySavedTracks(_savedItems, realIdx >= 0 ? realIdx : 0);
                        }
                        Invalidate();
                    }
                    else
                    {
                        if (AudioPlayer != null)
                        {
                            AudioPlayer.IsPlayingFromHistory = false;
                            AudioPlayer.LoadAndPlay(clickedItem.Url);
                        }
                        Invalidate();
                    }
                }
            }
        }

        private int GetSongIndexAt(Point pt)
        {
            int listTop = HeaderHeight + 33;
            if (pt.Y >= listTop && pt.Y <= Height - 6 && pt.X >= 8 && pt.X <= Width - 18)
            {
                int relY = pt.Y - listTop + _scrollOffset;
                int idx = relY / RowHeight;
                if (idx >= 0 && idx < FilteredItems.Count) return idx;
            }
            return -1;
        }

        // ================= CONTEXT MENUS =================

        private void ShowPlaylistTabContextMenu(Point screenLocation)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(24, 24, 28);
            menu.ForeColor = Color.FromArgb(235, 235, 235);
            menu.Renderer = new Form1.DarkMenuRenderer();
            menu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            var itemSaveToCollection = new ToolStripMenuItem("💾 Lưu vào bộ sưu tập...");
            itemSaveToCollection.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            itemSaveToCollection.Enabled = _playlistItems != null && _playlistItems.Count > 0;
            itemSaveToCollection.Click += (s, e) =>
            {
                PromptSaveCurrentPlaylistToCollection();
            };

            var itemDownloadAll = new ToolStripMenuItem("📥 Tải toàn bộ danh sách về máy");
            itemDownloadAll.Enabled = _playlistItems != null && _playlistItems.Count > 0;
            itemDownloadAll.Click += (s, e) =>
            {
                DownloadCurrentPlaylistTracks();
            };

            var itemClearAll = new ToolStripMenuItem("🗑️ Xóa sạch danh sách phát");
            itemClearAll.Enabled = _playlistItems != null && _playlistItems.Count > 0;
            itemClearAll.Click += (s, e) =>
            {
                if (AudioPlayer != null)
                {
                    AudioPlayer.ClearPlaylist();
                }
                Clear();
            };

            menu.Items.Add(itemSaveToCollection);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemDownloadAll);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemClearAll);

            menu.Show(screenLocation);
        }

        private void ShowCollectionTabContextMenu(Point screenLocation)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(24, 24, 28);
            menu.ForeColor = Color.FromArgb(235, 235, 235);
            menu.Renderer = new Form1.DarkMenuRenderer();
            menu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            var itemNew = new ToolStripMenuItem("➕ Tạo bộ sưu tập mới...");
            itemNew.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            itemNew.Click += (s, e) =>
            {
                PromptCreateNewPlaylist();
            };

            var itemOpenFolder = new ToolStripMenuItem("📂 Mở thư mục chứa dữ liệu (Saved)");
            itemOpenFolder.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start("explorer.exe", AppPaths.SavedDir); } catch { }
            };

            menu.Items.Add(itemNew);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemOpenFolder);

            menu.Show(screenLocation);
        }

        private void ShowUserPlaylistContextMenu(UserPlaylist playlist, Point screenLocation)
        {
            if (playlist == null) return;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(24, 24, 28);
            menu.ForeColor = Color.FromArgb(235, 235, 235);
            menu.Renderer = new Form1.DarkMenuRenderer();
            menu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            var itemPlay = new ToolStripMenuItem("▶ Phát tất cả (" + playlist.Items.Count + " bài)");
            itemPlay.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            itemPlay.Enabled = playlist.Items != null && playlist.Items.Count > 0;
            itemPlay.Click += (s, e) =>
            {
                PlayUserPlaylist(playlist, 0);
            };

            var itemDownload = new ToolStripMenuItem("📥 Tải toàn bộ bài về máy");
            itemDownload.Enabled = playlist.Items != null && playlist.Items.Count > 0;
            itemDownload.Click += (s, e) =>
            {
                DownloadUserPlaylist(playlist);
            };

            var itemRename = new ToolStripMenuItem("✏️ Đổi tên...");
            itemRename.Click += (s, e) =>
            {
                string newName = InputDialog.Show(this, "Đổi tên bộ sưu tập", "Nhập tên mới:", playlist.Name);
                if (!string.IsNullOrWhiteSpace(newName) && newName.Trim() != playlist.Name)
                {
                    PlaylistCollectionService.Instance.RenamePlaylist(playlist.Id, newName.Trim());
                    RefreshPlaylists();
                }
            };

            var itemDelete = new ToolStripMenuItem("🗑️ Xóa bộ sưu tập");
            itemDelete.Click += (s, e) =>
            {
                bool ok = CyberConfirmDialog.ShowConfirm(
                    "Xác nhận xóa bộ sưu tập",
                    "Bạn có chắc chắn muốn xóa bộ sưu tập '" + playlist.Name + "' không?\n\n(Thao tác này sẽ xóa danh sách trên cả máy và đám mây)",
                    "Xóa ngay",
                    "Hủy",
                    this.FindForm());
                if (ok)
                {
                    PlaylistCollectionService.Instance.DeletePlaylist(playlist.Id);
                    RefreshPlaylists();
                }
            };

            menu.Items.Add(itemPlay);
            menu.Items.Add(itemDownload);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemRename);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemDelete);

            menu.Show(screenLocation);
        }

        private void ShowCollectionTrackContextMenu(UserPlaylist playlist, int trackIndex, Point screenLocation)
        {
            if (playlist == null || playlist.Items == null || trackIndex < 0 || trackIndex >= playlist.Items.Count) return;
            PlaylistItem item = playlist.Items[trackIndex];

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(24, 24, 28);
            menu.ForeColor = Color.FromArgb(235, 235, 235);
            menu.Renderer = new Form1.DarkMenuRenderer();
            menu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            var itemPlay = new ToolStripMenuItem("▶ Phát từ bài này");
            itemPlay.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            itemPlay.Click += (s, e) =>
            {
                PlayUserPlaylist(playlist, trackIndex);
            };

            var itemAddQueue = new ToolStripMenuItem("➕ Thêm vào danh sách phát hiện tại");
            itemAddQueue.Click += (s, e) =>
            {
                AudioPlayer?.AddToPlaylist(item);
                Invalidate();
            };

            bool isSaved = SavedTracksService.Instance.IsTrackSaved(item.Id);
            var itemDownload = new ToolStripMenuItem(isSaved ? "✓ Đã lưu Offline (Saved)" : "📥 Tải về nghe Offline");
            itemDownload.Enabled = !isSaved;
            itemDownload.Click += (s, e) =>
            {
                AudioPlayer?.RaiseStatus("Đang lưu bài hát về máy: " + item.Title + "...");
                SavedTracksService.Instance.SaveTrackAsync(item, (success, msg) =>
                {
                    AudioPlayer?.RaiseStatus(success ? ("✓ Đã lưu bài hát: " + item.Title) : ("Lỗi lưu bài: " + msg));
                });
            };

            var itemRemove = new ToolStripMenuItem("❌ Xóa khỏi bộ sưu tập này");
            itemRemove.Click += (s, e) =>
            {
                PlaylistCollectionService.Instance.RemoveTrackFromPlaylist(playlist.Id, item.Id);
                if (AudioPlayer != null && (string.Equals(AudioPlayer.CurrentPlaylistId, playlist.Id, StringComparison.OrdinalIgnoreCase) || string.Equals(AudioPlayer.CurrentPlaylistTitle, playlist.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    AudioPlayer.RemoveTrackById(item.Id);
                }
                RefreshPlaylists();
            };

            menu.Items.Add(itemPlay);
            menu.Items.Add(itemAddQueue);
            menu.Items.Add(new ToolStripSeparator());
            AppendAddToCollectionSubmenu(menu, item);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemDownload);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemRemove);

            menu.Show(screenLocation);
        }

        private void AppendAddToCollectionSubmenu(ContextMenuStrip menu, PlaylistItem track)
        {
            if (track == null || string.IsNullOrWhiteSpace(track.Id)) return;

            var itemAddToColl = new ToolStripMenuItem("➕ Thêm vào Bộ sưu tập");

            var itemNewColl = new ToolStripMenuItem("[+ Tạo bộ sưu tập mới...]");
            itemNewColl.Click += (s, e) =>
            {
                string name = InputDialog.Show(this, "Bộ sưu tập mới", "Nhập tên bộ sưu tập:");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var p = PlaylistCollectionService.Instance.CreatePlaylist(name.Trim());
                    PlaylistCollectionService.Instance.AddTrackToPlaylist(p.Id, track);
                    AudioPlayer?.RaiseStatus("✓ Đã thêm bài hát vào '" + p.Name + "'");
                    RefreshPlaylists();
                }
            };
            itemAddToColl.DropDownItems.Add(itemNewColl);

            if (_collectionPlaylists != null && _collectionPlaylists.Count > 0)
            {
                itemAddToColl.DropDownItems.Add(new ToolStripSeparator());
                foreach (var p in _collectionPlaylists)
                {
                    var pItem = new ToolStripMenuItem(p.Name);
                    string targetId = p.Id;
                    string targetName = p.Name;
                    pItem.Click += (s, e) =>
                    {
                        PlaylistCollectionService.Instance.AddTrackToPlaylist(targetId, track);
                        AudioPlayer?.RaiseStatus("✓ Đã thêm bài hát vào '" + targetName + "'");
                        RefreshPlaylists();
                    };
                    itemAddToColl.DropDownItems.Add(pItem);
                }
            }

            menu.Items.Add(itemAddToColl);
        }

        private void ShowSongContextMenu(int index, Point screenLocation)
        {
            var filtered = FilteredItems;
            if (index < 0 || index >= filtered.Count) return;
            PlaylistItem item = filtered[index];

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(24, 24, 28);
            menu.ForeColor = Color.FromArgb(235, 235, 235);
            menu.Renderer = new Form1.DarkMenuRenderer();
            menu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            if (_activeTab == MenuTab.Saved)
            {
                var itemPlay = new ToolStripMenuItem("Phát ngay");
                itemPlay.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                itemPlay.Click += (s, e) =>
                {
                    if (AudioPlayer != null)
                    {
                        int sIdx = _savedItems.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                        AudioPlayer.PlaySavedTracks(_savedItems, sIdx >= 0 ? sIdx : 0);
                    }
                };

                var itemAdd = new ToolStripMenuItem("Thêm vào danh sách phát");
                itemAdd.Click += (s, e) =>
                {
                    if (AudioPlayer != null)
                    {
                        AudioPlayer.AddToPlaylist(item);
                        Invalidate();
                    }
                };

                var itemOpenFolder = new ToolStripMenuItem("Mở thư mục Saved");
                itemOpenFolder.Click += (s, e) =>
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", AppPaths.SavedAudioDir); } catch { }
                };

                var itemDelete = new ToolStripMenuItem("Xóa khỏi mục Đã tải");
                itemDelete.Click += (s, e) =>
                {
                    SavedTracksService.Instance.DeleteSavedTrack(item.Id);
                };

                menu.Items.Add(itemPlay);
                menu.Items.Add(itemAdd);
                menu.Items.Add(new ToolStripSeparator());
                AppendAddToCollectionSubmenu(menu, item);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(itemOpenFolder);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(itemDelete);
            }
            else if (_activeTab == MenuTab.History)
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
                        AudioPlayer.PlayHistory(_historyItems, index);
                    }
                };

                bool isSaved = SavedTracksService.Instance.IsTrackSaved(item.Id);
                var itemDownload = new ToolStripMenuItem(isSaved ? "✓ Đã lưu Offline (Saved)" : "📥 Tải về nghe Offline");
                itemDownload.Enabled = !isSaved;
                itemDownload.Click += (s, e) =>
                {
                    if (AudioPlayer != null)
                    {
                        AudioPlayer.RaiseStatus("Đang lưu bài hát về máy: " + item.Title + "...");
                    }
                    SavedTracksService.Instance.SaveTrackAsync(item, (success, msg) =>
                    {
                        if (AudioPlayer != null)
                        {
                            AudioPlayer.RaiseStatus(success ? ("✓ Đã lưu bài hát: " + item.Title) : ("Lỗi lưu bài: " + msg));
                        }
                    });
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
                AppendAddToCollectionSubmenu(menu, item);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(itemDownload);
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

                bool isSaved = SavedTracksService.Instance.IsTrackSaved(item.Id);
                var itemDownload = new ToolStripMenuItem(isSaved ? "✓ Đã lưu Offline (Saved)" : "📥 Tải về nghe Offline");
                itemDownload.Enabled = !isSaved;
                itemDownload.Click += (s, e) =>
                {
                    if (AudioPlayer != null)
                    {
                        AudioPlayer.RaiseStatus("Đang lưu bài hát về máy: " + item.Title + "...");
                    }
                    SavedTracksService.Instance.SaveTrackAsync(item, (success, msg) =>
                    {
                        if (AudioPlayer != null)
                        {
                            AudioPlayer.RaiseStatus(success ? ("✓ Đã lưu bài hát: " + item.Title) : ("Lỗi lưu bài: " + msg));
                        }
                    });
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
                AppendAddToCollectionSubmenu(menu, item);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(itemDownload);
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

            var itemClearAll = new ToolStripMenuItem("Xóa toàn bộ lịch sử");
            itemClearAll.Click += (s, e) =>
            {
                HistoryService.ClearHistory();
                RefreshHistory();
            };

            menu.Items.Add(itemClearAll);
            menu.Show(screenLocation);
        }

        // ================= USER ACTIONS =================

        private void PromptSaveCurrentPlaylistToCollection()
        {
            if (_playlistItems == null || _playlistItems.Count == 0) return;

            string defaultName = AudioPlayer?.CurrentPlaylistTitle;
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = string.Format("Danh sách phát {0:dd/MM/yyyy HH:mm}", DateTime.Now);
            }

            string chosenName = InputDialog.Show(
                this,
                "Lưu vào bộ sưu tập",
                "Nhập tên cho danh sách phát này:",
                defaultName);

            if (!string.IsNullOrWhiteSpace(chosenName))
            {
                var saved = PlaylistCollectionService.Instance.SavePlaylist(chosenName.Trim(), _playlistItems);
                if (saved != null)
                {
                    AudioPlayer?.RaiseStatus("✓ Đã lưu '" + saved.Name + "' (" + saved.Items.Count + " bài) vào Bộ sưu tập!");
                    _collectionPlaylists = PlaylistCollectionService.Instance.GetPlaylists();
                    _selectedPlaylistIndex = _collectionPlaylists.FindIndex(p => p.Id == saved.Id);
                    if (_selectedPlaylistIndex < 0) _selectedPlaylistIndex = 0;
                    ActiveTab = MenuTab.Collection;
                }
            }
        }

        private void PromptCreateNewPlaylist()
        {
            string name = InputDialog.Show(this, "Tạo bộ sưu tập mới", "Nhập tên bộ sưu tập:");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var p = PlaylistCollectionService.Instance.CreatePlaylist(name.Trim());
                RefreshPlaylists();
                _selectedPlaylistIndex = _collectionPlaylists.FindIndex(x => x.Id == p.Id);
                if (_selectedPlaylistIndex < 0) _selectedPlaylistIndex = 0;
                Invalidate();
            }
        }

        private void PlayUserPlaylist(UserPlaylist playlist, int startIndex = 0)
        {
            if (playlist == null || playlist.Items == null || playlist.Items.Count == 0) return;
            if (AudioPlayer != null)
            {
                AudioPlayer.PlayCollection(playlist.Items, startIndex, playlist.Name, playlist.Id);
                AudioPlayer.RaiseStatus("Đang phát bộ sưu tập: " + playlist.Name);
            }
        }

        private void DownloadUserPlaylist(UserPlaylist playlist)
        {
            if (playlist == null || playlist.Items == null || playlist.Items.Count == 0) return;
            int count = 0;
            foreach (var item in playlist.Items)
            {
                if (!SavedTracksService.Instance.IsTrackSaved(item.Id))
                {
                    count++;
                    SavedTracksService.Instance.SaveTrackAsync(item);
                }
            }
            if (count > 0)
            {
                AudioPlayer?.RaiseStatus(string.Format("Đang bắt đầu tải {0} bài hát từ '{1}'...", count, playlist.Name));
            }
            else
            {
                AudioPlayer?.RaiseStatus("Toàn bộ bài hát trong bộ sưu tập này đã được lưu trong máy.");
            }
        }

        private void DownloadCurrentPlaylistTracks()
        {
            if (_playlistItems == null || _playlistItems.Count == 0) return;
            int count = 0;
            foreach (var item in _playlistItems)
            {
                if (!SavedTracksService.Instance.IsTrackSaved(item.Id))
                {
                    count++;
                    SavedTracksService.Instance.SaveTrackAsync(item);
                }
            }
            if (count > 0)
            {
                AudioPlayer?.RaiseStatus(string.Format("Đang bắt đầu tải {0} bài hát về máy...", count));
            }
            else
            {
                AudioPlayer?.RaiseStatus("Toàn bộ bài hát đã được lưu trong máy.");
            }
        }

        // ================= RENDERING =================

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
            Rectangle tab0Rect = GetTabRect(0);
            Rectangle tab1Rect = GetTabRect(1);
            Rectangle tab2Rect = GetTabRect(2);
            Rectangle tab3Rect = GetTabRect(3);

            DrawTab(g, tab0Rect, "Danh sách phát", _playlistItems.Count, _activeTab == MenuTab.Playlist, _hoveredTabIndex == 0);
            DrawTab(g, tab1Rect, "Bộ sưu tập", _collectionPlaylists.Count, _activeTab == MenuTab.Collection, _hoveredTabIndex == 1);
            DrawTab(g, tab2Rect, "Lịch sử", _historyItems.Count, _activeTab == MenuTab.History, _hoveredTabIndex == 2);
            DrawTab(g, tab3Rect, "Đã tải", _savedItems.Count, _activeTab == MenuTab.Saved, _hoveredTabIndex == 3);

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

            // 3. Collection Tab rendering (2-column session layout)
            if (_activeTab == MenuTab.Collection)
            {
                DrawCollectionTab(g, w, h);
                return;
            }

            // 4. Standard Items List (Playlist, History, Saved)
            if (_activeTab == MenuTab.Playlist && _isLoading)
            {
                using (Font emptyFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (Font hintFont = new Font("Segoe UI", 8.5f))
                using (SolidBrush b1 = new SolidBrush(Color.FromArgb(210, 168, 255)))
                using (SolidBrush b2 = new SolidBrush(Color.FromArgb(160, 160, 175)))
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Đang tải danh sách phát...", emptyFont, b1, new RectangleF(0, HeaderHeight + 50, w, 24), sf);
                    g.DrawString("Vui lòng đợi trong giây lát...", hintFont, b2, new RectangleF(0, HeaderHeight + 76, w, 20), sf);
                }
                return;
            }

            List<PlaylistItem> currentList = FilteredItems;
            int listTop = HeaderHeight + 33;
            if (currentList.Count == 0)
            {
                // Empty state or empty search state
                using (Font emptyFont = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (Font hintFont = new Font("Segoe UI", 8f))
                using (SolidBrush b1 = new SolidBrush(Color.FromArgb(180, 180, 190)))
                using (SolidBrush b2 = new SolidBrush(Color.FromArgb(120, 120, 130)))
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    if (!string.IsNullOrWhiteSpace(_searchTracksQuery))
                    {
                        g.DrawString("Không tìm thấy bài hát nào phù hợp", emptyFont, b1, new RectangleF(0, listTop + 40, w, 24), sf);
                        g.DrawString("Thử tìm kiếm với từ khóa khác", hintFont, b2, new RectangleF(0, listTop + 66, w, 20), sf);
                    }
                    else if (_activeTab == MenuTab.Playlist)
                    {
                        g.DrawString("Chưa có danh sách phát", emptyFont, b1, new RectangleF(0, listTop + 40, w, 24), sf);
                        g.DrawString("Dán link YouTube playlist hoặc chuột phải vào bài hát để thêm", hintFont, b2, new RectangleF(0, listTop + 66, w, 20), sf);
                    }
                    else if (_activeTab == MenuTab.History)
                    {
                        g.DrawString("Chưa có lịch sử phát nhạc", emptyFont, b1, new RectangleF(0, listTop + 40, w, 24), sf);
                        g.DrawString("Các bài hát đã nghe sẽ được lưu tự động tại đây", hintFont, b2, new RectangleF(0, listTop + 66, w, 20), sf);
                    }
                    else
                    {
                        g.DrawString("Chưa có bài hát đã tải", emptyFont, b1, new RectangleF(0, listTop + 40, w, 24), sf);
                        g.DrawString("Chuột phải vào bài hát trong Danh sách hoặc Lịch sử để tải về", hintFont, b2, new RectangleF(0, listTop + 66, w, 20), sf);
                    }
                }
                return;
            }

            // Set clip region for scrollable rows
            Rectangle listArea = new Rectangle(6, listTop, w - 12, h - listTop - 6);
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
                    int rowY = listTop + (i * RowHeight) - _scrollOffset;
                    if (rowY + RowHeight < HeaderHeight || rowY > h) continue;

                    Rectangle rowRect = new Rectangle(8, rowY, w - 16, RowHeight - 2);
                    PlaylistItem item = currentList[i];

                    bool isActive = (AudioPlayer != null && !string.IsNullOrEmpty(AudioPlayer.CurrentVideoId) && string.Equals(item.Id, AudioPlayer.CurrentVideoId, StringComparison.OrdinalIgnoreCase));

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

                    // 1. Playing icon or Index number
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

                    // 2. Micro Thumbnail
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

                    // 3. Title
                    int titleX = rowRect.X + titleXOffset;
                    int titleW = Math.Max(50, rowRect.Right - 46 - titleX - 4);
                    RectangleF titleRect = new RectangleF(titleX, rowRect.Y + 8, titleW, 20);

                    using (StringFormat sfTitle = new StringFormat() { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
                    {
                        string displayTitle = (!string.IsNullOrWhiteSpace(item.Title) && item.Title.Trim() != "None" && item.Title.Trim() != "NA")
                            ? Services.AudioPlayerService.CleanDisplayTitle(item.Title)
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

            // Custom Interactive Scrollbar
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

        private void DrawCollectionTab(Graphics g, int w, int h)
        {
            int listTop = HeaderHeight + 55;
            int listBottom = h - 6;
            int listH = listBottom - listTop;

            int leftColX = 8;
            int leftColW = LeftColWidth;
            int divX = leftColX + leftColW + 4;
            int rightColX = divX + 4;
            int rightColW = w - rightColX - 8;

            // ================= LEFT COLUMN (User Playlists) =================
            // Sub-header
            using (Font subHeaderFont = new Font("Segoe UI", 8.25f, FontStyle.Bold))
            using (SolidBrush subHeaderBrush = new SolidBrush(Color.FromArgb(192, 132, 252)))
            {
                g.DrawString("BỘ SƯU TẬP", subHeaderFont, subHeaderBrush, leftColX + 4, HeaderHeight + 7);
            }

            // Button "+ Mới"
            Rectangle newBtnRect = GetNewPlaylistBtnRect();
            Color newBtnBg = _isHoveringNewPlaylistBtn ? Color.FromArgb(70, 168, 85, 247) : Color.FromArgb(35, 168, 85, 247);
            Color newBtnBorder = _isHoveringNewPlaylistBtn ? Color.FromArgb(216, 180, 254) : Color.FromArgb(168, 85, 247);
            using (GraphicsPath nbp = GetRoundedRect(newBtnRect, 4))
            using (SolidBrush nbb = new SolidBrush(newBtnBg))
            using (Pen nbpPen = new Pen(newBtnBorder, 1))
            {
                g.FillPath(nbb, nbp);
                g.DrawPath(nbpPen, nbp);
            }
            using (Font nbf = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (SolidBrush nbt = new SolidBrush(Color.White))
            using (StringFormat nbsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("+ Mới", nbf, nbt, newBtnRect, nbsf);
            }

            // Left sub-header separator line
            using (Pen sepPen = new Pen(Color.FromArgb(20, 255, 255, 255), 1))
            {
                g.DrawLine(sepPen, leftColX, HeaderHeight + 26, leftColX + leftColW, HeaderHeight + 26);
            }

            // Left column list area
            Rectangle leftListArea = new Rectangle(leftColX, listTop, leftColW, listH);
            g.SetClip(leftListArea);

            var filteredPlaylists = FilteredCollectionPlaylists;
            if (filteredPlaylists.Count == 0)
            {
                using (Font hintF1 = new Font("Segoe UI", 8.25f, FontStyle.Bold))
                using (Font hintF2 = new Font("Segoe UI", 7.5f))
                using (SolidBrush hb1 = new SolidBrush(Color.FromArgb(180, 180, 190)))
                using (SolidBrush hb2 = new SolidBrush(Color.FromArgb(130, 130, 140)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    if (!string.IsNullOrWhiteSpace(_searchCollectionsQuery))
                    {
                        g.DrawString("Không tìm thấy danh sách", hintF1, hb1, new RectangleF(leftColX, listTop + 30, leftColW, 20), sf);
                    }
                    else
                    {
                        g.DrawString("Chưa có bộ sưu tập", hintF1, hb1, new RectangleF(leftColX, listTop + 40, leftColW, 20), sf);
                        g.DrawString("Bấm '+ Mới' hoặc chuột phải tab 'Danh sách phát' để lưu", hintF2, hb2, new RectangleF(leftColX + 4, listTop + 62, leftColW - 8, 40), sf);
                    }
                }
            }
            else
            {
                using (Font plTitleFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (Font plSubFont = new Font("Segoe UI", 7.5f))
                using (SolidBrush textWhite = new SolidBrush(Color.White))
                using (SolidBrush textNormal = new SolidBrush(Color.FromArgb(220, 220, 230)))
                using (SolidBrush textDim = new SolidBrush(Color.FromArgb(145, 145, 160)))
                using (SolidBrush textPurp = new SolidBrush(Color.FromArgb(216, 180, 254)))
                using (StringFormat sfTitle = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    for (int i = 0; i < filteredPlaylists.Count; i++)
                    {
                        int rowY = listTop + (i * CollectionRowHeight) - _collectionLeftScroll;
                        if (rowY + CollectionRowHeight < listTop || rowY > listBottom) continue;

                        Rectangle rowRect = new Rectangle(leftColX + 2, rowY, leftColW - 4, CollectionRowHeight - 3);
                        UserPlaylist p = filteredPlaylists[i];
                        bool isSel = (_selectedPlaylistIndex >= 0 && _selectedPlaylistIndex < _collectionPlaylists.Count && _collectionPlaylists[_selectedPlaylistIndex].Id == p.Id);
                        bool isHov = (i == _hoveredPlaylistIndex);

                        if (isSel)
                        {
                            using (GraphicsPath rp = GetRoundedRect(rowRect, 6))
                            using (SolidBrush activeBg = new SolidBrush(Color.FromArgb(55, 168, 85, 247)))
                            using (Pen activeBorder = new Pen(Color.FromArgb(110, 168, 85, 247), 1))
                            {
                                g.FillPath(activeBg, rp);
                                g.DrawPath(activeBorder, rp);
                            }

                            // Accent left indicator bar
                            using (SolidBrush barBrush = new SolidBrush(Color.FromArgb(192, 132, 252)))
                            {
                                g.FillRectangle(barBrush, rowRect.X + 2, rowRect.Y + 7, 3, rowRect.Height - 14);
                            }
                        }
                        else if (isHov)
                        {
                            using (GraphicsPath rp = GetRoundedRect(rowRect, 6))
                            using (SolidBrush hovBg = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
                            {
                                g.FillPath(hovBg, rp);
                            }
                        }

                        int textX = rowRect.X + (isSel ? 9 : 6);
                        int textW = rowRect.Width - (isSel ? 12 : 9);

                        // Playlist Name
                        RectangleF tf = new RectangleF(textX, rowRect.Y + 5, textW, 16);
                        g.DrawString(p.Name, plTitleFont, isSel ? textWhite : textNormal, tf, sfTitle);

                        // Song count & icon
                        string subText = string.Format("♫ {0} bài", p.Items?.Count ?? 0);
                        RectangleF sfRect = new RectangleF(textX, rowRect.Y + 22, textW, 14);
                        g.DrawString(subText, plSubFont, isSel ? textPurp : textDim, sfRect, sfTitle);
                    }
                }
            }
            g.ResetClip();

            // Vertical divider line between left and right column
            using (Pen divPen = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
            {
                g.DrawLine(divPen, divX, HeaderHeight + 4, divX, listBottom);
            }

            // ================= RIGHT COLUMN (Tracks in Selected Playlist) =================
            UserPlaylist curPlaylist = GetSelectedCollectionPlaylist();

            if (curPlaylist == null)
            {
                using (Font hintF1 = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (SolidBrush hb1 = new SolidBrush(Color.FromArgb(150, 150, 160)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Chọn một bộ sưu tập ở cột bên trái", hintF1, hb1, new RectangleF(rightColX, listTop + 60, rightColW, 24), sf);
                }
                return;
            }

            // Sub-header for selected playlist: Title + [▶ Phát] + [📥 Tải]
            Rectangle playAllBtn = GetPlayAllBtnRect();
            Rectangle dlAllBtn = GetDownloadAllBtnRect();

            using (Font rightHeaderFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (SolidBrush rightHeaderBrush = new SolidBrush(Color.FromArgb(243, 232, 255)))
            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                int titleMaxW = playAllBtn.Left - rightColX - 6;
                RectangleF rf = new RectangleF(rightColX + 4, HeaderHeight + 6, titleMaxW, 20);
                g.DrawString(curPlaylist.Name, rightHeaderFont, rightHeaderBrush, rf, sf);
            }

            // [▶ Phát] Button
            bool hasItems = curPlaylist.Items != null && curPlaylist.Items.Count > 0;
            Color playBg = !hasItems ? Color.FromArgb(20, 255, 255, 255) : (_isHoveringPlayAllBtn ? Color.FromArgb(70, 168, 85, 247) : Color.FromArgb(40, 168, 85, 247));
            Color playBorder = !hasItems ? Color.FromArgb(40, 255, 255, 255) : (_isHoveringPlayAllBtn ? Color.FromArgb(216, 180, 254) : Color.FromArgb(168, 85, 247));
            using (GraphicsPath pbp = GetRoundedRect(playAllBtn, 4))
            using (SolidBrush pbb = new SolidBrush(playBg))
            using (Pen pbpPen = new Pen(playBorder, 1))
            {
                g.FillPath(pbb, pbp);
                g.DrawPath(pbpPen, pbp);
            }
            using (Font pbf = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (SolidBrush pbt = new SolidBrush(hasItems ? Color.White : Color.FromArgb(120, 120, 130)))
            using (StringFormat pbsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("▶ Phát", pbf, pbt, playAllBtn, pbsf);
            }

            // [📥 Tải] Button
            Color dlBg = !hasItems ? Color.FromArgb(20, 255, 255, 255) : (_isHoveringDownloadAllBtn ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(25, 255, 255, 255));
            Color dlBorder = !hasItems ? Color.FromArgb(30, 255, 255, 255) : (_isHoveringDownloadAllBtn ? Color.FromArgb(190, 190, 210) : Color.FromArgb(80, 255, 255, 255));
            using (GraphicsPath dbp = GetRoundedRect(dlAllBtn, 4))
            using (SolidBrush dbb = new SolidBrush(dlBg))
            using (Pen dbpPen = new Pen(dlBorder, 1))
            {
                g.FillPath(dbb, dbp);
                g.DrawPath(dbpPen, dbp);
            }
            using (Font dbf = new Font("Segoe UI", 7.5f))
            using (SolidBrush dbt = new SolidBrush(hasItems ? Color.FromArgb(230, 230, 240) : Color.FromArgb(120, 120, 130)))
            using (StringFormat dbsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("📥 Tải", dbf, dbt, dlAllBtn, dbsf);
            }

            // Right sub-header separator line
            using (Pen sepPen = new Pen(Color.FromArgb(20, 255, 255, 255), 1))
            {
                g.DrawLine(sepPen, rightColX, HeaderHeight + 26, w - 8, HeaderHeight + 26);
            }

            // Right column list area (Tracks)
            Rectangle rightListArea = new Rectangle(rightColX, listTop, rightColW, listH);
            g.SetClip(rightListArea);

            var filteredTracks = GetFilteredCollectionTracks(curPlaylist);
            if (filteredTracks.Count == 0)
            {
                using (Font hintF1 = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (Font hintF2 = new Font("Segoe UI", 8f))
                using (SolidBrush hb1 = new SolidBrush(Color.FromArgb(180, 180, 190)))
                using (SolidBrush hb2 = new SolidBrush(Color.FromArgb(130, 130, 140)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Bộ sưu tập này chưa có bài hát nào", hintF1, hb1, new RectangleF(rightColX, listTop + 40, rightColW, 20), sf);
                    g.DrawString("Chuột phải vào bài hát bất kỳ để thêm vào đây", hintF2, hb2, new RectangleF(rightColX + 8, listTop + 62, rightColW - 16, 20), sf);
                }
            }
            else
            {
                using (Font trkNumFont = new Font("Segoe UI", 8f))
                using (Font trkTitleFont = new Font("Segoe UI", 8.5f))
                using (Font trkDurFont = new Font("Segoe UI", 8f))
                using (SolidBrush normalText = new SolidBrush(Color.FromArgb(235, 235, 240)))
                using (SolidBrush activeText = new SolidBrush(Color.White))
                using (SolidBrush dimText = new SolidBrush(Color.FromArgb(140, 140, 150)))
                using (SolidBrush activeIndicatorBrush = new SolidBrush(Color.FromArgb(168, 85, 247)))
                using (StringFormat sfNum = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                {
                    for (int i = 0; i < filteredTracks.Count; i++)
                    {
                        int rowY = listTop + (i * CollectionTrackRowHeight) - _collectionRightScroll;
                        if (rowY + CollectionTrackRowHeight < listTop || rowY > listBottom) continue;

                        Rectangle rowRect = new Rectangle(rightColX + 2, rowY, rightColW - 4, CollectionTrackRowHeight - 2);
                        PlaylistItem item = filteredTracks[i];

                        bool isActive = (AudioPlayer != null && !string.IsNullOrEmpty(AudioPlayer.CurrentVideoId) && string.Equals(item.Id, AudioPlayer.CurrentVideoId, StringComparison.OrdinalIgnoreCase));
                        bool isHover = (i == _hoveredCollectionTrackIndex);

                        if (isActive)
                        {
                            using (GraphicsPath rp = GetRoundedRect(rowRect, 5))
                            using (SolidBrush activeBg = new SolidBrush(Color.FromArgb(45, 168, 85, 247)))
                            using (Pen activeBorder = new Pen(Color.FromArgb(80, 168, 85, 247), 1))
                            {
                                g.FillPath(activeBg, rp);
                                g.DrawPath(activeBorder, rp);
                            }
                        }
                        else if (isHover)
                        {
                            using (GraphicsPath rp = GetRoundedRect(rowRect, 5))
                            using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
                            {
                                g.FillPath(hoverBg, rp);
                            }
                        }

                        // Index or Play indicator
                        Rectangle numRect = new Rectangle(rowRect.X + 2, rowRect.Y + 7, 20, 18);
                        if (isActive)
                        {
                            Point[] playArrow = new Point[]
                            {
                                new Point(numRect.Right - 8, rowRect.Y + 11),
                                new Point(numRect.Right, rowRect.Y + 16),
                                new Point(numRect.Right - 8, rowRect.Y + 21)
                            };
                            g.FillPolygon(activeIndicatorBrush, playArrow);
                        }
                        else
                        {
                            g.DrawString((i + 1).ToString(), trkNumFont, dimText, numRect, sfNum);
                        }

                        // Micro Thumbnail
                        int thumbSize = 22;
                        int thumbX = numRect.Right + 5;
                        int thumbY = rowRect.Y + 5;
                        Rectangle thumbRect = new Rectangle(thumbX, thumbY, thumbSize, thumbSize);

                        using (GraphicsPath tp = GetRoundedRect(thumbRect, 3))
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
                                using (Font noteFont = new Font("Segoe UI", 7f))
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
                        }

                        // Track Title
                        int titleX = thumbRect.Right + 7;
                        int titleW = Math.Max(40, rowRect.Right - 44 - titleX - 2);
                        RectangleF titleRect = new RectangleF(titleX, rowRect.Y + 7, titleW, 18);

                        using (StringFormat sfTitle = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
                        {
                            string displayTitle = (!string.IsNullOrWhiteSpace(item.Title) && item.Title.Trim() != "None" && item.Title.Trim() != "NA")
                                ? Services.AudioPlayerService.CleanDisplayTitle(item.Title)
                                : string.Format("Bài {0}", i + 1);
                            g.DrawString(displayTitle, trkTitleFont, isActive ? activeText : normalText, titleRect, sfTitle);
                        }

                        // Track Duration
                        RectangleF durRect = new RectangleF(rowRect.Right - 42, rowRect.Y + 7, 38, 18);
                        using (StringFormat sfDur = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                        {
                            g.DrawString(item.FormattedDuration, trkDurFont, dimText, durRect, sfDur);
                        }
                    }
                }
            }
            g.ResetClip();
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
