using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public class ActiveQueuePanel : Panel
    {
        private const int WM_ERASEBKGND = 0x0014;
        private const int HeaderH = 34;
        private int RowH => Height < 140 ? 46 : 54;
        private const int ScrollBarW = 6;

        public bool IsSingleNextTrackMode { get; set; } = false;
        public bool EmbeddedMode { get; set; } = false;

        public int GetNextTrackIndex()
        {
            if (_items == null || _items.Count == 0) return -1;
            string curId = _audioPlayer?.CurrentVideoId;
            int curIdx = -1;
            if (!string.IsNullOrEmpty(curId))
            {
                curIdx = _items.FindIndex(it => string.Equals(it.Id, curId, StringComparison.OrdinalIgnoreCase));
            }
            if (curIdx >= 0)
            {
                return (curIdx + 1 < _items.Count) ? curIdx + 1 : 0;
            }
            return 0;
        }

        private AudioPlayerService _audioPlayer;
        private List<PlaylistItem> _items = new List<PlaylistItem>();
        private int _scrollOffset = 0;
        private int _hoveredRow = -1;
        private bool _isDraggingScroll = false;
        private int _dragGrabOffsetY = 0;
        private bool _isHoveringScroll = false;

        private bool _isLoading = false;
        private bool _isSorted = false;
        private bool _hoverSaveBtn = false;
        private bool _hoverSortBtn = false;

        private Rectangle GetSortBtnRect() => new Rectangle(Width - 12 - 36, 5, 36, 24);
        private Rectangle GetSaveBtnRect() => new Rectangle(Width - 12 - 36 - 6 - 62, 5, 62, 24);
        private Timer _dotsTimer;
        private int _dotCount = 1;
        private DynamicThemePalette _theme = ColorExtractorService.GetDefaultTheme();

        public void SetTheme(DynamicThemePalette theme)
        {
            if (theme != null)
            {
                _theme = theme;
                Invalidate();
            }
        }

        public event EventHandler<PlaylistItem> TrackSelected;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    Invalidate();
                }
            }
        }

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
                            _isLoading = false;
                            try { BeginInvoke(new Action(RefreshData)); } catch { }
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
                    RefreshData();
                }
            }
        }

        public ActiveQueuePanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;

            _dotsTimer = new Timer { Interval = 350 };
            _dotsTimer.Tick += (s, e) =>
            {
                _dotCount = (_dotCount % 3) + 1;
                bool isFetching = _isLoading || (_items.Count == 0 && _audioPlayer != null && (_audioPlayer.IsLoading || _audioPlayer.IsDownloadingOrPreloading));
                if (isFetching && Visible)
                {
                    Invalidate();
                }
            };
            _dotsTimer.Start();
        }

        public void RefreshData()
        {
            try
            {
                _items.Clear();
                var qList = _audioPlayer?.Playlist ?? _audioPlayer?.SessionPlaylist;
                if (qList != null && qList.Count > 0)
                {
                    _items.AddRange(qList);
                }
                else if (_audioPlayer != null && _audioPlayer.HasTrack)
                {
                    // Fallback to currently playing track if playlist has not been built
                    string tit = _audioPlayer.CurrentTitle;
                    if (!string.IsNullOrEmpty(tit))
                    {
                        _items.Add(new PlaylistItem
                        {
                            Id = _audioPlayer.CurrentVideoId ?? string.Empty,
                            Title = tit,
                            Author = _audioPlayer.CurrentAuthor ?? "YouTube",
                            DurationSeconds = (int)_audioPlayer.TotalDuration.TotalSeconds
                        });
                    }
                }

                if (_isSorted && _items.Count > 1)
                {
                    _items.Sort((a, b) => string.Compare(a?.Title, b?.Title, StringComparison.CurrentCultureIgnoreCase));
                }
            }
            catch { }

            ClampScroll();
            Invalidate();
        }

        private int ListTop => HeaderH + 2;
        private int ListHeight => Math.Max(10, Height - ListTop - 4);
        private int TotalListContentHeight => _items.Count * RowH;

        private int SingleRowHeight => Math.Max(56, Math.Min(68, ListHeight - 6));
        private Rectangle SingleRowRect => new Rectangle(8, ListTop + Math.Max(0, (ListHeight - SingleRowHeight) / 2), Math.Max(10, Width - 16), SingleRowHeight);

        private void ClampScroll()
        {
            int maxScroll = Math.Max(0, TotalListContentHeight - ListHeight);
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, maxScroll));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClampScroll();
            Invalidate();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                RefreshData();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (IsSingleNextTrackMode)
            {
                int nextIdx = GetNextTrackIndex();
                bool hover = false;
                if (nextIdx >= 0 && nextIdx < _items.Count)
                {
                    hover = SingleRowRect.Contains(e.Location);
                }
                int newRow = hover ? nextIdx : -1;
                if (_hoveredRow != newRow)
                {
                    _hoveredRow = newRow;
                    Invalidate();
                }
                Cursor = hover ? Cursors.Hand : Cursors.Default;
                return;
            }

            bool needRepaint = false;

            bool overScroll = (e.X >= Width - ScrollBarW - 14 && e.X <= Width && TotalListContentHeight > ListHeight && e.Y >= ListTop && e.Y <= ListTop + ListHeight);
            if (overScroll != _isHoveringScroll)
            {
                _isHoveringScroll = overScroll;
                needRepaint = true;
            }

            int prevRow = _hoveredRow;
            _hoveredRow = -1;
            if (!overScroll && e.Y >= ListTop && e.Y <= ListTop + ListHeight && e.X >= 6 && e.X <= Width - 14)
            {
                int relY = e.Y - ListTop + _scrollOffset;
                int rowIdx = relY / RowH;
                if (rowIdx >= 0 && rowIdx < _items.Count)
                {
                    _hoveredRow = rowIdx;
                }
            }
            if (_hoveredRow != prevRow) needRepaint = true;

            if (_isDraggingScroll)
            {
                int listH = ListHeight;
                int trackY = ListTop + 4;
                int trackH = listH - 8;
                float ratio = (float)listH / TotalListContentHeight;
                int thumbH = Math.Max(24, (int)(trackH * ratio));
                float maxScroll = TotalListContentHeight - listH;
                float availableTrackH = Math.Max(1, trackH - thumbH);

                int newThumbY = e.Y - _dragGrabOffsetY;
                float scrollRatio = (float)(newThumbY - trackY) / availableTrackH;
                _scrollOffset = (int)Math.Round(Math.Max(0f, Math.Min(1f, scrollRatio)) * maxScroll);
                ClampScroll();
                needRepaint = true;
            }

            if (needRepaint) Invalidate();
            bool prevSave = _hoverSaveBtn;
            bool prevSort = _hoverSortBtn;
            if (!IsSingleNextTrackMode && _items.Count > 0)
            {
                _hoverSaveBtn = GetSaveBtnRect().Contains(e.Location);
                _hoverSortBtn = GetSortBtnRect().Contains(e.Location);
            }
            else
            {
                _hoverSaveBtn = false;
                _hoverSortBtn = false;
            }
            if (prevSave != _hoverSaveBtn || prevSort != _hoverSortBtn) Invalidate();

            Cursor = (_hoveredRow >= 0 || _isHoveringScroll || _isDraggingScroll || _hoverSaveBtn || _hoverSortBtn) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_isDraggingScroll)
            {
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

            if (!IsSingleNextTrackMode && _items.Count > 0)
            {
                if (GetSortBtnRect().Contains(e.Location))
                {
                    _isSorted = !_isSorted;
                    RefreshData();
                    return;
                }
                if (GetSaveBtnRect().Contains(e.Location))
                {
                    PromptSaveQueueToCollection();
                    return;
                }
            }

            if (IsSingleNextTrackMode)
            {
                int nextIdx = GetNextTrackIndex();
                if (nextIdx >= 0 && nextIdx < _items.Count)
                {
                    if (SingleRowRect.Contains(e.Location))
                    {
                        PlaylistItem item = _items[nextIdx];
                        if (_audioPlayer != null)
                        {
                            var pl = _audioPlayer.Playlist;
                            if (pl != null && pl.Contains(item))
                            {
                                int idx = pl.IndexOf(item);
                                _audioPlayer.PlayTrackAtIndex(idx);
                            }
                            else
                            {
                                TrackSelected?.Invoke(this, item);
                                _audioPlayer.LoadAndPlay(item.Url);
                            }
                        }
                        Invalidate();
                    }
                }
                return;
            }

            int listTop = ListTop;
            int listH = ListHeight;

            // Scrollbar click & drag
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

            // Row click
            if (e.Y >= ListTop && e.Y <= ListTop + ListHeight)
            {
                int relY = e.Y - ListTop + _scrollOffset;
                int rowIdx = relY / RowH;
                if (rowIdx >= 0 && rowIdx < _items.Count)
                {
                    PlaylistItem item = _items[rowIdx];
                    if (_audioPlayer != null)
                    {
                        var pl = _audioPlayer.Playlist;
                        if (pl != null && pl.Contains(item))
                        {
                            int idx = pl.IndexOf(item);
                            _audioPlayer.PlayTrackAtIndex(idx);
                        }
                        else
                        {
                            TrackSelected?.Invoke(this, item);
                            _audioPlayer.LoadAndPlay(item.Url);
                        }
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
            _scrollOffset -= (e.Delta / 120) * 42;
            ClampScroll();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. Card Background & Border (Translucent glassmorphism, opacity < 100%)
            if (!EmbeddedMode)
            {
                Rectangle cardRect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = RoundedRect(cardRect, 22))
                {
                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(150, 22, 10, 42)))
                    {
                        g.FillPath(bgBrush, path);
                    }
                    using (Pen borderPen = new Pen(_theme.CardBorderColor, 1.2f))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }
            }
            else
            {
                using (Pen topDiv = new Pen(Color.FromArgb(35, 255, 255, 255), 1f))
                {
                    g.DrawLine(topDiv, 12, 0, Width - 12, 0);
                }
            }

            // 2. Header (Title + Save List + Sort)
            string titleText = IsSingleNextTrackMode ? "Bài tiếp theo" : (_items.Count > 0 ? $"Hàng đợi ({_items.Count})" : "Hàng đợi");
            using (Font titleFont = new Font("Segoe UI", Height < 140 ? 10.5f : 11.5f, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(250, 245, 255)))
            {
                g.DrawString(titleText, titleFont, titleBrush, new PointF(16, Height < 140 ? 7 : 9));
            }

            if (!IsSingleNextTrackMode && _items.Count > 0)
            {
                Rectangle saveRect = GetSaveBtnRect();
                Rectangle sortRect = GetSortBtnRect();

                DrawHeaderActionPill(g, saveRect, "Lưu list", _hoverSaveBtn, false);
                DrawHeaderActionPill(g, sortRect, "A-Z", _hoverSortBtn, _isSorted);
            }

            // Header separator line
            using (Pen sepPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1f))
            {
                g.DrawLine(sepPen, 14, HeaderH, Width - 14, HeaderH);
            }

            // 3. Track List
            int listTop = ListTop;
            int listH = ListHeight;
            Rectangle listBounds = new Rectangle(6, listTop, Width - 12, listH);

            Region oldClip = g.Clip;
            g.SetClip(listBounds);

            string currentId = _audioPlayer?.CurrentVideoId;

            bool isFetching = _isLoading || (_items.Count == 0 && _audioPlayer != null && (_audioPlayer.IsLoading || _audioPlayer.IsDownloadingOrPreloading));
            if (isFetching)
            {
                using (Font loadingFont = new Font("Segoe UI", 12.0f, FontStyle.Bold))
                using (SolidBrush loadBrush = new SolidBrush(Color.FromArgb(255, 60, 160)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    string dots = new string('.', _dotCount);
                    string loadingText = "Đang tải danh sách" + dots;
                    g.DrawString(loadingText, loadingFont, loadBrush, listBounds, sf);
                }
            }
            else if (_items.Count == 0)
            {
                using (Font emptyFont = new Font("Segoe UI", 10.0f, FontStyle.Italic))
                using (SolidBrush emptyBrush = new SolidBrush(Color.FromArgb(135, 130, 160)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Chưa có bài tiếp theo", emptyFont, emptyBrush, listBounds, sf);
                }
            }
            else if (IsSingleNextTrackMode)
            {
                int nextIdx = GetNextTrackIndex();
                if (nextIdx < 0 || nextIdx >= _items.Count)
                {
                    using (Font emptyFont = new Font("Segoe UI", 10.0f, FontStyle.Italic))
                    using (SolidBrush emptyBrush = new SolidBrush(Color.FromArgb(135, 130, 160)))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("Chưa có bài tiếp theo", emptyFont, emptyBrush, listBounds, sf);
                    }
                }
                else
                {
                    PlaylistItem item = _items[nextIdx];
                    Rectangle rowRect = SingleRowRect;
                    bool isHovered = (nextIdx == _hoveredRow);

                    if (isHovered)
                    {
                        using (GraphicsPath rp = RoundedRect(rowRect, 10))
                        using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                        {
                            g.FillPath(hoverBg, rp);
                        }
                    }

                    // 1. Next pill indicator
                    int indexX = rowRect.X + 8;
                    using (SolidBrush pillBrush = new SolidBrush(_theme.AccentColor))
                    {
                        int pillH = Math.Min(22, rowRect.Height - 24);
                        int pillY = rowRect.Y + (rowRect.Height - pillH) / 2;
                        g.FillRectangle(pillBrush, indexX, pillY, 3, pillH);
                    }

                    // 2. Thumbnail (scaled up to fill card nicely)
                    int thumbX = indexX + 12;
                    int thumbSize = Math.Min(52, rowRect.Height - 12);
                    int thumbY = rowRect.Y + (rowRect.Height - thumbSize) / 2;
                    Rectangle thumbRect = new Rectangle(thumbX, thumbY, thumbSize, thumbSize);

                    Bitmap bmp = null;
                    if (!string.IsNullOrEmpty(item.Id))
                    {
                        bmp = ThumbnailManager.GetThumbnail(item.Id);
                        if (bmp == null)
                        {
                            ThumbnailManager.QueueLoad(item.Id, () =>
                            {
                                try { BeginInvoke(new Action(Invalidate)); } catch { }
                            });
                        }
                    }

                    using (GraphicsPath tp = RoundedRect(thumbRect, 8))
                    {
                        if (bmp != null)
                        {
                            g.DrawImage(bmp, thumbRect);
                        }
                        else
                        {
                            using (SolidBrush placeholderBrush = new SolidBrush(Color.FromArgb(50, 40, 75)))
                            {
                                g.FillPath(placeholderBrush, tp);
                            }
                        }
                    }

                    // 3. Duration on right
                    string durStr = item.FormattedDuration ?? string.Empty;
                    int durW = 56;
                    int durX = rowRect.Right - durW - 10;
                    if (!string.IsNullOrEmpty(durStr))
                    {
                        using (Font durFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                        using (SolidBrush durBrush = new SolidBrush(Color.FromArgb(145, 140, 170)))
                        using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                        {
                            g.DrawString(durStr, durFont, durBrush, new Rectangle(durX, rowRect.Y, durW, rowRect.Height), sf);
                        }
                    }

                    // 4. Title and Author
                    int textLeft = thumbRect.Right + 12;
                    int textWidth = Math.Max(40, durX - textLeft - 8);

                    using (Font tFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                    using (Font aFont = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                    using (SolidBrush tBrush = new SolidBrush(isHovered ? Color.White : Color.FromArgb(245, 245, 255)))
                    using (SolidBrush aBrush = new SolidBrush(Color.FromArgb(155, 150, 180)))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    {
                        RectangleF tRect = new RectangleF(textLeft, rowRect.Y + (rowRect.Height - 42) / 2, textWidth, 22);
                        RectangleF aRect = new RectangleF(textLeft, tRect.Bottom + 1, textWidth, 18);
                        g.DrawString(item.Title ?? string.Empty, tFont, tBrush, tRect, sf);
                        string authorText = !string.IsNullOrWhiteSpace(item.Author) ? item.Author : Services.AudioPlayerService.ExtractAuthorFromTitle(item.Title);
                        if (string.IsNullOrWhiteSpace(authorText)) authorText = "YouTube";
                        g.DrawString(authorText, aFont, aBrush, aRect, sf);
                    }
                }
            }
            else
            {
                int startIdx = Math.Max(0, _scrollOffset / RowH);
                int endIdx = Math.Min(_items.Count - 1, (_scrollOffset + listH) / RowH + 1);

                for (int i = startIdx; i <= endIdx; i++)
                {
                    PlaylistItem item = _items[i];
                    int rowY = listTop + (i * RowH) - _scrollOffset;
                    Rectangle rowRect = new Rectangle(8, rowY + 2, Width - 16 - (TotalListContentHeight > listH ? ScrollBarW + 6 : 0), RowH - 4);

                    bool isPlaying = !string.IsNullOrEmpty(currentId) && string.Equals(item.Id, currentId, StringComparison.OrdinalIgnoreCase);
                    bool isHovered = (i == _hoveredRow);

                    // Row background
                    if (isPlaying)
                    {
                        using (GraphicsPath rp = RoundedRect(rowRect, 8))
                        using (SolidBrush playBg = new SolidBrush(_theme.ActiveRowBg))
                        using (Pen playBorder = new Pen(_theme.ActiveRowBorder, 1.2f))
                        {
                            g.FillPath(playBg, rp);
                            g.DrawPath(playBorder, rp);
                        }
                        // Left active indicator pill
                        using (SolidBrush pillBrush = new SolidBrush(_theme.AccentColor))
                        {
                            g.FillRectangle(pillBrush, rowRect.X + 2, rowRect.Y + 8, 3, rowRect.Height - 16);
                        }
                    }
                    else if (isHovered)
                    {
                        using (GraphicsPath rp = RoundedRect(rowRect, 8))
                        using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                        {
                            g.FillPath(hoverBg, rp);
                        }
                    }

                    // 1. Index or Soundwave icon
                    int indexX = rowRect.X + 10;
                    if (isPlaying)
                    {
                        // Draw animated/equalizer wave icon
                        using (SolidBrush waveBrush = new SolidBrush(_theme.AccentColor))
                        {
                            int waveX = indexX + 2;
                            int waveY = rowRect.Y + (rowRect.Height - 14) / 2;
                            g.FillRectangle(waveBrush, waveX, waveY + 4, 3, 10);
                            g.FillRectangle(waveBrush, waveX + 5, waveY, 3, 14);
                            g.FillRectangle(waveBrush, waveX + 10, waveY + 3, 3, 11);
                        }
                    }
                    else
                    {
                        int idxW = (i + 1) >= 1000 ? 46 : ((i + 1) >= 100 ? 38 : ((i + 1) >= 10 ? 32 : 24));
                        using (Font idxFont = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                        using (SolidBrush idxBrush = new SolidBrush(Color.FromArgb(130, 125, 155)))
                        using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                        {
                            g.DrawString((i + 1).ToString(), idxFont, idxBrush, new Rectangle(indexX, rowRect.Y, idxW, rowRect.Height), sf);
                        }
                    }

                    // 2. Thumbnail
                    int thumbX = indexX + ((i + 1) >= 1000 ? 52 : ((i + 1) >= 100 ? 44 : 36));
                    int thumbSize = RowH < 50 ? 32 : 38;
                    int thumbY = rowRect.Y + (rowRect.Height - thumbSize) / 2;
                    Rectangle thumbRect = new Rectangle(thumbX, thumbY, thumbSize, thumbSize);

                    Bitmap bmp = null;
                    if (!string.IsNullOrEmpty(item.Id))
                    {
                        bmp = ThumbnailManager.GetThumbnail(item.Id);
                        if (bmp == null)
                        {
                            ThumbnailManager.QueueLoad(item.Id, () =>
                            {
                                try { BeginInvoke(new Action(Invalidate)); } catch { }
                            });
                        }
                    }

                    using (GraphicsPath tp = RoundedRect(thumbRect, 6))
                    {
                        if (bmp != null)
                        {
                            ThumbnailManager.DrawRoundedImage(g, bmp, thumbRect, tp);
                        }
                        else
                        {
                            using (SolidBrush tbg = new SolidBrush(Color.FromArgb(42, 28, 68)))
                            {
                                g.FillPath(tbg, tp);
                            }
                            using (Font noteFont = new Font("Segoe UI", 10.0f, FontStyle.Bold))
                            using (SolidBrush noteBrush = new SolidBrush(Color.FromArgb(120, 115, 150)))
                            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("♬", noteFont, noteBrush, thumbRect, sf);
                            }
                        }
                    }

                    // 3. Duration on right (dynamic width with NoWrap, supporting long formats like hh:mm:ss without wrapping)
                    string durStr = item.FormattedDuration ?? string.Empty;
                    using (Font durFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                    {
                        SizeF durSz = !string.IsNullOrEmpty(durStr) ? g.MeasureString(durStr, durFont) : SizeF.Empty;
                        int durW = Math.Max(64, (int)Math.Ceiling(durSz.Width) + 12);
                        int durX = rowRect.Right - durW - 8;
                        using (SolidBrush durBrush = new SolidBrush(Color.FromArgb(145, 140, 170)))
                        using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                        {
                            g.DrawString(durStr, durFont, durBrush, new Rectangle(durX, rowRect.Y, durW, rowRect.Height), sf);
                        }

                        // 4. Title and Author (proportional font, bounded width)
                        int textLeft = thumbRect.Right + 12;
                        int textWidth = Math.Max(40, durX - textLeft - 8);

                        Color titleColor = isPlaying ? ColorExtractorService.Lerp(_theme.AccentColor, Color.White, 0.2f) : Color.FromArgb(245, 245, 255);
                        using (Font tFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                        using (Font aFont = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                        using (SolidBrush tBrush = new SolidBrush(titleColor))
                        using (SolidBrush aBrush = new SolidBrush(Color.FromArgb(155, 150, 180)))
                        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                        {
                            RectangleF tRect = new RectangleF(textLeft, rowRect.Y + 7, textWidth, 20);
                            RectangleF aRect = new RectangleF(textLeft, rowRect.Y + 28, textWidth, 18);
                            g.DrawString(item.Title ?? string.Empty, tFont, tBrush, tRect, sf);
                            string authorText = ResolveDisplayAuthor(item);
                            g.DrawString(authorText, aFont, aBrush, aRect, sf);
                        }
                    }
                }
            }

            g.Clip = oldClip;

            // 4. Custom Scrollbar
            if (!IsSingleNextTrackMode && TotalListContentHeight > listH)
            {
                int trackX = Width - ScrollBarW - 6;
                int trackY = listTop + 4;
                int trackH = listH - 8;
                float ratio = (float)listH / TotalListContentHeight;
                int thumbH = Math.Max(24, (int)(trackH * ratio));
                float maxScroll = TotalListContentHeight - listH;
                float availableTrackH = Math.Max(1, trackH - thumbH);
                int thumbY = trackY + (int)((_scrollOffset / maxScroll) * availableTrackH);

                Color thumbColor = (_isDraggingScroll || _isHoveringScroll) ? Color.FromArgb(220, _theme.AccentColor) : Color.FromArgb(110, _theme.SecondaryAccentColor);
                Rectangle thumbRect = new Rectangle(trackX, thumbY, ScrollBarW, thumbH);
                using (GraphicsPath thumbPath = RoundedRect(thumbRect, ScrollBarW / 2))
                using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
                {
                    g.FillPath(thumbBrush, thumbPath);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dotsTimer?.Stop();
                _dotsTimer?.Dispose();
            }
            base.Dispose(disposing);
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

        private void DrawHeaderActionPill(Graphics g, Rectangle rect, string text, bool hovered, bool active)
        {
            Color bg = active
                ? _theme.AccentColor
                : (hovered ? Color.FromArgb(180, 50, 28, 88) : Color.FromArgb(130, 32, 18, 60));
            Color brd = active ? Color.White : Color.FromArgb(95, _theme.SecondaryAccentColor);

            using (GraphicsPath p = RoundedRect(rect, 11))
            using (SolidBrush b = new SolidBrush(bg))
            using (Pen pen = new Pen(brd, 1f))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }

            using (Font f = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.White))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(text, f, tb, rect, sf);
            }
        }

        private void PromptSaveQueueToCollection()
        {
            if (_items == null || _items.Count == 0) return;

            using (Form dlg = new Form())
            {
                dlg.Text = "Lưu hàng đợi vào Bộ sưu tập";
                dlg.Size = new Size(340, 155);
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Color.FromArgb(24, 14, 46);
                dlg.ForeColor = Color.White;

                Label lbl = new Label
                {
                    Text = "Tên danh sách mới:",
                    Location = new Point(16, 14),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = Color.White
                };
                TextBox txt = new TextBox
                {
                    Location = new Point(16, 40),
                    Width = 290,
                    Text = "Hàng đợi " + DateTime.Now.ToString("dd/MM HH:mm"),
                    BackColor = Color.FromArgb(38, 22, 68),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Segoe UI", 9.5f)
                };
                Button btnOk = new Button
                {
                    Text = "Lưu",
                    DialogResult = DialogResult.OK,
                    Location = new Point(146, 78),
                    Size = new Size(76, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = _theme.AccentColor,
                    ForeColor = Color.White
                };
                Button btnCancel = new Button
                {
                    Text = "Hủy",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(230, 78),
                    Size = new Size(76, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 34, 80),
                    ForeColor = Color.White
                };

                dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    string name = txt.Text?.Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        PlaylistCollectionService.Instance.SavePlaylist(name, new List<PlaylistItem>(_items));
                    }
                }
            }
        }

        private string ResolveDisplayAuthor(PlaylistItem item)
        {
            if (item == null) return string.Empty;
            string raw = item.Author;
            if (!string.IsNullOrWhiteSpace(raw) && !string.Equals(raw.Trim(), "YouTube", StringComparison.OrdinalIgnoreCase))
            {
                return raw;
            }

            string fromTitle = YouTubeSearchService.ExtractSmartArtistFromTitle(item.Title);
            if (!string.IsNullOrWhiteSpace(fromTitle))
            {
                item.Author = fromTitle;
                return fromTitle;
            }

            string cached = YouTubeSearchService.GetCachedArtist(item.Id);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                item.Author = cached;
                return cached;
            }

            YouTubeSearchService.QueueResolveArtist(item, () =>
            {
                if (!IsDisposed)
                {
                    try { BeginInvoke(new Action(Invalidate)); } catch { }
                }
            });

            return "Nghệ sĩ";
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

