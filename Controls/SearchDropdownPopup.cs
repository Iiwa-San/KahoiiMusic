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
    public class SearchDropdownPopup : Form
    {
        private List<PlaylistItem> _items = new List<PlaylistItem>();
        private string _currentQuery = string.Empty;
        private int _hoveredIndex = -1;
        private bool _hoveringViewAll = false;
        private Dictionary<string, Image> _thumbCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        private const int HeaderHeight = 26;
        private const int ItemHeight = 46;
        private const int FooterHeight = 30;

        public event EventHandler<PlaylistItem> TrackPlaySelected;
        public event EventHandler<PlaylistItem> TrackAddSelected;
        public event EventHandler<string> ViewAllSelected;

        public SearchDropdownPopup()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(22, 17, 42);
            Size = new Size(420, 100);
            DoubleBuffered = true;
        }

        public void SetResults(string query, List<PlaylistItem> items, int targetWidth = 0)
        {
            _currentQuery = query ?? string.Empty;
            _items = items != null ? items.GetRange(0, Math.Min(items.Count, 5)) : new List<PlaylistItem>();
            _hoveredIndex = -1;
            _hoveringViewAll = false;

            int targetHeight = HeaderHeight + (_items.Count * ItemHeight) + FooterHeight;
            int w = targetWidth > 100 ? targetWidth : Width;
            Size = new Size(w, targetHeight);
            UpdateRegion();

            // Fetch thumbnails in background
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

        private void UpdateRegion()
        {
            try
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    int radius = 12;
                    Rectangle rect = new Rectangle(0, 0, Width, Height);
                    path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
                    path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
                    path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                    path.CloseFigure();
                    Region = new Region(path);
                }
            }
            catch { }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int prevIdx = _hoveredIndex;
            bool prevViewAll = _hoveringViewAll;

            _hoveredIndex = -1;
            _hoveringViewAll = false;

            if (e.Y >= HeaderHeight && e.Y < HeaderHeight + (_items.Count * ItemHeight))
            {
                _hoveredIndex = (e.Y - HeaderHeight) / ItemHeight;
                Cursor = Cursors.Hand;
            }
            else if (e.Y >= Height - FooterHeight)
            {
                _hoveringViewAll = true;
                Cursor = Cursors.Hand;
            }
            else
            {
                Cursor = Cursors.Default;
            }

            if (prevIdx != _hoveredIndex || prevViewAll != _hoveringViewAll)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1 || _hoveringViewAll)
            {
                _hoveredIndex = -1;
                _hoveringViewAll = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count)
                {
                    PlaylistItem item = _items[_hoveredIndex];
                    int rowY = HeaderHeight + (_hoveredIndex * ItemHeight);
                    Rectangle addBtnRect = new Rectangle(Width - 36, rowY + (ItemHeight - 24) / 2, 24, 24);

                    if (addBtnRect.Contains(e.Location))
                    {
                        TrackAddSelected?.Invoke(this, item);
                    }
                    else
                    {
                        TrackPlaySelected?.Invoke(this, item);
                    }
                    return;
                }

                if (_hoveringViewAll)
                {
                    ViewAllSelected?.Invoke(this, _currentQuery);
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(20, 16, 38)))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // Header
            Rectangle headerRect = new Rectangle(12, 0, Width - 24, HeaderHeight);
            using (Font hFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (SolidBrush hBrush = new SolidBrush(Color.FromArgb(150, 140, 175)))
            using (SolidBrush qBrush = new SolidBrush(Color.FromArgb(255, 45, 133)))
            {
                StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center };
                g.DrawString("Gợi ý YouTube:", hFont, hBrush, 12, HeaderHeight / 2 - 6);
                string qText = "\"" + _currentQuery + "\"";
                float labelWidth = g.MeasureString("Gợi ý YouTube: ", hFont).Width;
                g.DrawString(qText, hFont, qBrush, 12 + labelWidth, HeaderHeight / 2 - 6);

                StringFormat rightSf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                g.DrawString("Bấm để phát ngay", hFont, hBrush, Width - 12, HeaderHeight / 2, rightSf);
            }

            using (Pen divPen = new Pen(Color.FromArgb(40, 32, 65)))
            {
                g.DrawLine(divPen, 10, HeaderHeight - 1, Width - 10, HeaderHeight - 1);
            }

            // Items
            for (int i = 0; i < _items.Count; i++)
            {
                PlaylistItem item = _items[i];
                int y = HeaderHeight + (i * ItemHeight);
                Rectangle rowRect = new Rectangle(6, y + 2, Width - 12, ItemHeight - 4);

                bool isHovered = (i == _hoveredIndex);

                if (isHovered)
                {
                    using (GraphicsPath rPath = CreateRoundedRectPath(rowRect, 6))
                    using (SolidBrush hRowBrush = new SolidBrush(Color.FromArgb(45, 25, 60)))
                    using (Pen hRowPen = new Pen(Color.FromArgb(120, 255, 45, 133), 1f))
                    {
                        g.FillPath(hRowBrush, rPath);
                        g.DrawPath(hRowPen, rPath);
                    }
                }

                // Thumbnail (46x26)
                Rectangle thumbRect = new Rectangle(rowRect.X + 6, rowRect.Y + (rowRect.Height - 28) / 2, 46, 28);
                if (_thumbCache.TryGetValue(item.Id, out Image thumb) && thumb != null)
                {
                    g.DrawImage(thumb, thumbRect);
                }
                else
                {
                    using (SolidBrush tbBrush = new SolidBrush(Color.FromArgb(35, 28, 58)))
                    {
                        g.FillRectangle(tbBrush, thumbRect);
                    }
                    using (Font noteFont = new Font("Segoe UI", 9.0f))
                    using (SolidBrush noteBrush = new SolidBrush(Color.FromArgb(160, 150, 190)))
                    {
                        StringFormat nsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("🎵", noteFont, noteBrush, thumbRect, nsf);
                    }
                }

                // Text Content
                int textX = thumbRect.Right + 8;
                int textW = Width - textX - 68;

                using (Font titleFont = new Font("Segoe UI", 8.25f, FontStyle.Bold))
                using (Font subFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                using (SolidBrush titleBrush = new SolidBrush(isHovered ? Color.FromArgb(255, 100, 165) : Color.White))
                using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(150, 140, 175)))
                {
                    StringFormat tsf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    g.DrawString(item.Title ?? "Bài hát", titleFont, titleBrush, new RectangleF(textX, rowRect.Y + 4, textW, 16), tsf);

                    string authorAndDur = (!string.IsNullOrEmpty(item.Author) ? item.Author : "YouTube") + " • " + item.FormattedDuration;
                    g.DrawString(authorAndDur, subFont, subBrush, new RectangleF(textX, rowRect.Y + 20, textW, 14), tsf);
                }

                // Quick Play Button (▶)
                Rectangle playBtnRect = new Rectangle(Width - 64, rowRect.Y + (rowRect.Height - 22) / 2, 22, 22);
                using (GraphicsPath btnPath = new GraphicsPath())
                {
                    btnPath.AddEllipse(playBtnRect);
                    using (SolidBrush pbBrush = new SolidBrush(isHovered ? Color.FromArgb(255, 45, 133) : Color.FromArgb(40, 32, 65)))
                    {
                        g.FillPath(pbBrush, btnPath);
                    }
                    using (Font pFont = new Font("Segoe UI", 8.0f, FontStyle.Bold))
                    using (SolidBrush pTextBrush = new SolidBrush(isHovered ? Color.White : Color.FromArgb(255, 45, 133)))
                    {
                        StringFormat psf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("▶", pFont, pTextBrush, playBtnRect, psf);
                    }
                }

                // Quick Add Button (+)
                Rectangle addBtnRect = new Rectangle(Width - 36, rowRect.Y + (rowRect.Height - 22) / 2, 22, 22);
                using (GraphicsPath btnPath = new GraphicsPath())
                {
                    btnPath.AddEllipse(addBtnRect);
                    using (SolidBrush abBrush = new SolidBrush(Color.FromArgb(32, 26, 52)))
                    {
                        g.FillPath(abBrush, btnPath);
                    }
                    using (Font aFont = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                    using (SolidBrush aTextBrush = new SolidBrush(Color.FromArgb(180, 170, 205)))
                    {
                        StringFormat asf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("+", aFont, aTextBrush, addBtnRect, asf);
                    }
                }
            }

            // Footer
            Rectangle footerRect = new Rectangle(0, Height - FooterHeight, Width, FooterHeight);
            using (SolidBrush fBg = new SolidBrush(_hoveringViewAll ? Color.FromArgb(45, 20, 55) : Color.FromArgb(16, 12, 30)))
            {
                g.FillRectangle(fBg, footerRect);
            }
            using (Pen divPen = new Pen(Color.FromArgb(40, 32, 65)))
            {
                g.DrawLine(divPen, 0, footerRect.Y, Width, footerRect.Y);
            }

            using (Font fFont = new Font("Segoe UI", 8.0f, FontStyle.Bold))
            using (SolidBrush fBrush = new SolidBrush(_hoveringViewAll ? Color.FromArgb(255, 45, 133) : Color.FromArgb(200, 190, 225)))
            {
                StringFormat fsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                string fText = string.Format("🔍 Xem tất cả kết quả cho \"{0}\" (Enter ↵)", _currentQuery);
                g.DrawString(fText, fFont, fBrush, footerRect, fsf);
            }

            // Outer Neon Border
            using (Pen borderPen = new Pen(Color.FromArgb(255, 45, 133), 1.5f))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
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

