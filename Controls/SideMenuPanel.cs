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
        private const int TabsH = 40;
        private const int SearchBoxH = 28;
        private const int RowH = 56;
        private const int PlaylistRowH = 56;
        private const int ScrollBarW = 6;

        private SideTab _activeTab = SideTab.Queue;
        private List<PlaylistItem> _rawItems = new List<PlaylistItem>();
        private List<PlaylistItem> _filteredItems = new List<PlaylistItem>();
        private PlaylistSearchBox _searchBox;
        private PlaylistSearchBox _searchCollectionsBox;
        private PlaylistSearchBox _searchCollectionTracksBox;
        private string _searchQuery = string.Empty;
        private string _searchCollectionsQuery = string.Empty;
        private string _searchCollectionTracksQuery = string.Empty;

        // Collection (2-column layout) state
        private List<UserPlaylist> _collectionPlaylists = new List<UserPlaylist>();
        private List<PlaylistItem> _filteredCollectionTracks = new List<PlaylistItem>();
        private int _selectedPlaylistIndex = 0;
        private int _hoveredPlaylistIndex = -1;
        private int _hoveredTrackRow = -1;
        private int _colLeftScroll = 0;
        private int _colRightScroll = 0;
        private bool _isHoveringNewBtn = false;
        private bool _isHoveringPlayAllBtn = false;
        private bool _isDraggingColLeftScroll = false;
        private bool _isDraggingColRightScroll = false;
        private int _colLeftDragGrabOffsetY = 0;
        private int _colRightDragGrabOffsetY = 0;
        private bool _isHoveringColLeftScroll = false;
        private bool _isHoveringColRightScroll = false;

        // Standard 1-column state (Queue, History, Saved)
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
                                try { BeginInvoke(new Action(RefreshData)); } catch { }
                            }
                            else
                            {
                                try { BeginInvoke(new Action(Invalidate)); } catch { }
                            }
                        };
                        _audioPlayer.TrackLoaded += (s, e) =>
                        {
                            try { BeginInvoke(new Action(Invalidate)); } catch { }
                        };
                        _audioPlayer.PlaybackStateChanged += (s, e) =>
                        {
                            try { BeginInvoke(new Action(Invalidate)); } catch { }
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
                    _colLeftScroll = 0;
                    _colRightScroll = 0;
                    _searchBox?.Clear();
                    _searchCollectionsBox?.Clear();
                    _searchCollectionTracksBox?.Clear();
                    UpdateSearchBoxesLayout();
                    RefreshData();
                }
            }
        }

        public SideMenuPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;

            _searchBox = new PlaylistSearchBox("Tìm kiếm bài hát trong danh sách...");
            _searchBox.SearchTextChanged += (s, text) =>
            {
                _searchQuery = text?.Trim() ?? string.Empty;
                _scrollOffset = 0;
                _colRightScroll = 0;
                ApplyFilter();
                Invalidate();
            };
            Controls.Add(_searchBox);

            _searchCollectionsBox = new PlaylistSearchBox("Tìm danh sách...");
            _searchCollectionsBox.SearchTextChanged += (s, text) =>
            {
                _searchCollectionsQuery = text?.Trim() ?? string.Empty;
                _colLeftScroll = 0;
                ClampCollectionScroll();
                Invalidate();
            };
            _searchCollectionsBox.Visible = false;
            Controls.Add(_searchCollectionsBox);

            _searchCollectionTracksBox = new PlaylistSearchBox("Tìm bài hát...");
            _searchCollectionTracksBox.SearchTextChanged += (s, text) =>
            {
                _searchCollectionTracksQuery = text?.Trim() ?? string.Empty;
                _colRightScroll = 0;
                RefreshCollectionTracks();
                ClampCollectionScroll();
                Invalidate();
            };
            _searchCollectionTracksBox.Visible = false;
            Controls.Add(_searchCollectionTracksBox);

            UpdateSearchBoxesLayout();

            // Listen to real-time playlist and collection changes
            PlaylistCollectionService.Instance.PlaylistsChanged += (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try { BeginInvoke(new Action(RefreshData)); } catch { }
                }
            };
            FavoritesService.FavoritesChanged += (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try { BeginInvoke(new Action(RefreshData)); } catch { }
                }
            };
            HistoryService.HistoryChanged += (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed && _activeTab == SideTab.History)
                {
                    try { BeginInvoke(new Action(RefreshData)); } catch { }
                }
            };
            SavedTracksService.Instance.SavedTracksChanged += (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed && _activeTab == SideTab.Saved)
                {
                    try { BeginInvoke(new Action(RefreshData)); } catch { }
                }
            };

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

        public bool IsSearchFocused => (_searchBox != null && _searchBox.ContainsFocus) ||
                                       (_searchCollectionsBox != null && _searchCollectionsBox.ContainsFocus) ||
                                       (_searchCollectionTracksBox != null && _searchCollectionTracksBox.ContainsFocus);

        public void ClearSearchFocus()
        {
            _searchBox?.RemoveFocus();
            _searchCollectionsBox?.RemoveFocus();
            _searchCollectionTracksBox?.RemoveFocus();
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
                        // Strictly show ONLY tracks imported via link or extension!
                        var qList = _audioPlayer?.SessionPlaylist;
                        if (qList != null && qList.Count > 0)
                        {
                            _rawItems = new List<PlaylistItem>(qList);
                        }
                        else
                        {
                            _rawItems = new List<PlaylistItem>();
                        }
                        break;

                    case SideTab.Favorites: // Tab 1: "Bộ sưu tập"
                        _collectionPlaylists.Clear();
                        // 1. Built-in "Yêu thích"
                        var favList = FavoritesService.GetFavorites() ?? new List<PlaylistItem>();
                        _collectionPlaylists.Add(new UserPlaylist("Yêu thích", favList) { Id = "__FAVORITES__" });
                        // 2. Custom user-created playlists
                        var userPlaylists = PlaylistCollectionService.Instance.GetPlaylists();
                        if (userPlaylists != null)
                        {
                            foreach (var p in userPlaylists)
                            {
                                _collectionPlaylists.Add(p);
                            }
                        }
                        if (_selectedPlaylistIndex >= _collectionPlaylists.Count)
                        {
                            _selectedPlaylistIndex = Math.Max(0, _collectionPlaylists.Count - 1);
                        }
                        RefreshCollectionTracks();
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
            ClampCollectionScroll();
            Invalidate();
        }

        private List<UserPlaylist> FilteredCollectionPlaylists
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_searchCollectionsQuery)) return _collectionPlaylists;
                string clean = RemoveDiacritics(_searchCollectionsQuery);
                return _collectionPlaylists.FindAll(x =>
                    x.Name.IndexOf(_searchCollectionsQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    RemoveDiacritics(x.Name).IndexOf(clean, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        private void RefreshCollectionTracks()
        {
            UserPlaylist curP = GetSelectedCollectionPlaylist();
            if (curP == null || curP.Items == null)
            {
                _filteredCollectionTracks = new List<PlaylistItem>();
                return;
            }

            if (string.IsNullOrWhiteSpace(_searchCollectionTracksQuery))
            {
                _filteredCollectionTracks = new List<PlaylistItem>(curP.Items);
                return;
            }

            string cleanQuery = RemoveDiacritics(_searchCollectionTracksQuery);
            _filteredCollectionTracks = curP.Items.FindAll(x =>
            {
                if (string.IsNullOrEmpty(x.Title)) return false;
                if (x.Title.IndexOf(_searchCollectionTracksQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (RemoveDiacritics(x.Title).IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (!string.IsNullOrEmpty(x.Author))
                {
                    if (x.Author.IndexOf(_searchCollectionTracksQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (RemoveDiacritics(x.Author).IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
                return false;
            });
        }

        private UserPlaylist GetSelectedCollectionPlaylist()
        {
            if (_collectionPlaylists != null && _selectedPlaylistIndex >= 0 && _selectedPlaylistIndex < _collectionPlaylists.Count)
            {
                return _collectionPlaylists[_selectedPlaylistIndex];
            }
            return null;
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
            if (_activeTab == SideTab.Favorites)
            {
                RefreshCollectionTracks();
                return;
            }

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

        // Collection column layout metrics
        private const int ColHeaderH = 36;
        private int LeftColWidth => Math.Max(150, Math.Min(220, (int)((Width - 28) * 0.38f)));
        private int LeftListTop => ListTop + ColHeaderH;
        private int LeftListHeight => Math.Max(10, Height - LeftListTop - 8);
        private int RightListTop => ListTop + ColHeaderH;
        private int RightListHeight => Math.Max(10, Height - RightListTop - 8);

        private Rectangle GetNewPlaylistBtnRect()
        {
            return new Rectangle(10 + LeftColWidth - 46, ListTop + 4, 42, 22);
        }

        private Rectangle GetPlayAllBtnRect()
        {
            int divX = 10 + LeftColWidth + 4;
            int rightColX = divX + 6;
            int rightColW = Width - rightColX - 10;
            return new Rectangle(rightColX + rightColW - 78, ListTop + 4, 74, 22);
        }

        private void ClampScroll()
        {
            int maxScroll = Math.Max(0, TotalListContentHeight - ListHeight);
            _scrollOffset = Math.Max(0, Math.Min(maxScroll, _scrollOffset));
        }

        private void ClampCollectionScroll()
        {
            int leftListH = LeftListHeight;
            int leftTotalH = FilteredCollectionPlaylists.Count * PlaylistRowH;
            int maxLeftScroll = Math.Max(0, leftTotalH - leftListH);
            _colLeftScroll = Math.Max(0, Math.Min(_colLeftScroll, maxLeftScroll));

            int rightListH = RightListHeight;
            int rightTotalH = _filteredCollectionTracks.Count * RowH;
            int maxRightScroll = Math.Max(0, rightTotalH - rightListH);
            _colRightScroll = Math.Max(0, Math.Min(_colRightScroll, maxRightScroll));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            _closeBtnRect = Rectangle.Empty;

            int tabY = 10;
            int tabMargin = 8;
            int availTabW = Width - (tabMargin * 2);
            int tabSpacing = 4;
            int tabW = Math.Max(20, (availTabW - 3 * tabSpacing) / 4);
            for (int i = 0; i < 4; i++)
            {
                _tabRects[i] = new Rectangle(tabMargin + i * (tabW + tabSpacing), tabY, tabW, TabsH);
            }

            UpdateSearchBoxesLayout();

            ClampScroll();
            ClampCollectionScroll();
            Invalidate();
        }

        private void UpdateSearchBoxesLayout()
        {
            int tabY = 10;
            int searchY = tabY + TabsH + 8;
            bool isCol = (_activeTab == SideTab.Favorites);

            if (_searchBox != null)
            {
                _searchBox.Visible = !isCol;
                _searchBox.Location = new Point(10, searchY);
                _searchBox.Size = new Size(Math.Max(100, Width - 20), SearchBoxH);
            }

            int leftColW = LeftColWidth;
            int divX = 10 + leftColW + 4;
            int rightColX = divX + 6;
            int rightColW = Math.Max(100, Width - rightColX - 10);

            if (_searchCollectionsBox != null)
            {
                _searchCollectionsBox.Visible = isCol;
                _searchCollectionsBox.Location = new Point(10, searchY);
                _searchCollectionsBox.Size = new Size(leftColW, SearchBoxH);
            }

            if (_searchCollectionTracksBox != null)
            {
                _searchCollectionTracksBox.Visible = isCol;
                _searchCollectionTracksBox.Location = new Point(rightColX, searchY);
                _searchCollectionTracksBox.Size = new Size(rightColW, SearchBoxH);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 1. Handling dragging for 1-column scroll
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

            // 2. Handling dragging for Collection Left Scroll
            if (_isDraggingColLeftScroll)
            {
                int listTop = LeftListTop;
                int listH = LeftListHeight;
                int trackY = listTop + 4;
                int trackH = listH - 8;
                int totalH = FilteredCollectionPlaylists.Count * PlaylistRowH;
                float maxScroll = Math.Max(1, totalH - listH);
                float ratio = (float)listH / totalH;
                int thumbH = Math.Max(24, (int)(trackH * ratio));
                float availableTrackH = Math.Max(1, trackH - thumbH);

                float newThumbY = e.Y - _colLeftDragGrabOffsetY - trackY;
                float scrollRatio = Math.Max(0f, Math.Min(1f, newThumbY / availableTrackH));
                _colLeftScroll = (int)Math.Round(scrollRatio * maxScroll);
                ClampCollectionScroll();
                Invalidate();
                return;
            }

            // 3. Handling dragging for Collection Right Scroll
            if (_isDraggingColRightScroll)
            {
                int listTop = RightListTop;
                int listH = RightListHeight;
                int trackY = listTop + 4;
                int trackH = listH - 8;
                int totalH = _filteredCollectionTracks.Count * RowH;
                float maxScroll = Math.Max(1, totalH - listH);
                float ratio = (float)listH / totalH;
                int thumbH = Math.Max(24, (int)(trackH * ratio));
                float availableTrackH = Math.Max(1, trackH - thumbH);

                float newThumbY = e.Y - _colRightDragGrabOffsetY - trackY;
                float scrollRatio = Math.Max(0f, Math.Min(1f, newThumbY / availableTrackH));
                _colRightScroll = (int)Math.Round(scrollRatio * maxScroll);
                ClampCollectionScroll();
                Invalidate();
                return;
            }

            bool needRepaint = false;

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

            if (_activeTab == SideTab.Favorites)
            {
                // Collection 2-column mode hover detection
                bool prevNewBtn = _isHoveringNewBtn;
                _isHoveringNewBtn = GetNewPlaylistBtnRect().Contains(e.Location);
                if (_isHoveringNewBtn != prevNewBtn) needRepaint = true;

                bool prevPlayAll = _isHoveringPlayAllBtn;
                _isHoveringPlayAllBtn = GetPlayAllBtnRect().Contains(e.Location);
                if (_isHoveringPlayAllBtn != prevPlayAll) needRepaint = true;

                int leftColX = 10;
                int leftColW = LeftColWidth;
                int divX = leftColX + leftColW + 4;
                int rightColX = divX + 6;
                int rightColW = Width - rightColX - 10;

                // Left column playlist hover
                int prevPlHov = _hoveredPlaylistIndex;
                _hoveredPlaylistIndex = -1;
                if (e.X >= leftColX && e.X <= leftColX + leftColW && e.Y >= LeftListTop && e.Y <= LeftListTop + LeftListHeight)
                {
                    var filtered = FilteredCollectionPlaylists;
                    int relY = e.Y - LeftListTop + _colLeftScroll;
                    int pIdx = relY / PlaylistRowH;
                    if (pIdx >= 0 && pIdx < filtered.Count)
                    {
                        _hoveredPlaylistIndex = pIdx;
                    }
                }
                if (_hoveredPlaylistIndex != prevPlHov) needRepaint = true;

                // Right column track hover
                int prevTrackHov = _hoveredTrackRow;
                _hoveredTrackRow = -1;
                if (e.X >= rightColX && e.X <= rightColX + rightColW && e.Y >= RightListTop && e.Y <= RightListTop + RightListHeight)
                {
                    int relY = e.Y - RightListTop + _colRightScroll;
                    int tIdx = relY / RowH;
                    if (tIdx >= 0 && tIdx < _filteredCollectionTracks.Count)
                    {
                        _hoveredTrackRow = tIdx;
                    }
                }
                if (_hoveredTrackRow != prevTrackHov) needRepaint = true;

                // Scrollbar hover detection
                bool overLeftScroll = (FilteredCollectionPlaylists.Count * PlaylistRowH > LeftListHeight &&
                                       e.X >= leftColX + leftColW - 12 && e.X <= leftColX + leftColW &&
                                       e.Y >= LeftListTop && e.Y <= LeftListTop + LeftListHeight);
                if (overLeftScroll != _isHoveringColLeftScroll)
                {
                    _isHoveringColLeftScroll = overLeftScroll;
                    needRepaint = true;
                }

                bool overRightScroll = (_filteredCollectionTracks.Count * RowH > RightListHeight &&
                                        e.X >= Width - ScrollBarW - 14 && e.X <= Width &&
                                        e.Y >= RightListTop && e.Y <= RightListTop + RightListHeight);
                if (overRightScroll != _isHoveringColRightScroll)
                {
                    _isHoveringColRightScroll = overRightScroll;
                    needRepaint = true;
                }

                Cursor = (_hoveredTab >= 0 || _isHoveringNewBtn || _isHoveringPlayAllBtn || _hoveredPlaylistIndex >= 0 || _hoveredTrackRow >= 0 || _isHoveringColLeftScroll || _isHoveringColRightScroll)
                    ? Cursors.Hand : Cursors.Default;
            }
            else
            {
                // Standard 1-column scrollbar hover
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

                Cursor = (_hoveredTab >= 0 || _hoveredRow >= 0 || _isHoveringScroll || _isDraggingScroll) ? Cursors.Hand : Cursors.Default;
            }

            if (needRepaint) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_isDraggingScroll && !_isDraggingColLeftScroll && !_isDraggingColRightScroll)
            {
                _isHoveringClose = false;
                _hoveredTab = -1;
                _hoveredRow = -1;
                _hoveredPlaylistIndex = -1;
                _hoveredTrackRow = -1;
                _isHoveringNewBtn = false;
                _isHoveringPlayAllBtn = false;
                _isHoveringScroll = false;
                _isHoveringColLeftScroll = false;
                _isHoveringColRightScroll = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            // Context Menu on Right Click
            if (e.Button == MouseButtons.Right)
            {
                if (_activeTab == SideTab.Favorites)
                {
                    int leftColX = 10;
                    int leftColW = LeftColWidth;
                    int divX = leftColX + leftColW + 4;
                    int rightColX = divX + 6;
                    int rightColW = Width - rightColX - 10;

                    // Right click on playlist in left column
                    if (e.X >= leftColX && e.X <= leftColX + leftColW && e.Y >= LeftListTop && e.Y <= LeftListTop + LeftListHeight)
                    {
                        var filtered = FilteredCollectionPlaylists;
                        int relY = e.Y - LeftListTop + _colLeftScroll;
                        int pIdx = relY / PlaylistRowH;
                        if (pIdx >= 0 && pIdx < filtered.Count)
                        {
                            int realIdx = _collectionPlaylists.FindIndex(p => p.Id == filtered[pIdx].Id);
                            if (realIdx >= 0) _selectedPlaylistIndex = realIdx;
                            RefreshCollectionTracks();
                            Invalidate();
                            ShowPlaylistContextMenu(filtered[pIdx], PointToScreen(e.Location));
                            return;
                        }
                    }

                    // Right click on track in right column
                    if (e.X >= rightColX && e.X <= rightColX + rightColW && e.Y >= RightListTop && e.Y <= RightListTop + RightListHeight)
                    {
                        int relY = e.Y - RightListTop + _colRightScroll;
                        int tIdx = relY / RowH;
                        if (tIdx >= 0 && tIdx < _filteredCollectionTracks.Count)
                        {
                            UserPlaylist curP = GetSelectedCollectionPlaylist();
                            if (curP != null)
                            {
                                ShowTrackContextMenu(curP, _filteredCollectionTracks[tIdx], PointToScreen(e.Location));
                                return;
                            }
                        }
                    }
                }
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            // Tab click
            for (int i = 0; i < 4; i++)
            {
                if (_tabRects[i].Contains(e.Location))
                {
                    ActiveTab = (SideTab)i;
                    return;
                }
            }

            // Collection 2-Column Interactions
            if (_activeTab == SideTab.Favorites)
            {
                // "+ Mới" button clicked
                if (GetNewPlaylistBtnRect().Contains(e.Location))
                {
                    PromptCreateNewPlaylist();
                    return;
                }

                // "[▶ Phát tất cả]" button clicked
                if (GetPlayAllBtnRect().Contains(e.Location))
                {
                    PlayAllInSelectedCollection();
                    return;
                }

                int leftColX = 10;
                int leftColW = LeftColWidth;
                int divX = leftColX + leftColW + 4;
                int rightColX = divX + 6;
                int rightColW = Width - rightColX - 10;

                // Left column scrollbar click
                int leftTotalH = FilteredCollectionPlaylists.Count * PlaylistRowH;
                if (leftTotalH > LeftListHeight && e.X >= leftColX + leftColW - 12 && e.X <= leftColX + leftColW && e.Y >= LeftListTop && e.Y <= LeftListTop + LeftListHeight)
                {
                    _isDraggingColLeftScroll = true;
                    _colLeftDragGrabOffsetY = 12;
                    Capture = true;
                    return;
                }

                // Left column playlist item clicked
                if (e.X >= leftColX && e.X <= leftColX + leftColW && e.Y >= LeftListTop && e.Y <= LeftListTop + LeftListHeight)
                {
                    var filtered = FilteredCollectionPlaylists;
                    int relY = e.Y - LeftListTop + _colLeftScroll;
                    int pIdx = relY / PlaylistRowH;
                    if (pIdx >= 0 && pIdx < filtered.Count)
                    {
                        int realIdx = _collectionPlaylists.FindIndex(p => p.Id == filtered[pIdx].Id);
                        if (realIdx >= 0) _selectedPlaylistIndex = realIdx;
                        _colRightScroll = 0;
                        RefreshCollectionTracks();
                        Invalidate();
                    }
                    return;
                }

                // Right column scrollbar click
                int rightTotalH = _filteredCollectionTracks.Count * RowH;
                if (rightTotalH > RightListHeight && e.X >= Width - ScrollBarW - 14 && e.X <= Width && e.Y >= RightListTop && e.Y <= RightListTop + RightListHeight)
                {
                    _isDraggingColRightScroll = true;
                    _colRightDragGrabOffsetY = 12;
                    Capture = true;
                    return;
                }

                // Right column track item clicked
                if (e.X >= rightColX && e.X <= rightColX + rightColW && e.Y >= RightListTop && e.Y <= RightListTop + RightListHeight)
                {
                    int relY = e.Y - RightListTop + _colRightScroll;
                    int tIdx = relY / RowH;
                    if (tIdx >= 0 && tIdx < _filteredCollectionTracks.Count)
                    {
                        PlayCollectionTrack(tIdx);
                        return;
                    }
                }
                return;
            }

            // Standard 1-Column Tab Interactions (Queue, History, Saved)
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

            // Standard 1-column row click
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
                            int sIdx = _audioPlayer.SessionPlaylist?.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase)) ?? -1;
                            if (sIdx >= 0)
                            {
                                _audioPlayer.RestoreSessionPlaylist(sIdx);
                            }
                            else
                            {
                                TrackSelected?.Invoke(this, item);
                            }
                        }
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

        private void PlayAllInSelectedCollection()
        {
            UserPlaylist curP = GetSelectedCollectionPlaylist();
            if (curP == null || curP.Items == null || curP.Items.Count == 0) return;
            if (curP.Id == "__FAVORITES__")
            {
                _audioPlayer?.PlayFavorites(curP.Items, 0);
            }
            else
            {
                _audioPlayer?.PlayCollection(curP.Items, 0, curP.Name, curP.Id);
            }
            Invalidate();
        }

        private void PlayCollectionTrack(int trackIdx)
        {
            UserPlaylist curP = GetSelectedCollectionPlaylist();
            if (curP == null || curP.Items == null || trackIdx >= _filteredCollectionTracks.Count) return;
            PlaylistItem item = _filteredCollectionTracks[trackIdx];
            int realIdx = curP.Items.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            if (realIdx < 0) realIdx = trackIdx;

            if (curP.Id == "__FAVORITES__")
            {
                _audioPlayer?.PlayFavorites(curP.Items, realIdx);
            }
            else
            {
                _audioPlayer?.PlayCollection(curP.Items, realIdx, curP.Name, curP.Id);
            }
            Invalidate();
        }

        private void PromptCreateNewPlaylist()
        {
            string name = InputDialog.Show(this.FindForm(), "Tạo bộ sưu tập mới", "Nhập tên bộ sưu tập:");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var created = PlaylistCollectionService.Instance.CreatePlaylist(name.Trim());
                RefreshData();
                if (created != null)
                {
                    int idx = _collectionPlaylists.FindIndex(p => p.Id == created.Id);
                    if (idx >= 0)
                    {
                        _selectedPlaylistIndex = idx;
                        RefreshCollectionTracks();
                        Invalidate();
                    }
                }
            }
        }

        private void ShowPlaylistContextMenu(UserPlaylist p, Point screenPt)
        {
            ContextMenuStrip cms = new ContextMenuStrip();
            cms.Items.Add("▶ Phát bộ sưu tập này", null, (s, e) =>
            {
                if (p.Items != null && p.Items.Count > 0)
                {
                    if (p.Id == "__FAVORITES__") _audioPlayer?.PlayFavorites(p.Items, 0);
                    else _audioPlayer?.PlayCollection(p.Items, 0, p.Name, p.Id);
                }
            });

            if (p.Id != "__FAVORITES__")
            {
                cms.Items.Add(new ToolStripSeparator());
                cms.Items.Add("✎ Đổi tên...", null, (s, e) =>
                {
                    string newName = InputDialog.Show(this.FindForm(), "Đổi tên bộ sưu tập", "Nhập tên mới:", p.Name);
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        PlaylistCollectionService.Instance.RenamePlaylist(p.Id, newName.Trim());
                        RefreshData();
                    }
                });
                cms.Items.Add("🗑 Xóa bộ sưu tập", null, (s, e) =>
                {
                    if (CyberConfirmDialog.ShowConfirm("Xóa bộ sưu tập", string.Format("Bạn có chắc chắn muốn xóa bộ sưu tập '{0}'?", p.Name), "Xóa", "Hủy", this.FindForm()))
                    {
                        PlaylistCollectionService.Instance.DeletePlaylist(p.Id);
                        RefreshData();
                    }
                });
            }
            cms.Show(screenPt);
        }

        private void ShowTrackContextMenu(UserPlaylist p, PlaylistItem track, Point screenPt)
        {
            ContextMenuStrip cms = new ContextMenuStrip();
            cms.Items.Add("▶ Phát bài này", null, (s, e) =>
            {
                int idx = p.Items.IndexOf(track);
                if (p.Id == "__FAVORITES__") _audioPlayer?.PlayFavorites(p.Items, idx >= 0 ? idx : 0);
                else _audioPlayer?.PlayCollection(p.Items, idx >= 0 ? idx : 0, p.Name, p.Id);
            });
            cms.Items.Add("📋 Sao chép link bài hát", null, (s, e) =>
            {
                if (!string.IsNullOrEmpty(track.Url))
                {
                    try { Clipboard.SetText(track.Url); } catch { }
                }
            });
            cms.Items.Add(new ToolStripSeparator());
            cms.Items.Add("🗑 Xóa khỏi bộ sưu tập này", null, (s, e) =>
            {
                if (p.Id == "__FAVORITES__")
                {
                    FavoritesService.RemoveFavorite(track.Id);
                }
                else
                {
                    PlaylistCollectionService.Instance.RemoveTrackFromPlaylist(p.Id, track.Id);
                }
                RefreshData();
            });
            cms.Show(screenPt);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDraggingScroll || _isDraggingColLeftScroll || _isDraggingColRightScroll)
            {
                _isDraggingScroll = false;
                _isDraggingColLeftScroll = false;
                _isDraggingColRightScroll = false;
                Capture = false;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (_activeTab == SideTab.Favorites)
            {
                int leftColX = 10;
                int leftColW = LeftColWidth;
                int divX = leftColX + leftColW + 4;

                if (e.X < divX)
                {
                    _colLeftScroll -= (e.Delta / 120) * 36;
                }
                else
                {
                    _colRightScroll -= (e.Delta / 120) * 36;
                }
                ClampCollectionScroll();
                Invalidate();
                return;
            }

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

            // 1. Panel Background (Glassmorphism dark translucent card, opacity < 100%)
            Rectangle cardRect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(cardRect, 14))
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(150, 22, 10, 42)))
                {
                    g.FillPath(bgBrush, path);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(140, 180, 50, 220), 1.2f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // 2. Tabs (Pill Buttons directly at top)
            string[] tabLabels = { "Danh sách phát", "Bộ sưu tập", "Lịch sử", "Đã tải" };
            for (int i = 0; i < 4; i++)
            {
                Rectangle tr = _tabRects[i];
                bool isCurTab = ((int)_activeTab == i);
                bool isHover = (_hoveredTab == i);

                Color bgColor = isCurTab ? Color.FromArgb(220, 75, 22, 105) : (isHover ? Color.FromArgb(180, 45, 28, 76) : Color.FromArgb(140, 28, 14, 52));
                Color borderColor = isCurTab ? Color.FromArgb(255, 45, 135) : (isHover ? Color.FromArgb(160, 180, 80, 220) : Color.FromArgb(80, 140, 50, 190));
                Color textColor = isCurTab ? Color.White : (isHover ? Color.FromArgb(245, 240, 255) : Color.FromArgb(190, 180, 210));
                FontStyle fs = isCurTab ? FontStyle.Bold : FontStyle.Regular;

                using (GraphicsPath tp = RoundedRect(tr, 10))
                {
                    using (SolidBrush tbBrush = new SolidBrush(bgColor))
                    {
                        g.FillPath(tbBrush, tp);
                    }
                    using (Pen tbPen = new Pen(borderColor, isCurTab ? 1.4f : 1f))
                    {
                        g.DrawPath(tbPen, tp);
                    }
                }

                using (Font tabFont = new Font("Segoe UI", 9.0f, fs))
                using (SolidBrush tabBrush = new SolidBrush(textColor))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(tabLabels[i], tabFont, tabBrush, tr, sf);
                }
            }

            // 3. Render Session Area
            if (_activeTab == SideTab.Favorites)
            {
                DrawCollectionSession(g);
            }
            else
            {
                DrawStandardSession(g);
            }
        }

        private void DrawCollectionSession(Graphics g)
        {
            int listTop = ListTop;
            int listH = ListHeight;
            int leftColX = 10;
            int leftColW = LeftColWidth;
            int divX = leftColX + leftColW + 4;
            int rightColX = divX + 6;
            int rightColW = Width - rightColX - 10;

            // ===== LEFT COLUMN HEADER =====
            using (Font subHeaderFont = new Font("Segoe UI", 8.25f, FontStyle.Bold))
            using (SolidBrush subHeaderBrush = new SolidBrush(Color.FromArgb(220, 180, 255)))
            {
                g.DrawString("DANH SÁCH LƯU", subHeaderFont, subHeaderBrush, leftColX + 2, listTop + 4);
            }

            // Button "+ Mới"
            Rectangle newBtnRect = GetNewPlaylistBtnRect();
            Color newBtnBg = _isHoveringNewBtn ? Color.FromArgb(70, 235, 0, 130) : Color.FromArgb(35, 235, 0, 130);
            Color newBtnBorder = _isHoveringNewBtn ? Color.FromArgb(255, 120, 190) : Color.FromArgb(200, 45, 135);
            using (GraphicsPath nbp = RoundedRect(newBtnRect, 5))
            using (SolidBrush nbb = new SolidBrush(newBtnBg))
            using (Pen nbpPen = new Pen(newBtnBorder, 1f))
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

            // ===== RIGHT COLUMN HEADER =====
            UserPlaylist curP = GetSelectedCollectionPlaylist();
            string selTitle = curP != null ? curP.Name : "Bộ sưu tập";
            using (Font rightHeaderFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            using (SolidBrush rightHeaderBrush = new SolidBrush(Color.White))
            using (StringFormat sfNear = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                RectangleF titleBounds = new RectangleF(rightColX, listTop + 4, rightColW - 84, 20);
                g.DrawString(selTitle, rightHeaderFont, rightHeaderBrush, titleBounds, sfNear);
            }

            // Button "[▶ Phát tất cả]"
            Rectangle playAllRect = GetPlayAllBtnRect();
            Color playAllBg = _isHoveringPlayAllBtn ? Color.FromArgb(70, 235, 0, 130) : Color.FromArgb(35, 235, 0, 130);
            Color playAllBorder = _isHoveringPlayAllBtn ? Color.FromArgb(255, 120, 190) : Color.FromArgb(200, 45, 135);
            using (GraphicsPath pap = RoundedRect(playAllRect, 5))
            using (SolidBrush pab = new SolidBrush(playAllBg))
            using (Pen papPen = new Pen(playAllBorder, 1f))
            {
                g.FillPath(pab, pap);
                g.DrawPath(papPen, pap);
            }
            using (Font paf = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (SolidBrush pat = new SolidBrush(Color.White))
            using (StringFormat pasf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("▶ Phát tất cả", paf, pat, playAllRect, pasf);
            }

            // Header Separators (spaced comfortably below buttons)
            using (Pen sepPen = new Pen(Color.FromArgb(25, 255, 255, 255), 1f))
            {
                int sepY = listTop + 32;
                g.DrawLine(sepPen, leftColX, sepY, leftColX + leftColW, sepY);
                g.DrawLine(sepPen, rightColX, sepY, rightColX + rightColW, sepY);
            }

            // Vertical divider between Left and Right columns
            using (Pen divPen = new Pen(Color.FromArgb(35, 200, 100, 255), 1.2f))
            {
                g.DrawLine(divPen, divX, listTop + 2, divX, listTop + listH - 2);
            }

            // ===== LEFT COLUMN LIST (Saved Playlists) =====
            int leftListTop = LeftListTop;
            int leftListH = LeftListHeight;
            Rectangle leftClip = new Rectangle(leftColX, leftListTop, leftColW, leftListH);
            Region oldClip = g.Clip;
            g.SetClip(leftClip);

            var filteredPlaylists = FilteredCollectionPlaylists;
            int totalLeftH = filteredPlaylists.Count * PlaylistRowH;
            bool hasLeftScroll = totalLeftH > leftListH && leftListH > 0;
            int itemW = hasLeftScroll ? (leftColW - ScrollBarW - 6) : (leftColW - 6);

            using (Font pTitleFont = new Font("Segoe UI", 10.0f, FontStyle.Bold))
            using (Font pSubFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (StringFormat sfPl = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                for (int i = 0; i < filteredPlaylists.Count; i++)
                {
                    int rowY = leftListTop + (i * PlaylistRowH) - _colLeftScroll;
                    if (rowY + PlaylistRowH < leftListTop || rowY > leftListTop + leftListH) continue;

                    UserPlaylist pl = filteredPlaylists[i];
                    bool isSel = (_selectedPlaylistIndex >= 0 && _selectedPlaylistIndex < _collectionPlaylists.Count && _collectionPlaylists[_selectedPlaylistIndex].Id == pl.Id);
                    bool isHov = (i == _hoveredPlaylistIndex);

                    Rectangle rowRect = new Rectangle(leftColX + 3, rowY + 1, itemW, PlaylistRowH - 2);

                    if (isSel)
                    {
                        using (GraphicsPath rp = RoundedRect(rowRect, 6))
                        using (SolidBrush selBg = new SolidBrush(Color.FromArgb(60, 235, 0, 130)))
                        using (Pen selPen = new Pen(Color.FromArgb(180, 255, 45, 135), 1.2f))
                        {
                            g.FillPath(selBg, rp);
                            g.DrawPath(selPen, rp);
                        }
                        // Left active indicator pill
                        using (SolidBrush pill = new SolidBrush(Color.FromArgb(255, 45, 135)))
                        {
                            g.FillRectangle(pill, rowRect.X + 2, rowRect.Y + 8, 3, rowRect.Height - 16);
                        }
                    }
                    else if (isHov)
                    {
                        using (GraphicsPath rp = RoundedRect(rowRect, 6))
                        using (SolidBrush hovBg = new SolidBrush(Color.FromArgb(25, 255, 255, 255)))
                        {
                            g.FillPath(hovBg, rp);
                        }
                    }

                    int textX = rowRect.X + (isSel ? 10 : 8);
                    int textW = rowRect.Width - (isSel ? 14 : 12);

                    // Icon / prefix
                    bool isFav = (pl.Id == "__FAVORITES__");
                    string iconPrefix = isFav ? "♥ " : "♫ ";
                    Color titleColor = isSel ? Color.White : (isHov ? Color.FromArgb(250, 245, 255) : Color.FromArgb(220, 215, 230));
                    Color subColor = isSel ? Color.FromArgb(255, 130, 190) : Color.FromArgb(150, 140, 165);

                    using (SolidBrush tb = new SolidBrush(titleColor))
                    {
                        g.DrawString(iconPrefix + pl.Name, pTitleFont, tb, new RectangleF(textX, rowRect.Y + 6, textW, 20), sfPl);
                    }
                    using (SolidBrush sb = new SolidBrush(subColor))
                    {
                        string cntText = string.Format("{0} bài hát", pl.Items?.Count ?? 0);
                        g.DrawString(cntText, pSubFont, sb, new RectangleF(textX + 2, rowRect.Y + 28, textW - 2, 18), sfPl);
                    }
                }
            }
            g.Clip = oldClip;

            // Left scrollbar
            if (hasLeftScroll)
            {
                int trackX = leftColX + leftColW - ScrollBarW - 1;
                int trackY = leftListTop + 4;
                int trackH = leftListH - 8;
                float ratio = (float)leftListH / totalLeftH;
                int thumbH = Math.Max(20, (int)(trackH * ratio));
                float maxScroll = totalLeftH - leftListH;
                float availH = Math.Max(1, trackH - thumbH);
                int thumbY = trackY + (int)((_colLeftScroll / maxScroll) * availH);

                Color thumbColor = (_isDraggingColLeftScroll || _isHoveringColLeftScroll) ? Color.FromArgb(235, 255, 80, 160) : Color.FromArgb(90, 255, 255, 255);
                using (GraphicsPath thumbPath = RoundedRect(new Rectangle(trackX, thumbY, ScrollBarW, thumbH), 3))
                using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
                {
                    g.FillPath(thumbBrush, thumbPath);
                }
            }

            // ===== RIGHT COLUMN LIST (Tracks in Selected Playlist) =====
            int rightListTop = RightListTop;
            int rightListH = RightListHeight;
            Rectangle rightClip = new Rectangle(rightColX, rightListTop, rightColW, rightListH);
            g.SetClip(rightClip);

            string currentId = _audioPlayer?.CurrentVideoId;

            if (_filteredCollectionTracks.Count == 0)
            {
                using (Font emptyFont = new Font("Segoe UI", 9.0f, FontStyle.Italic))
                using (SolidBrush emptyBrush = new SolidBrush(Color.FromArgb(135, 140, 165)))
                using (StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    string msg = string.IsNullOrWhiteSpace(_searchQuery)
                        ? "Bộ sưu tập này chưa có bài hát nào"
                        : "Không tìm thấy bài hát phù hợp";
                    g.DrawString(msg, emptyFont, emptyBrush, rightClip, sfCenter);
                }
            }
            else
            {
                int totalRightH = _filteredCollectionTracks.Count * RowH;
                bool hasRightScroll = totalRightH > rightListH && rightListH > 0;
                int trackItemW = hasRightScroll ? (rightColW - ScrollBarW - 4) : rightColW;

                using (Font idxFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                using (Font titleFont = new Font("Segoe UI", 15.0f, FontStyle.Bold))
                using (Font authorFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                using (Font durFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (StringFormat titleSf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                using (StringFormat durSf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                using (StringFormat idxSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    for (int i = 0; i < _filteredCollectionTracks.Count; i++)
                    {
                        int rowY = rightListTop + (i * RowH) - _colRightScroll;
                        if (rowY + RowH < rightListTop || rowY > rightListTop + rightListH) continue;

                        PlaylistItem item = _filteredCollectionTracks[i];
                        bool isPlaying = !string.IsNullOrEmpty(currentId) && string.Equals(item.Id, currentId, StringComparison.OrdinalIgnoreCase);
                        bool isHov = (i == _hoveredTrackRow);

                        Rectangle rowRect = new Rectangle(rightColX, rowY + 1, trackItemW, RowH - 2);

                        if (isPlaying)
                        {
                            using (GraphicsPath rp = RoundedRect(rowRect, 6))
                            using (SolidBrush activeBg = new SolidBrush(Color.FromArgb(50, 255, 60, 160)))
                            {
                                g.FillPath(activeBg, rp);
                            }
                            using (SolidBrush bar = new SolidBrush(Color.FromArgb(255, 65, 160)))
                            {
                                g.FillRectangle(bar, rowRect.X, rowRect.Y + 4, 3, rowRect.Height - 8);
                            }
                        }
                        else if (isHov)
                        {
                            using (GraphicsPath rp = RoundedRect(rowRect, 6))
                            using (SolidBrush hovBg = new SolidBrush(Color.FromArgb(25, 255, 255, 255)))
                            {
                                g.FillPath(hovBg, rp);
                            }
                        }

                        int leftX = rowRect.X + 8;
                        if (isPlaying)
                        {
                            using (SolidBrush eqBrush = new SolidBrush(Color.FromArgb(255, 100, 180)))
                            {
                                int eqY = rowY + (RowH - 12) / 2;
                                g.FillRectangle(eqBrush, leftX, eqY + 4, 2, 8);
                                g.FillRectangle(eqBrush, leftX + 4, eqY, 2, 12);
                                g.FillRectangle(eqBrush, leftX + 8, eqY + 2, 2, 10);
                            }
                            leftX += 18;
                        }
                        else
                        {
                            int idxW = (i + 1) >= 10 ? 22 : 18;
                            using (SolidBrush idxBrush = new SolidBrush(Color.FromArgb(120, 130, 160)))
                            {
                                g.DrawString((i + 1).ToString(), idxFont, idxBrush, new RectangleF(leftX, rowY, idxW, RowH), idxSf);
                            }
                            leftX += idxW + 4;
                        }

                        // Micro Thumbnail
                        int thumbSize = 38;
                        int thumbY = rowY + (RowH - thumbSize) / 2;
                        Rectangle thumbRect = new Rectangle(leftX, thumbY, thumbSize, thumbSize);
                        leftX = thumbRect.Right + 8;

                        Bitmap thumb = null;
                        if (!string.IsNullOrEmpty(item.Id))
                        {
                            thumb = ThumbnailManager.GetThumbnail(item.Id);
                        }
                        using (GraphicsPath tp = RoundedRect(thumbRect, 4))
                        {
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
                                using (SolidBrush b = new SolidBrush(Color.FromArgb(32, 22, 50)))
                                using (Pen p = new Pen(Color.FromArgb(50, 255, 255, 255), 1))
                                {
                                    g.FillPath(b, tp);
                                    g.DrawPath(p, tp);
                                }
                                using (Font noteFont = new Font("Segoe UI", 9f))
                                using (SolidBrush noteBrush = new SolidBrush(Color.FromArgb(140, 120, 160)))
                                using (StringFormat sfNote = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                {
                                    g.DrawString("♪", noteFont, noteBrush, thumbRect, sfNote);
                                }

                                if (!string.IsNullOrEmpty(item.Id))
                                {
                                    ThumbnailManager.QueueLoad(item.Id, () =>
                                    {
                                        if (IsHandleCreated && !IsDisposed)
                                        {
                                            try { BeginInvoke(new Action(Invalidate)); } catch { }
                                        }
                                    });
                                }
                            }
                        }

                        int durW = 44;
                        int durX = rowRect.Right - durW - 4;
                        string durText = !string.IsNullOrEmpty(item.FormattedDuration) ? item.FormattedDuration : "";
                        using (SolidBrush durBrush = new SolidBrush(Color.FromArgb(135, 140, 165)))
                        {
                            g.DrawString(durText, durFont, durBrush, new RectangleF(durX, rowY, durW, RowH), durSf);
                        }

                        int textW = durX - leftX - 6;
                        if (textW > 20)
                        {
                            Color titleColor = isPlaying ? Color.FromArgb(255, 230, 245) : (isHov ? Color.White : Color.FromArgb(230, 230, 240));
                            Color authorColor = isPlaying ? Color.FromArgb(230, 180, 215) : Color.FromArgb(155, 155, 180);

                            using (SolidBrush titleBrush = new SolidBrush(titleColor))
                            {
                                g.DrawString(item.Title ?? "Unknown Title", titleFont, titleBrush, new RectangleF(leftX, rowY + 3, textW, 26), titleSf);
                            }
                            using (SolidBrush authorBrush = new SolidBrush(authorColor))
                            {
                                g.DrawString(item.Author ?? "Unknown Artist", authorFont, authorBrush, new RectangleF(leftX, rowY + 30, textW, 19), titleSf);
                            }
                        }
                    }
                }
            }
            g.Clip = oldClip;

            // Right scrollbar
            int totalTracksH = _filteredCollectionTracks.Count * RowH;
            if (totalTracksH > rightListH && rightListH > 0)
            {
                int trackX = Width - ScrollBarW - 4;
                int trackY = rightListTop + 4;
                int trackH = rightListH - 8;

                float ratio = (float)rightListH / totalTracksH;
                int thumbH = Math.Max(24, (int)(trackH * ratio));
                float maxScroll = totalTracksH - rightListH;
                float availableTrackH = Math.Max(1, trackH - thumbH);
                int thumbY = trackY + (int)((_colRightScroll / maxScroll) * availableTrackH);

                Color thumbColor = (_isDraggingColRightScroll || _isHoveringColRightScroll) ? Color.FromArgb(235, 255, 80, 160) : Color.FromArgb(90, 255, 255, 255);
                using (GraphicsPath thumbPath = RoundedRect(new Rectangle(trackX, thumbY, ScrollBarW, thumbH), 3))
                using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
                {
                    g.FillPath(thumbBrush, thumbPath);
                }
            }
        }

        private void DrawStandardSession(Graphics g)
        {
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
                    else if (_activeTab == SideTab.Queue) emptyMsg = "Danh sách phát trống (Dán link YouTube vào ô tìm kiếm)";
                    else if (_activeTab == SideTab.History) emptyMsg = "Lịch sử nghe nhạc trống";
                    else if (_activeTab == SideTab.Saved) emptyMsg = "Chưa có bài hát đã tải";

                    g.DrawString(emptyMsg, emptyFont, emptyBrush, listBounds, sf);
                }
            }
            else
            {
                using (Font idxFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                using (Font titleFont = new Font("Segoe UI", 15.0f, FontStyle.Bold))
                using (Font authorFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                using (Font durFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (StringFormat titleSf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                using (StringFormat durSf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                using (StringFormat idxSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    for (int i = 0; i < _filteredItems.Count; i++)
                    {
                        int rowY = listTop + (i * RowH) - _scrollOffset;
                        if (rowY + RowH < listTop || rowY > listTop + listH) continue;

                        PlaylistItem item = _filteredItems[i];
                        bool isPlaying = !string.IsNullOrEmpty(currentId) && string.Equals(item.Id, currentId, StringComparison.OrdinalIgnoreCase);
                        bool isHover = (i == _hoveredRow);

                        Rectangle rowRect = new Rectangle(8, rowY, Width - 16, RowH - 2);

                        if (isPlaying)
                        {
                            using (GraphicsPath rp = RoundedRect(rowRect, 6))
                            using (SolidBrush activeBg = new SolidBrush(Color.FromArgb(50, 255, 60, 160)))
                            {
                                g.FillPath(activeBg, rp);
                            }
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

                        int leftX = rowRect.X + 8;
                        if (isPlaying)
                        {
                            using (SolidBrush eqBrush = new SolidBrush(Color.FromArgb(255, 100, 180)))
                            {
                                int eqY = rowY + (RowH - 12) / 2;
                                g.FillRectangle(eqBrush, leftX, eqY + 4, 2, 8);
                                g.FillRectangle(eqBrush, leftX + 4, eqY, 2, 12);
                                g.FillRectangle(eqBrush, leftX + 8, eqY + 2, 2, 10);
                            }
                            leftX += 18;
                        }
                        else
                        {
                            int idxW = (i + 1) >= 10 ? 22 : 18;
                            using (SolidBrush idxBrush = new SolidBrush(Color.FromArgb(120, 130, 160)))
                            {
                                g.DrawString((i + 1).ToString(), idxFont, idxBrush, new RectangleF(leftX, rowY, idxW, RowH), idxSf);
                            }
                            leftX += idxW + 4;
                        }

                        // Micro Thumbnail
                        int thumbSize = 38;
                        int thumbY = rowY + (RowH - thumbSize) / 2;
                        Rectangle thumbRect = new Rectangle(leftX, thumbY, thumbSize, thumbSize);
                        leftX = thumbRect.Right + 8;

                        Bitmap thumb = null;
                        if (!string.IsNullOrEmpty(item.Id))
                        {
                            thumb = ThumbnailManager.GetThumbnail(item.Id);
                        }
                        using (GraphicsPath tp = RoundedRect(thumbRect, 4))
                        {
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
                                using (SolidBrush b = new SolidBrush(Color.FromArgb(32, 22, 50)))
                                using (Pen p = new Pen(Color.FromArgb(50, 255, 255, 255), 1))
                                {
                                    g.FillPath(b, tp);
                                    g.DrawPath(p, tp);
                                }
                                using (Font noteFont = new Font("Segoe UI", 9f))
                                using (SolidBrush noteBrush = new SolidBrush(Color.FromArgb(140, 120, 160)))
                                using (StringFormat sfNote = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                {
                                    g.DrawString("♪", noteFont, noteBrush, thumbRect, sfNote);
                                }

                                if (!string.IsNullOrEmpty(item.Id))
                                {
                                    ThumbnailManager.QueueLoad(item.Id, () =>
                                    {
                                        if (IsHandleCreated && !IsDisposed)
                                        {
                                            try { BeginInvoke(new Action(Invalidate)); } catch { }
                                        }
                                    });
                                }
                            }
                        }

                        int durW = 44;
                        int durX = rowRect.Right - durW - 4;
                        string durText = !string.IsNullOrEmpty(item.FormattedDuration) ? item.FormattedDuration : "";
                        using (SolidBrush durBrush = new SolidBrush(Color.FromArgb(135, 140, 165)))
                        {
                            g.DrawString(durText, durFont, durBrush, new RectangleF(durX, rowY, durW, RowH), durSf);
                        }

                        int textW = durX - leftX - 6;
                        if (textW > 20)
                        {
                            Color titleColor = isPlaying ? Color.FromArgb(255, 230, 245) : (isHover ? Color.White : Color.FromArgb(230, 230, 240));
                            Color authorColor = isPlaying ? Color.FromArgb(230, 180, 215) : Color.FromArgb(155, 155, 180);

                            using (SolidBrush titleBrush = new SolidBrush(titleColor))
                            {
                                g.DrawString(item.Title ?? "Unknown Title", titleFont, titleBrush, new RectangleF(leftX, rowY + 3, textW, 26), titleSf);
                            }
                            using (SolidBrush authorBrush = new SolidBrush(authorColor))
                            {
                                g.DrawString(item.Author ?? "Unknown Artist", authorFont, authorBrush, new RectangleF(leftX, rowY + 30, textW, 19), titleSf);
                            }
                        }
                    }
                }
            }

            g.Clip = oldClip;

            // Scrollbar for 1-column list
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
