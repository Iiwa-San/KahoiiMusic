using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Models;

namespace KahoiiMusic.Controls
{
    public class PlaylistPopup : Form
    {
        private List<PlaylistItem> _items = new List<PlaylistItem>();
        private int _currentIndex = -1;
        private int _hoveredIndex = -1;
        private int _scrollOffset = 0;
        private bool _isHoveringClose = false;
        private bool _isLoading = false;

        private const int HeaderHeight = 36;
        private const int RowHeight = 36;
        private const int ScrollBarWidth = 4;

        public event EventHandler<PlaylistItem> TrackSelected;
        public event EventHandler CloseRequested;

        public bool IsLoading
        {
            get { return _isLoading; }
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
            Size = new Size(360, 250);
            DoubleBuffered = true;
            UpdateRegion();
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
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW (native hardware drop shadow)
                return cp;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
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
                if (CloseRequested != null) CloseRequested(this, EventArgs.Empty);
                return;
            }
            base.OnFormClosing(e);
        }

        public void StartLoading()
        {
            _isLoading = true;
            _items.Clear();
            _currentIndex = -1;
            _scrollOffset = 0;
            Invalidate();
        }

        public void SetPlaylist(List<PlaylistItem> items, int currentIndex = 0)
        {
            _isLoading = false;
            _items = items ?? new List<PlaylistItem>();
            _currentIndex = currentIndex;
            _scrollOffset = 0;
            EnsureCurrentVisible();
            Invalidate();
        }

        public void SetCurrentIndex(int currentIndex)
        {
            _currentIndex = currentIndex;
            EnsureCurrentVisible();
            Invalidate();
        }

        public void Clear()
        {
            _isLoading = false;
            _items.Clear();
            _currentIndex = -1;
            _scrollOffset = 0;
            Invalidate();
        }

