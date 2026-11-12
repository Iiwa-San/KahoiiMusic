using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Windows.Forms;
using KahoiiMusic.Models;

namespace KahoiiMusic.Controls
{
    public class SearchResultsView : UserControl
    {
        private List<PlaylistItem> _items = new List<PlaylistItem>();
        private string _query = string.Empty;
        private int _scrollOffset = 0;
        private int _maxScroll = 0;
        private int _hoveredIndex = -1;
        private bool _isHoveringBack = false;
        private bool _isHoveringBestMatchPlay = false;

        private Rectangle _backBtnRect;
        private Rectangle _bestMatchRect;
        private Rectangle _bestMatchPlayRect;
        private Dictionary<string, Image> _thumbCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _loadingThumbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Scrollbar dragging
        private bool _isDraggingScroll = false;
        private int _dragStartY = 0;
        private int _dragStartScroll = 0;
        private Rectangle _scrollBarThumbRect;

        private bool IsLarge => Width >= 900;
        private int ActualTopHeaderHeight => IsLarge ? 54 : 44;
        private int ActualHeroCardHeight => IsLarge ? 120 : 84;
        private int ActualItemRowHeight => IsLarge ? 64 : 42;

        public event EventHandler<PlaylistItem> TrackPlaySelected;
        public event EventHandler<PlaylistItem> TrackAddSelected;
        public event EventHandler CloseRequested;

        public SearchResultsView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            BackColor = Color.FromArgb(16, 13, 30);
            DoubleBuffered = true;
        }

        private bool _isLoading = false;

        public void ShowLoading(string query)
        {
            _query = query ?? string.Empty;
            _items = new List<PlaylistItem>();
            _isLoading = true;
            _scrollOffset = 0;
            _hoveredIndex = -1;
            Invalidate();
        }

        public void SetResults(string query, List<PlaylistItem> items)
        {
            _isLoading = false;
            _query = query ?? string.Empty;
            _items = items ?? new List<PlaylistItem>();
            _scrollOffset = 0;
            _hoveredIndex = -1;

            UpdateScrollBounds();

            // Pre-load thumbnails only for the first visible items (up to 8)
            int preloadCount = Math.Min(8, _items.Count);
            for (int i = 0; i < preloadCount; i++)
            {
                var item = _items[i];
                if (item != null && !string.IsNullOrEmpty(item.Id))
                {
                    RequestThumbnail(item.Id);
                }
            }

            Invalidate();
        }

