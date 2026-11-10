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

        // Scrollbar dragging
        private bool _isDraggingScroll = false;
        private int _dragStartY = 0;
        private int _dragStartScroll = 0;
        private Rectangle _scrollBarThumbRect;

        private const int TopHeaderHeight = 44;
        private const int HeroCardHeight = 84;
        private const int ItemRowHeight = 42;

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

            // Load thumbnails in background
            foreach (var item in _items)
            {
                if (item != null && !string.IsNullOrEmpty(item.Id) && !_thumbCache.ContainsKey(item.Id))
                {
                    string vid = item.Id;
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate
                    {
                        LoadThumbnailAsync(vid);
                    });
                }
            }

            Invalidate();
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
            catch { }
        }

        private void UpdateScrollBounds()
        {
            int listStartY = TopHeaderHeight + (_items.Count > 0 ? HeroCardHeight + 10 : 0);
            int totalContentHeight = listStartY + (_items.Count * ItemRowHeight) + 16;
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
                int delta = (e.Delta / 120) * -ItemRowHeight;
                _scrollOffset = Math.Max(0, Math.Min(_maxScroll, _scrollOffset + delta));
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDraggingScroll)
            {
                int trackHeight = Height - 20;
                int thumbHeight = Math.Max(24, (int)((float)Height / (Height + _maxScroll) * trackHeight));
                int availTravel = trackHeight - thumbHeight;
                if (availTravel > 0)
                {
                    int deltaY = e.Y - _dragStartY;
                    float scrollRatio = (float)deltaY / availTravel;
                    _scrollOffset = Math.Max(0, Math.Min(_maxScroll, _dragStartScroll + (int)(scrollRatio * _maxScroll)));
                    Invalidate();
                }
                return;
            }

            bool prevBack = _isHoveringBack;
            bool prevBest = _isHoveringBestMatchPlay;
            int prevIdx = _hoveredIndex;

            _isHoveringBack = _backBtnRect.Contains(e.Location);
            _isHoveringBestMatchPlay = _bestMatchPlayRect.Contains(e.Location);
            _hoveredIndex = -1;

            int listStartY = TopHeaderHeight + (_items.Count > 0 ? HeroCardHeight + 10 : 0) - _scrollOffset;

            if (e.X < Width - 14 && e.Y >= listStartY)
            {
                int row = (e.Y - listStartY) / ItemRowHeight;
                if (row >= 0 && row < _items.Count)
                {
                    _hoveredIndex = row;
                }
            }

            Cursor = (_isHoveringBack || _isHoveringBestMatchPlay || _hoveredIndex >= 0 || _scrollBarThumbRect.Contains(e.Location))
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
                if (_scrollBarThumbRect.Contains(e.Location))
                {
                    _isDraggingScroll = true;
                    _dragStartY = e.Y;
                    _dragStartScroll = _scrollOffset;
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
                    int listStartY = TopHeaderHeight + (_items.Count > 0 ? HeroCardHeight + 10 : 0) - _scrollOffset;
                    int rowY = listStartY + (_hoveredIndex * ItemRowHeight);
                    Rectangle addRect = new Rectangle(Width - 44, rowY + (ItemRowHeight - 24) / 2, 24, 24);

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
            _isDraggingScroll = false;
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
            _backBtnRect = new Rectangle(12, 8, 126, 26);
            using (GraphicsPath bPath = CreateRoundedRectPath(_backBtnRect, 6))
            using (SolidBrush bbBrush = new SolidBrush(_isHoveringBack ? Color.FromArgb(255, 45, 133) : Color.FromArgb(32, 25, 54)))
            using (Pen bbPen = new Pen(_isHoveringBack ? Color.White : Color.FromArgb(80, 255, 45, 133), 1f))
            {
                g.FillPath(bbBrush, bPath);
                g.DrawPath(bbPen, bPath);

                using (Font bFont = new Font("Segoe UI", 8.0f, FontStyle.Bold))
                using (SolidBrush btBrush = new SolidBrush(Color.White))
                {
                    StringFormat bsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("← Quay lại đĩa nhạc", bFont, btBrush, _backBtnRect, bsf);
                }
            }

            // Header Title
            int titleX = _backBtnRect.Right + 12;
            int titleW = Width - titleX - 12;
            using (Font tFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            using (Font qFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            using (SolidBrush tBrush = new SolidBrush(Color.White))
            using (SolidBrush qBrush = new SolidBrush(Color.FromArgb(255, 45, 133)))
            {
                StringFormat tsf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center };
                string prefix = "Kết quả: ";
                g.DrawString(prefix, tFont, tBrush, titleX, 21, tsf);
                float pW = g.MeasureString(prefix, tFont).Width;
                g.DrawString("\"" + _query + "\"", qFont, qBrush, new RectangleF(titleX + pW, 11, titleW - pW, 20), tsf);
            }

            using (Pen divPen = new Pen(Color.FromArgb(38, 30, 60)))
            {
                g.DrawLine(divPen, 10, TopHeaderHeight - 1, Width - 10, TopHeaderHeight - 1);
            }

            // Clip for scrollable area
            Rectangle contentArea = new Rectangle(0, TopHeaderHeight, Width, Height - TopHeaderHeight);
            g.SetClip(contentArea);

            if (_isLoading)
            {
                using (Font lFont = new Font("Segoe UI", 10.0f, FontStyle.Bold))
                using (Font subFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(255, 45, 133)))
                using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(160, 150, 185)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("⚡ Đang tìm kiếm trên YouTube...", lFont, textBrush, new RectangleF(0, Height / 2 - 25, Width, 24), sf);
                    g.DrawString("Vui lòng đợi giây lát", subFont, subBrush, new RectangleF(0, Height / 2 + 2, Width, 20), sf);
                }
                return;
            }

            if (_items.Count == 0)
            {
                using (Font emptyFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                using (SolidBrush emptyBrush = new SolidBrush(Color.FromArgb(160, 150, 185)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("Không tìm thấy kết quả phù hợp cho \"" + _query + "\"", emptyFont, emptyBrush, new RectangleF(0, Height / 2 - 15, Width, 30), sf);
                }
                return;
            }

            int curY = TopHeaderHeight + 6 - _scrollOffset;

            // Best Match Hero Card
            if (_items.Count > 0)
            {
                PlaylistItem hero = _items[0];
                _bestMatchRect = new Rectangle(12, curY, Width - 24, HeroCardHeight);

                using (GraphicsPath hPath = CreateRoundedRectPath(_bestMatchRect, 8))
                using (LinearGradientBrush hBrush = new LinearGradientBrush(_bestMatchRect, Color.FromArgb(42, 28, 70), Color.FromArgb(24, 18, 46), LinearGradientMode.Horizontal))
                using (Pen hPen = new Pen(Color.FromArgb(90, 255, 45, 133), 1f))
                {
                    g.FillPath(hBrush, hPath);
                    g.DrawPath(hPen, hPath);
                }

                // Thumbnail (96x54)
                Rectangle heroThumbRect = new Rectangle(_bestMatchRect.X + 10, _bestMatchRect.Y + (_bestMatchRect.Height - 54) / 2, 96, 54);
                if (_thumbCache.TryGetValue(hero.Id, out Image hThumb) && hThumb != null)
                {
                    g.DrawImage(hThumb, heroThumbRect);
                }
                else
                {
                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(30, 22, 50)))
                    {
                        g.FillRectangle(tb, heroThumbRect);
                    }
                }

                // Text info
                int infoX = heroThumbRect.Right + 12;
                int infoW = _bestMatchRect.Width - (infoX - _bestMatchRect.X) - 90;

                using (Font tagFont = new Font("Segoe UI", 6.5f, FontStyle.Bold))
                using (Font heroTitleFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (Font heroSubFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                using (SolidBrush tagBrush = new SolidBrush(Color.FromArgb(255, 45, 133)))
                using (SolidBrush hTitleBrush = new SolidBrush(Color.White))
                using (SolidBrush hSubBrush = new SolidBrush(Color.FromArgb(160, 150, 185)))
                {
                    StringFormat tsf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    g.DrawString("BÀI HÁT PHÙ HỢP NHẤT", tagFont, tagBrush, infoX, _bestMatchRect.Y + 10);
                    g.DrawString(hero.Title ?? "Bài hát", heroTitleFont, hTitleBrush, new RectangleF(infoX, _bestMatchRect.Y + 24, infoW, 18), tsf);
                    g.DrawString((hero.Author ?? "YouTube") + " • " + hero.FormattedDuration, heroSubFont, hSubBrush, new RectangleF(infoX, _bestMatchRect.Y + 44, infoW, 16), tsf);
                }

                // Big Play button
                _bestMatchPlayRect = new Rectangle(_bestMatchRect.Right - 84, _bestMatchRect.Y + (_bestMatchRect.Height - 30) / 2, 74, 30);
                using (GraphicsPath pPath = CreateRoundedRectPath(_bestMatchPlayRect, 6))
                using (SolidBrush pbBrush = new SolidBrush(_isHoveringBestMatchPlay ? Color.FromArgb(255, 65, 150) : Color.FromArgb(255, 45, 133)))
                {
                    g.FillPath(pbBrush, pPath);
                    using (Font pFont = new Font("Segoe UI", 8.0f, FontStyle.Bold))
                    using (SolidBrush ptBrush = new SolidBrush(Color.White))
                    {
                        StringFormat psf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("▶ Phát ngay", pFont, ptBrush, _bestMatchPlayRect, psf);
                    }
                }

                curY += HeroCardHeight + 10;
            }

            // Results List
            for (int i = 0; i < _items.Count; i++)
            {
                PlaylistItem item = _items[i];
                Rectangle rowRect = new Rectangle(12, curY, Width - 24, ItemRowHeight);

                // Only render visible rows
                if (rowRect.Bottom >= TopHeaderHeight && rowRect.Top <= Height)
                {
                    bool isHovered = (i == _hoveredIndex);

                    if (isHovered)
                    {
                        using (GraphicsPath rPath = CreateRoundedRectPath(rowRect, 6))
                        using (SolidBrush rhBrush = new SolidBrush(Color.FromArgb(40, 28, 62)))
                        using (Pen rhPen = new Pen(Color.FromArgb(100, 255, 45, 133), 1f))
                        {
                            g.FillPath(rhBrush, rPath);
                            g.DrawPath(rhPen, rPath);
                        }
                    }

                    // Index #
                    using (Font numFont = new Font("Segoe UI", 8.0f, FontStyle.Bold))
                    using (SolidBrush numBrush = new SolidBrush(Color.FromArgb(120, 110, 140)))
                    {
                        StringFormat nsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString((i + 1).ToString(), numFont, numBrush, new Rectangle(rowRect.X + 2, rowRect.Y, 20, rowRect.Height), nsf);
                    }

                    // Thumbnail (46x26)
                    Rectangle thumbRect = new Rectangle(rowRect.X + 26, rowRect.Y + (rowRect.Height - 26) / 2, 46, 26);
                    if (_thumbCache.TryGetValue(item.Id, out Image thumb) && thumb != null)
                    {
                        g.DrawImage(thumb, thumbRect);
                    }
                    else
                    {
                        using (SolidBrush tb = new SolidBrush(Color.FromArgb(28, 22, 48)))
                        {
                            g.FillRectangle(tb, thumbRect);
                        }
                    }

                    // Title & Author
                    int textX = thumbRect.Right + 8;
                    int textW = Width - textX - 80;

                    using (Font titleFont = new Font("Segoe UI", 8.25f, FontStyle.Bold))
                    using (Font subFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                    using (SolidBrush titleBrush = new SolidBrush(isHovered ? Color.FromArgb(255, 100, 165) : Color.White))
                    using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(150, 140, 175)))
                    {
                        StringFormat tsf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                        g.DrawString(item.Title ?? "Bài hát", titleFont, titleBrush, new RectangleF(textX, rowRect.Y + 4, textW, 16), tsf);

                        string sub = (item.Author ?? "YouTube") + " • " + item.FormattedDuration;
                        g.DrawString(sub, subFont, subBrush, new RectangleF(textX, rowRect.Y + 20, textW, 14), tsf);
                    }

                    // Quick Play (▶)
                    Rectangle playRect = new Rectangle(Width - 72, rowRect.Y + (rowRect.Height - 22) / 2, 22, 22);
                    using (GraphicsPath btnPath = new GraphicsPath())
                    {
                        btnPath.AddEllipse(playRect);
                        using (SolidBrush pbBrush = new SolidBrush(isHovered ? Color.FromArgb(255, 45, 133) : Color.FromArgb(42, 32, 65)))
                        {
                            g.FillPath(pbBrush, btnPath);
                        }
                        using (Font pFont = new Font("Segoe UI", 8.0f, FontStyle.Bold))
                        using (SolidBrush pTextBrush = new SolidBrush(isHovered ? Color.White : Color.FromArgb(255, 45, 133)))
                        {
                            StringFormat psf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString("▶", pFont, pTextBrush, playRect, psf);
                        }
                    }

                    // Add Button (+)
                    Rectangle addRect = new Rectangle(Width - 44, rowRect.Y + (rowRect.Height - 22) / 2, 22, 22);
                    using (GraphicsPath btnPath = new GraphicsPath())
                    {
                        btnPath.AddEllipse(addRect);
                        using (SolidBrush abBrush = new SolidBrush(Color.FromArgb(32, 25, 52)))
                        {
                            g.FillPath(abBrush, btnPath);
                        }
                        using (Font aFont = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                        using (SolidBrush aTextBrush = new SolidBrush(Color.FromArgb(180, 170, 205)))
                        {
                            StringFormat asf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString("+", aFont, aTextBrush, addRect, asf);
                        }
                    }
                }

                curY += ItemRowHeight;
            }

            g.ResetClip();

            // Scrollbar
            if (_maxScroll > 0)
            {
                int trackX = Width - 7;
                int trackY = TopHeaderHeight + 2;
                int trackHeight = Height - trackY - 4;

                int thumbHeight = Math.Max(24, (int)((float)Height / (Height + _maxScroll) * trackHeight));
                int availTravel = trackHeight - thumbHeight;
                int thumbY = trackY + (int)((float)_scrollOffset / _maxScroll * availTravel);

                _scrollBarThumbRect = new Rectangle(trackX, thumbY, 5, thumbHeight);

                using (GraphicsPath sbPath = CreateRoundedRectPath(_scrollBarThumbRect, 2))
                using (SolidBrush sbBrush = new SolidBrush(_isDraggingScroll ? Color.FromArgb(255, 45, 133) : Color.FromArgb(70, 55, 105)))
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