        private void EnsureCurrentVisible()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;
            int viewHeight = Height - HeaderHeight - 8;
            int itemTop = _currentIndex * RowHeight;
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
            int totalHeight = _items.Count * RowHeight;
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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            UpdateHover(e.Location);
        }

        private void UpdateHover(Point pt)
        {
            // Close button hover
            Rectangle closeRect = new Rectangle(Width - 32, 7, 22, 22);
            bool oldCloseHover = _isHoveringClose;
            _isHoveringClose = closeRect.Contains(pt);

            // Row hover
            int oldHover = _hoveredIndex;
            if (pt.Y >= HeaderHeight && pt.Y <= Height - 6 && pt.X >= 8 && pt.X <= Width - 8)
            {
                int relY = pt.Y - HeaderHeight + _scrollOffset;
                int idx = relY / RowHeight;
                _hoveredIndex = (idx >= 0 && idx < _items.Count) ? idx : -1;
            }
            else
            {
                _hoveredIndex = -1;
            }

            if (oldHover != _hoveredIndex || oldCloseHover != _isHoveringClose)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            _isHoveringClose = false;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;

            // Close button clicked
            Rectangle closeRect = new Rectangle(Width - 32, 7, 22, 22);
            if (closeRect.Contains(e.Location))
            {
                Visible = false;
                if (CloseRequested != null) CloseRequested(this, EventArgs.Empty);
                return;
            }

            // Song row clicked
            if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count)
            {
                _currentIndex = _hoveredIndex;
                Invalidate();
                if (TrackSelected != null)
                {
                    TrackSelected(this, _items[_currentIndex]);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;

            // 1. Frosted Dark Glass Card Background
            using (GraphicsPath cardPath = GetRoundedRect(new Rectangle(0, 0, w - 1, h - 1), 10))
            {
                // Dark glass body
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(238, 18, 16, 26)))
                {
                    g.FillPath(bgBrush, cardPath);
                }
                // Subtle border
                using (Pen borderPen = new Pen(Color.FromArgb(45, 255, 255, 255), 1))
                {
                    g.DrawPath(borderPen, cardPath);
                }
            }

            // 2. Header
            using (Font titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Font countFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (SolidBrush badgeBrush = new SolidBrush(Color.FromArgb(192, 132, 252)))
            {
                g.DrawString("Danh sách phát", titleFont, textBrush, 14, 9);
                if (_isLoading)
                {
                    g.DrawString("• Đang tải...", countFont, badgeBrush, 114, 10);
                }
                else if (_items.Count > 0)
                {
                    string countText = string.Format("• {0} bài", _items.Count);
                    g.DrawString(countText, countFont, badgeBrush, 114, 10);
                }
            }

            // Close button "✕"
            Color closeColor = _isHoveringClose ? Color.FromArgb(255, 120, 120) : Color.FromArgb(150, 150, 160);
            using (Font closeFont = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (SolidBrush closeBrush = new SolidBrush(closeColor))
            using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("✕", closeFont, closeBrush, new RectangleF(Width - 32, 7, 22, 22), sf);
            }

            // Header separator line
            using (Pen sepPen = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
            {
                g.DrawLine(sepPen, 10, HeaderHeight, w - 10, HeaderHeight);
            }

            // 3. Items List
            if (_isLoading)
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

            if (_items.Count == 0)
            {
                // Empty state
                using (Font emptyFont = new Font("Segoe UI", 9f))
                using (Font hintFont = new Font("Segoe UI", 8f))
                using (SolidBrush b1 = new SolidBrush(Color.FromArgb(180, 180, 190)))
                using (SolidBrush b2 = new SolidBrush(Color.FromArgb(120, 120, 130)))
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Chưa có danh sách phát", emptyFont, b1, new RectangleF(0, HeaderHeight + 40, w, 24), sf);
                    g.DrawString("Dán link playlist YouTube vào ô tìm kiếm", hintFont, b2, new RectangleF(0, HeaderHeight + 66, w, 20), sf);
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
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    int rowY = HeaderHeight + 2 + (i * RowHeight) - _scrollOffset;
                    if (rowY + RowHeight < HeaderHeight || rowY > h) continue;

                    Rectangle rowRect = new Rectangle(8, rowY, w - 16, RowHeight - 2);
                    bool isActive = (i == _currentIndex);
                    bool isHover = (i == _hoveredIndex);

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

                    // Playing icon or Index number
                    if (isActive)
                    {
                        // Draw play triangle indicator
                        Point[] playArrow = new Point[]
                        {
                            new Point(rowRect.X + 10, rowRect.Y + 12),
                            new Point(rowRect.X + 18, rowRect.Y + 17),
                            new Point(rowRect.X + 10, rowRect.Y + 22)
                        };
                        g.FillPolygon(activeIndicatorBrush, playArrow);
                    }
                    else
                    {
                        string num = (i + 1).ToString();
                        g.DrawString(num, numFont, dimText, rowRect.X + 8, rowRect.Y + 9);
                    }

                    // Title
                    PlaylistItem item = _items[i];
                    int titleX = rowRect.X + 26;
                    int titleW = rowRect.Width - 26 - 48; // reserve space for duration
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

            // 4. Custom Slim Scrollbar
            int totalContentHeight = _items.Count * RowHeight;
            if (totalContentHeight > listArea.Height && listArea.Height > 0)
            {
                int scrollTrackH = listArea.Height;
                int thumbH = Math.Max(24, (int)((float)listArea.Height / totalContentHeight * scrollTrackH));
                int maxScroll = totalContentHeight - listArea.Height;
                int thumbY = listArea.Top + (int)((float)_scrollOffset / maxScroll * (scrollTrackH - thumbH));

                Rectangle thumbRect = new Rectangle(w - 7, thumbY, ScrollBarWidth, thumbH);
                using (GraphicsPath tp = GetRoundedRect(thumbRect, 2))
                using (SolidBrush thumbBrush = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                {
                    g.FillPath(thumbBrush, tp);
                }
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