        private void RequestThumbnail(string videoId)
        {
            if (string.IsNullOrEmpty(videoId) || _thumbCache.ContainsKey(videoId)) return;
            lock (_loadingThumbs)
            {
                if (!_loadingThumbs.Add(videoId)) return;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                LoadThumbnailAsync(videoId);
            });
        }

        private void LoadThumbnailAsync(string videoId)
        {
            try
            {
                string url = string.Format("https://i.ytimg.com/vi/{0}/mqdefault.jpg", videoId);
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 4000;
                req.UserAgent = "Mozilla/5.0";
                using (var res = req.GetResponse())
                using (var stream = res.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    using (var rawImg = Image.FromStream(ms))
                    {
                        var copy = new Bitmap(rawImg);
                        if (!IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke(new Action(() =>
                            {
                                lock (_loadingThumbs)
                                {
                                    _loadingThumbs.Remove(videoId);
                                }
                                if (!_thumbCache.ContainsKey(videoId))
                                {
                                    _thumbCache[videoId] = copy;
                                    Invalidate();
                                }
                            }));
                        }
                    }
                }
            }
            catch
            {
                lock (_loadingThumbs)
                {
                    _loadingThumbs.Remove(videoId);
                }
            }
        }

        private int ScrollTrackX => Width - 14;
        private int ScrollTrackY => ActualTopHeaderHeight + 4;
        private int ScrollTrackHeight => Math.Max(10, Height - ScrollTrackY - 6);
        private int ScrollTrackWidth => 8;

        private Rectangle GetScrollTrackHitRect()
        {
            return new Rectangle(ScrollTrackX - 4, ScrollTrackY, ScrollTrackWidth + 8, ScrollTrackHeight);
        }

        private int GetThumbHeight()
        {
            int trackHeight = ScrollTrackHeight;
            if (Height + _maxScroll <= 0) return 24;
            return Math.Max(24, (int)((float)Height / (Height + _maxScroll) * trackHeight));
        }

        private Rectangle GetThumbRect()
        {
            if (_maxScroll <= 0) return Rectangle.Empty;
            int trackY = ScrollTrackY;
            int trackHeight = ScrollTrackHeight;
            int thumbHeight = GetThumbHeight();
            int availTravel = Math.Max(1, trackHeight - thumbHeight);
            int thumbY = trackY + (int)((float)_scrollOffset / _maxScroll * availTravel);
            return new Rectangle(ScrollTrackX, thumbY, ScrollTrackWidth, thumbHeight);
        }

        private void UpdateScrollBounds()
        {
            int listStartY = ActualTopHeaderHeight + (_items.Count > 0 ? ActualHeroCardHeight + 12 : 0);
            int totalContentHeight = listStartY + (_items.Count * ActualItemRowHeight) + 24;
            _maxScroll = Math.Max(0, totalContentHeight - Height);
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, _maxScroll));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBounds();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_maxScroll > 0)
            {
                int delta = (e.Delta / 120) * -(ActualItemRowHeight * 2);
                _scrollOffset = Math.Max(0, Math.Min(_maxScroll, _scrollOffset + delta));
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDraggingScroll)
            {
                int thumbHeight = GetThumbHeight();
                int availTravel = Math.Max(1, ScrollTrackHeight - thumbHeight);
                int deltaY = e.Y - _dragStartY;
                float scrollRatio = (float)deltaY / availTravel;
                _scrollOffset = Math.Max(0, Math.Min(_maxScroll, _dragStartScroll + (int)(scrollRatio * _maxScroll)));
                Invalidate();
                return;
            }

            bool prevBack = _isHoveringBack;
            bool prevBest = _isHoveringBestMatchPlay;
            int prevIdx = _hoveredIndex;

            _isHoveringBack = _backBtnRect.Contains(e.Location);
            _isHoveringBestMatchPlay = _bestMatchPlayRect.Contains(e.Location);
            _hoveredIndex = -1;

            int listStartY = ActualTopHeaderHeight + (_items.Count > 0 ? ActualHeroCardHeight + 12 : 0) - _scrollOffset;

            if (e.X < Width - 18 && e.Y >= listStartY)
            {
                int row = (e.Y - listStartY) / ActualItemRowHeight;
                if (row >= 0 && row < _items.Count)
                {
                    _hoveredIndex = row;
                }
            }

            bool isOverScrollbar = _maxScroll > 0 && GetScrollTrackHitRect().Contains(e.Location);
            Cursor = (_isHoveringBack || _isHoveringBestMatchPlay || _hoveredIndex >= 0 || isOverScrollbar)
                ? Cursors.Hand : Cursors.Default;

            if (prevBack != _isHoveringBack || prevBest != _isHoveringBestMatchPlay || prevIdx != _hoveredIndex)
            {
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_maxScroll > 0 && GetScrollTrackHitRect().Contains(e.Location))
                {
                    _scrollBarThumbRect = GetThumbRect();
                    if (_scrollBarThumbRect.Contains(e.Location))
                    {
                        _isDraggingScroll = true;
                        _dragStartY = e.Y;
                        _dragStartScroll = _scrollOffset;
                    }
                    else
                    {
                        int thumbHeight = GetThumbHeight();
                        int availTravel = Math.Max(1, ScrollTrackHeight - thumbHeight);
                        int targetThumbY = e.Y - (thumbHeight / 2) - ScrollTrackY;
                        float ratio = Math.Max(0f, Math.Min(1f, (float)targetThumbY / availTravel));
                        _scrollOffset = (int)(ratio * _maxScroll);
                        _isDraggingScroll = true;
                        _dragStartY = e.Y;
                        _dragStartScroll = _scrollOffset;
                        Invalidate();
                    }
                    return;
                }

                if (_isHoveringBack)
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                if (_isHoveringBestMatchPlay && _items.Count > 0)
                {
                    TrackPlaySelected?.Invoke(this, _items[0]);
                    return;
                }

                if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count)
                {
                    PlaylistItem item = _items[_hoveredIndex];
                    int listStartY = ActualTopHeaderHeight + (_items.Count > 0 ? ActualHeroCardHeight + 12 : 0) - _scrollOffset;
                    int rowY = listStartY + (_hoveredIndex * ActualItemRowHeight);
                    int btnSize = IsLarge ? 32 : 24;
                    int rightPad = IsLarge ? 16 : 10;
                    int addX = Width - rightPad - btnSize;
                    Rectangle addRect = new Rectangle(addX, rowY + (ActualItemRowHeight - btnSize) / 2, btnSize, btnSize);

                    if (addRect.Contains(e.Location))
                    {
                        TrackAddSelected?.Invoke(this, item);
                    }
                    else
                    {
                        TrackPlaySelected?.Invoke(this, item);
                    }
                    return;
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDraggingScroll)
            {
                _isDraggingScroll = false;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background gradient
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(20, 15, 38), Color.FromArgb(12, 9, 24), LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // Top Header
            int backBtnW = IsLarge ? 160 : 126;
            int backBtnH = IsLarge ? 34 : 26;
            _backBtnRect = new Rectangle(IsLarge ? 16 : 12, (ActualTopHeaderHeight - backBtnH) / 2, backBtnW, backBtnH);
            using (GraphicsPath bPath = CreateRoundedRectPath(_backBtnRect, 6))
            using (SolidBrush bbBrush = new SolidBrush(_isHoveringBack ? Color.FromArgb(255, 45, 133) : Color.FromArgb(32, 25, 54)))
            using (Pen bbPen = new Pen(_isHoveringBack ? Color.White : Color.FromArgb(80, 255, 45, 133), 1f))
            {
                g.FillPath(bbBrush, bPath);
                g.DrawPath(bbPen, bPath);

                using (Font bFont = new Font("Segoe UI", IsLarge ? 9.5f : 8.0f, FontStyle.Bold))
                using (SolidBrush btBrush = new SolidBrush(Color.White))
                {
                    StringFormat bsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("← Quay lại đĩa nhạc", bFont, btBrush, _backBtnRect, bsf);
                }
            }

            // Header Title
            int titleX = _backBtnRect.Right + (IsLarge ? 16 : 12);
            int titleW = Width - titleX - 12;
            using (Font tFont = new Font("Segoe UI", IsLarge ? 11.5f : 9.0f, FontStyle.Bold))
            using (Font qFont = new Font("Segoe UI", IsLarge ? 11.5f : 9.0f, FontStyle.Bold))
            using (SolidBrush tBrush = new SolidBrush(Color.White))
            using (SolidBrush qBrush = new SolidBrush(Color.FromArgb(255, 45, 133)))
            {
                StringFormat tsf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center };
                string prefix = "Kết quả: ";
                int titleY = ActualTopHeaderHeight / 2;
                g.DrawString(prefix, tFont, tBrush, titleX, titleY, tsf);
                float pW = g.MeasureString(prefix, tFont).Width;
                int textH = IsLarge ? 26 : 20;
                g.DrawString("\"" + _query + "\"", qFont, qBrush, new RectangleF(titleX + pW, titleY - textH / 2, titleW - pW, textH), tsf);
            }

            using (Pen divPen = new Pen(Color.FromArgb(38, 30, 60)))
            {
                g.DrawLine(divPen, 10, ActualTopHeaderHeight - 1, Width - 10, ActualTopHeaderHeight - 1);
            }

            // Clip for scrollable area
            Rectangle contentArea = new Rectangle(0, ActualTopHeaderHeight, Width, Height - ActualTopHeaderHeight);
            g.SetClip(contentArea);

            if (_isLoading)
            {
                using (Font lFont = new Font("Segoe UI", IsLarge ? 12.5f : 10.0f, FontStyle.Bold))
                using (Font subFont = new Font("Segoe UI", IsLarge ? 10.5f : 8.5f, FontStyle.Regular))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(255, 45, 133)))
                using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(160, 150, 185)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("⚡ Đang tìm kiếm trên YouTube...", lFont, textBrush, new RectangleF(0, Height / 2 - 30, Width, 30), sf);
                    g.DrawString("Vui lòng đợi giây lát", subFont, subBrush, new RectangleF(0, Height / 2 + 5, Width, 24), sf);
                }
                return;
            }

            if (_items.Count == 0)
            {
                using (Font emptyFont = new Font("Segoe UI", IsLarge ? 12.0f : 9.5f, FontStyle.Regular))
                using (SolidBrush emptyBrush = new SolidBrush(Color.FromArgb(160, 150, 185)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("Không tìm thấy kết quả phù hợp cho \"" + _query + "\"", emptyFont, emptyBrush, new RectangleF(0, Height / 2 - 15, Width, 30), sf);
                }
                return;
            }

            int curY = ActualTopHeaderHeight + 8 - _scrollOffset;
            int cardPad = IsLarge ? 16 : 12;

            // Best Match Hero Card
            if (_items.Count > 0)
            {
                PlaylistItem hero = _items[0];
                _bestMatchRect = new Rectangle(cardPad, curY, Width - (cardPad * 2), ActualHeroCardHeight);

                using (GraphicsPath hPath = CreateRoundedRectPath(_bestMatchRect, IsLarge ? 12 : 8))
                using (LinearGradientBrush hBrush = new LinearGradientBrush(_bestMatchRect, Color.FromArgb(42, 28, 70), Color.FromArgb(24, 18, 46), LinearGradientMode.Horizontal))
                using (Pen hPen = new Pen(Color.FromArgb(90, 255, 45, 133), 1f))
                {
                    g.FillPath(hBrush, hPath);
                    g.DrawPath(hPen, hPath);
                }

                // Thumbnail (144x81 when large, 96x54 normal)
                int heroThumbW = IsLarge ? 144 : 96;
                int heroThumbH = IsLarge ? 81 : 54;
                Rectangle heroThumbRect = new Rectangle(_bestMatchRect.X + (IsLarge ? 16 : 10), _bestMatchRect.Y + (_bestMatchRect.Height - heroThumbH) / 2, heroThumbW, heroThumbH);
                
                using (GraphicsPath tp = CreateRoundedRectPath(heroThumbRect, IsLarge ? 8 : 4))
                {
                    Region oldRgn = g.Clip;
                    g.SetClip(tp, CombineMode.Intersect);
                    if (_thumbCache.TryGetValue(hero.Id, out Image hThumb) && hThumb != null)
                    {
                        g.DrawImage(hThumb, heroThumbRect);
                    }
                    else
                    {
                        RequestThumbnail(hero.Id);
                        using (SolidBrush tb = new SolidBrush(Color.FromArgb(30, 22, 50)))
                        {
                            g.FillRectangle(tb, heroThumbRect);
                        }
                    }
                    g.Clip = oldRgn;
                }

                // Big Play button
                int heroPlayW = IsLarge ? 120 : 74;
                int heroPlayH = IsLarge ? 38 : 30;
                _bestMatchPlayRect = new Rectangle(_bestMatchRect.Right - heroPlayW - (IsLarge ? 16 : 10), _bestMatchRect.Y + (_bestMatchRect.Height - heroPlayH) / 2, heroPlayW, heroPlayH);
                using (GraphicsPath pPath = CreateRoundedRectPath(_bestMatchPlayRect, 6))
                using (SolidBrush pbBrush = new SolidBrush(_isHoveringBestMatchPlay ? Color.FromArgb(255, 65, 150) : Color.FromArgb(255, 45, 133)))
                {
                    g.FillPath(pbBrush, pPath);
                    using (Font pFont = new Font("Segoe UI", IsLarge ? 10.0f : 8.0f, FontStyle.Bold))
                    using (SolidBrush ptBrush = new SolidBrush(Color.White))
                    {
                        StringFormat psf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("▶ Phát ngay", pFont, ptBrush, _bestMatchPlayRect, psf);
                    }
                }

                // Text info
                int infoX = heroThumbRect.Right + (IsLarge ? 16 : 12);
                int infoW = _bestMatchPlayRect.Left - infoX - 16;

                using (Font tagFont = new Font("Segoe UI", IsLarge ? 8.5f : 6.5f, FontStyle.Bold))
                using (Font heroTitleFont = new Font("Segoe UI", IsLarge ? 12.5f : 8.5f, FontStyle.Bold))
                using (Font heroSubFont = new Font("Segoe UI", IsLarge ? 10.5f : 7.5f, FontStyle.Regular))
                using (SolidBrush tagBrush = new SolidBrush(Color.FromArgb(255, 45, 133)))
                using (SolidBrush hTitleBrush = new SolidBrush(Color.White))
                using (SolidBrush hSubBrush = new SolidBrush(Color.FromArgb(160, 150, 185)))
                {
                    StringFormat tsf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    int tagY = IsLarge ? _bestMatchRect.Y + 14 : _bestMatchRect.Y + 10;
                    int titleY = IsLarge ? _bestMatchRect.Y + 36 : _bestMatchRect.Y + 24;
                    int subY = IsLarge ? _bestMatchRect.Y + 68 : _bestMatchRect.Y + 44;
                    int lineH = IsLarge ? 26 : 18;

                    g.DrawString("BÀI HÁT PHÙ HỢP NHẤT", tagFont, tagBrush, infoX, tagY);
                    g.DrawString(hero.Title ?? "Bài hát", heroTitleFont, hTitleBrush, new RectangleF(infoX, titleY, infoW, lineH), tsf);
                    g.DrawString((hero.Author ?? "YouTube") + " • " + hero.FormattedDuration, heroSubFont, hSubBrush, new RectangleF(infoX, subY, infoW, lineH), tsf);
                }

                curY += ActualHeroCardHeight + 12;
            }

            // Results List
            for (int i = 0; i < _items.Count; i++)
            {
                PlaylistItem item = _items[i];
                Rectangle rowRect = new Rectangle(cardPad, curY, Width - (cardPad * 2), ActualItemRowHeight);

                // Only render visible rows
                if (rowRect.Bottom >= ActualTopHeaderHeight && rowRect.Top <= Height)
                {
                    bool isHovered = (i == _hoveredIndex);

                    if (isHovered)
                    {
                        using (GraphicsPath rPath = CreateRoundedRectPath(rowRect, IsLarge ? 8 : 6))
                        using (SolidBrush rhBrush = new SolidBrush(Color.FromArgb(40, 28, 62)))
                        using (Pen rhPen = new Pen(Color.FromArgb(100, 255, 45, 133), 1f))
                        {
                            g.FillPath(rhBrush, rPath);
                            g.DrawPath(rhPen, rPath);
                        }
                    }

                    // Index #
                    int numW = IsLarge ? 34 : 20;
                    using (Font numFont = new Font("Segoe UI", IsLarge ? 10.5f : 8.0f, FontStyle.Bold))
                    using (SolidBrush numBrush = new SolidBrush(Color.FromArgb(130, 120, 150)))
                    {
                        StringFormat nsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString((i + 1).ToString(), numFont, numBrush, new Rectangle(rowRect.X + 2, rowRect.Y, numW, rowRect.Height), nsf);
                    }

                    // Thumbnail (78x44 when large, 46x26 normal)
                    int thumbW = IsLarge ? 78 : 46;
                    int thumbH = IsLarge ? 44 : 26;
                    Rectangle thumbRect = new Rectangle(rowRect.X + numW + 6, rowRect.Y + (rowRect.Height - thumbH) / 2, thumbW, thumbH);
                    
                    using (GraphicsPath tp = CreateRoundedRectPath(thumbRect, IsLarge ? 6 : 4))
                    {
                        Region oldRgn = g.Clip;
                        g.SetClip(tp, CombineMode.Intersect);
                        if (_thumbCache.TryGetValue(item.Id, out Image thumb) && thumb != null)
                        {
                            g.DrawImage(thumb, thumbRect);
                        }
                        else
                        {
                            RequestThumbnail(item.Id);
                            using (SolidBrush tb = new SolidBrush(Color.FromArgb(28, 22, 48)))
                            {
                                g.FillRectangle(tb, thumbRect);
                            }
                        }
                        g.Clip = oldRgn;
                    }

                    // Buttons sizing
                    int btnSize = IsLarge ? 32 : 22;
                    int rightPad = IsLarge ? 16 : 10;
                    int addX = Width - rightPad - btnSize;
                    int playX = addX - btnSize - (IsLarge ? 10 : 6);

                    // Quick Play (▶)
                    Rectangle playRect = new Rectangle(playX, rowRect.Y + (rowRect.Height - btnSize) / 2, btnSize, btnSize);
                    using (GraphicsPath btnPath = new GraphicsPath())
                    {
                        btnPath.AddEllipse(playRect);
                        using (SolidBrush pbBrush = new SolidBrush(isHovered ? Color.FromArgb(255, 45, 133) : Color.FromArgb(42, 32, 65)))
                        {
                            g.FillPath(pbBrush, btnPath);
                        }
                        using (Font pFont = new Font("Segoe UI", IsLarge ? 10.0f : 8.0f, FontStyle.Bold))
                        using (SolidBrush pTextBrush = new SolidBrush(isHovered ? Color.White : Color.FromArgb(255, 45, 133)))
                        {
                            StringFormat psf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString("▶", pFont, pTextBrush, playRect, psf);
                        }
                    }

                    // Add Button (+)
                    Rectangle addRect = new Rectangle(addX, rowRect.Y + (rowRect.Height - btnSize) / 2, btnSize, btnSize);
                    using (GraphicsPath btnPath = new GraphicsPath())
                    {
                        btnPath.AddEllipse(addRect);
                        using (SolidBrush abBrush = new SolidBrush(Color.FromArgb(32, 25, 52)))
                        {
                            g.FillPath(abBrush, btnPath);
                        }
                        using (Font aFont = new Font("Segoe UI", IsLarge ? 12.0f : 9.0f, FontStyle.Regular))
                        using (SolidBrush aTextBrush = new SolidBrush(Color.FromArgb(180, 170, 205)))
                        {
                            StringFormat asf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString("+", aFont, aTextBrush, addRect, asf);
                        }
                    }

                    // Title & Author
                    int textX = thumbRect.Right + (IsLarge ? 14 : 8);
                    int textW = playX - textX - (IsLarge ? 14 : 8);

                    using (Font titleFont = new Font("Segoe UI", IsLarge ? 11.5f : 8.25f, FontStyle.Bold))
                    using (Font subFont = new Font("Segoe UI", IsLarge ? 10.0f : 7.5f, FontStyle.Regular))
                    using (SolidBrush titleBrush = new SolidBrush(isHovered ? Color.FromArgb(255, 100, 165) : Color.White))
                    using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(150, 140, 175)))
                    {
                        StringFormat tsf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                        int tLineY = IsLarge ? rowRect.Y + 10 : rowRect.Y + 4;
                        int sLineY = IsLarge ? rowRect.Y + 34 : rowRect.Y + 20;
                        int tLineH = IsLarge ? 22 : 16;
                        int sLineH = IsLarge ? 18 : 14;

                        g.DrawString(item.Title ?? "Bài hát", titleFont, titleBrush, new RectangleF(textX, tLineY, textW, tLineH), tsf);

                        string sub = (item.Author ?? "YouTube") + " • " + item.FormattedDuration;
                        g.DrawString(sub, subFont, subBrush, new RectangleF(textX, sLineY, textW, sLineH), tsf);
                    }
                }

                curY += ActualItemRowHeight;
            }

            g.ResetClip();

            // Scrollbar
            if (_maxScroll > 0)
            {
                Rectangle trackRect = new Rectangle(ScrollTrackX, ScrollTrackY, ScrollTrackWidth, ScrollTrackHeight);
                using (GraphicsPath trackPath = CreateRoundedRectPath(trackRect, 3))
                using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(24, 20, 42)))
                {
                    g.FillPath(trackBrush, trackPath);
                }

                _scrollBarThumbRect = GetThumbRect();
                using (GraphicsPath sbPath = CreateRoundedRectPath(_scrollBarThumbRect, 3))
                using (SolidBrush sbBrush = new SolidBrush(_isDraggingScroll ? Color.FromArgb(255, 45, 133) : Color.FromArgb(90, 75, 130)))
                {
                    g.FillPath(sbBrush, sbPath);
                }
            }
        }

        private GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private const int WM_ERASEBKGND = 0x0014;
        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int HTTRANSPARENT = -1;
        private const int ResizeBorder = 8;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;
                return;
            }

            if (m.Msg == WM_NCHITTEST)
            {
                Point screenPt;
                if (m.LParam != IntPtr.Zero)
                {
                    int x = unchecked((short)(m.LParam.ToInt64() & 0xFFFF));
                    int y = unchecked((short)((m.LParam.ToInt64() >> 16) & 0xFFFF));
                    screenPt = new Point(x, y);
                }
                else
                {
                    screenPt = Cursor.Position;
                }

                Point clientPt = PointToClient(screenPt);

                // If hovering over scrollbar, handle as client area so clicks/drags are never hijacked by window resize!
                if (_maxScroll > 0 && GetScrollTrackHitRect().Contains(clientPt))
                {
                    m.Result = (IntPtr)HTCLIENT;
                    return;
                }

                Form parent = FindForm();
                if (parent != null && parent.WindowState == FormWindowState.Normal)
                {
                    Point pt = parent.PointToClient(screenPt);
                    int w = parent.ClientSize.Width;
                    int h = parent.ClientSize.Height;

                    if (pt.X <= ResizeBorder || pt.X >= w - ResizeBorder ||
                        pt.Y <= ResizeBorder || pt.Y >= h - ResizeBorder)
                    {
                        m.Result = (IntPtr)HTTRANSPARENT;
                        return;
                    }
                }
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var img in _thumbCache.Values)
                {
                    try { img?.Dispose(); } catch { }
                }
                _thumbCache.Clear();
            }
            base.Dispose(disposing);
        }
    }
}

