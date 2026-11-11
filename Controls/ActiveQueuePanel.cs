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
        private const int HeaderH = 42;
        private const int RowH = 46;
        private const int ScrollBarW = 6;

        private AudioPlayerService _audioPlayer;
        private List<PlaylistItem> _items = new List<PlaylistItem>();
        private int _scrollOffset = 0;
        private int _hoveredRow = -1;
        private bool _isDraggingScroll = false;
        private int _dragGrabOffsetY = 0;
        private bool _isHoveringScroll = false;

        public event EventHandler<PlaylistItem> TrackSelected;

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
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(24, 16, 42);
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
            }
            catch { }

            ClampScroll();
            Invalidate();
        }

        private int ListTop => HeaderH + 4;
        private int ListHeight => Math.Max(10, Height - ListTop - 6);
        private int TotalListContentHeight => _items.Count * RowH;

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
            Cursor = (_hoveredRow >= 0 || _isHoveringScroll || _isDraggingScroll) ? Cursors.Hand : Cursors.Default;
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

            // 1. Card Background & Border
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

            // 2. Header
            using (Font titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(250, 245, 255)))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(160, 145, 185)))
            {
                g.DrawString("Danh sách đang phát", titleFont, titleBrush, new PointF(18, 10));
                SizeF titleSize = g.MeasureString("Danh sách đang phát", titleFont);
                g.DrawString("(phát từ đâu cũng sẽ hiện ở đây)", subFont, subBrush, new PointF(18 + titleSize.Width + 8, 14));
            }

            // Track count pill badge on the right of header
            if (_items.Count > 0)
            {
                string countText = string.Format("{0} bài hát", _items.Count);
                using (Font countFont = new Font("Segoe UI", 8.25f, FontStyle.Bold))
                using (SolidBrush countBg = new SolidBrush(Color.FromArgb(45, 255, 45, 135)))
                using (SolidBrush countTextBrush = new SolidBrush(Color.FromArgb(255, 120, 190)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    SizeF sz = g.MeasureString(countText, countFont);
                    int pillW = (int)sz.Width + 16;
                    int pillH = 22;
                    Rectangle pillRect = new Rectangle(Width - pillW - 18, 10, pillW, pillH);
                    using (GraphicsPath pillPath = RoundedRect(pillRect, pillH / 2))
                    {
                        g.FillPath(countBg, pillPath);
                    }
                    g.DrawString(countText, countFont, countTextBrush, pillRect, sf);
                }
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

            if (_items.Count == 0)
            {
                using (Font emptyFont = new Font("Segoe UI", 9.5f, FontStyle.Italic))
                using (SolidBrush emptyBrush = new SolidBrush(Color.FromArgb(135, 130, 160)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Chưa có bài hát nào trong danh sách phát", emptyFont, emptyBrush, listBounds, sf);
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
                        using (SolidBrush playBg = new SolidBrush(Color.FromArgb(55, 255, 45, 135)))
                        using (Pen playBorder = new Pen(Color.FromArgb(90, 255, 75, 175), 1.2f))
                        {
                            g.FillPath(playBg, rp);
                            g.DrawPath(playBorder, rp);
                        }
                        // Left active indicator pill
                        using (SolidBrush pillBrush = new SolidBrush(Color.FromArgb(255, 45, 135)))
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
                        using (SolidBrush waveBrush = new SolidBrush(Color.FromArgb(255, 70, 160)))
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
                        using (Font idxFont = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                        using (SolidBrush idxBrush = new SolidBrush(Color.FromArgb(130, 125, 155)))
                        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        {
                            g.DrawString((i + 1).ToString(), idxFont, idxBrush, new Rectangle(indexX, rowRect.Y, 22, rowRect.Height), sf);
                        }
                    }

                    // 2. Thumbnail
                    int thumbX = indexX + 28;
                    int thumbSize = 34;
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
                            g.SetClip(tp);
                            g.DrawImage(bmp, thumbRect);
                            g.SetClip(listBounds);
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

                    // 3. Duration on right
                    int durW = 46;
                    int durX = rowRect.Right - durW - 8;
                    using (Font durFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                    using (SolidBrush durBrush = new SolidBrush(Color.FromArgb(145, 140, 170)))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(item.FormattedDuration, durFont, durBrush, new Rectangle(durX, rowRect.Y, durW, rowRect.Height), sf);
                    }

                    // 4. Title and Author
                    int textLeft = thumbRect.Right + 12;
                    int textWidth = Math.Max(40, durX - textLeft - 8);

                    Color titleColor = isPlaying ? Color.FromArgb(255, 75, 175) : Color.FromArgb(245, 245, 255);
                    using (Font tFont = new Font("Segoe UI", 9.25f, isPlaying ? FontStyle.Bold : FontStyle.Regular))
                    using (Font aFont = new Font("Segoe UI", 8.0f, FontStyle.Regular))
                    using (SolidBrush tBrush = new SolidBrush(titleColor))
                    using (SolidBrush aBrush = new SolidBrush(Color.FromArgb(155, 150, 180)))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    {
                        RectangleF tRect = new RectangleF(textLeft, rowRect.Y + 4, textWidth, 18);
                        RectangleF aRect = new RectangleF(textLeft, rowRect.Y + 22, textWidth, 16);
                        g.DrawString(item.Title ?? string.Empty, tFont, tBrush, tRect, sf);
                        g.DrawString(item.Author ?? string.Empty, aFont, aBrush, aRect, sf);
                    }
                }
            }

            g.Clip = oldClip;

            // 4. Custom Scrollbar
            if (TotalListContentHeight > listH)
            {
                int trackX = Width - ScrollBarW - 6;
                int trackY = listTop + 4;
                int trackH = listH - 8;
                float ratio = (float)listH / TotalListContentHeight;
                int thumbH = Math.Max(24, (int)(trackH * ratio));
                float maxScroll = TotalListContentHeight - listH;
                float availableTrackH = Math.Max(1, trackH - thumbH);
                int thumbY = trackY + (int)((_scrollOffset / maxScroll) * availableTrackH);

                Color thumbColor = (_isDraggingScroll || _isHoveringScroll) ? Color.FromArgb(220, 255, 65, 160) : Color.FromArgb(110, 180, 120, 220);
                Rectangle thumbRect = new Rectangle(trackX, thumbY, ScrollBarW, thumbH);
                using (GraphicsPath thumbPath = RoundedRect(thumbRect, ScrollBarW / 2))
                using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
                {
                    g.FillPath(thumbBrush, thumbPath);
                }
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
