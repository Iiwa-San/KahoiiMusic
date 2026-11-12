using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public class CollectionPickerPopup : Control
    {
        private const int WM_ERASEBKGND = 0x0014;
        private const int NewBtnH = 26;
        private const int HeaderH = 34;
        private const int ItemH = 28;
        private const int ScrollBarW = 5;

        private List<UserPlaylist> _playlists = new List<UserPlaylist>();
        private string _currentTrackId = null;
        private int _hoveredRow = -1;
        private bool _isHoveringNewBtn = false;
        private int _scrollOffset = 0;
        private bool _isDraggingScroll = false;
        private int _dragGrabOffsetY = 0;
        private KahoiiMusic.Services.DynamicThemePalette _theme = KahoiiMusic.Services.ColorExtractorService.GetDefaultTheme();

        public void SetTheme(KahoiiMusic.Services.DynamicThemePalette theme)
        {
            if (theme != null)
            {
                _theme = theme;
                Invalidate();
            }
        }

        public event EventHandler<UserPlaylist> PlaylistSelected;
        public event EventHandler CreateNewPlaylistClicked;

        public CollectionPickerPopup()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(218, 180);
            Visible = false;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        public string CurrentTrackId => _currentTrackId;

        public bool PlaylistContainsTrack(UserPlaylist pl)
        {
            if (pl == null || string.IsNullOrWhiteSpace(_currentTrackId)) return false;
            if (pl.Id == "__FAVORITES__")
            {
                return FavoritesService.IsFavorite(_currentTrackId);
            }
            return pl.Items != null && pl.Items.Exists(x => string.Equals(x.Id, _currentTrackId, StringComparison.OrdinalIgnoreCase));
        }

        public void LoadCollections(string currentTrackId)
        {
            _currentTrackId = currentTrackId;
            try
            {
                _playlists = new List<UserPlaylist>();

                // 1. Built-in "Yêu thích"
                var favList = FavoritesService.GetFavorites() ?? new List<PlaylistItem>();
                _playlists.Add(new UserPlaylist("Yêu thích", favList) { Id = "__FAVORITES__" });

                // 2. Custom user-created playlists
                var all = PlaylistCollectionService.Instance.GetPlaylists();
                if (all != null)
                {
                    _playlists.AddRange(all);
                }
            }
            catch
            {
                _playlists = new List<UserPlaylist>();
            }

            int count = Math.Max(1, _playlists.Count);
            int targetH = HeaderH + (count * ItemH) + 6;
            targetH = Math.Max(70, Math.Min(260, targetH));
            Height = targetH;

            _scrollOffset = 0;
            _hoveredRow = -1;
            _isHoveringNewBtn = false;
            Invalidate();
        }

        private int ListTop => HeaderH;
        private int ListHeight => Math.Max(10, Height - ListTop - 4);
        private int TotalContentHeight => Math.Max(1, _playlists.Count * ItemH);

        private Rectangle GetNewBtnRect()
        {
            return new Rectangle(8, 4, Width - 16, NewBtnH);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (TotalContentHeight > ListHeight)
            {
                int scrollStep = ItemH;
                if (e.Delta > 0) _scrollOffset -= scrollStep;
                else _scrollOffset += scrollStep;
                ClampScroll();
                Invalidate();
            }
        }

        private void ClampScroll()
        {
            int maxScroll = Math.Max(0, TotalContentHeight - ListHeight);
            _scrollOffset = Math.Max(0, Math.Min(maxScroll, _scrollOffset));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool prevNewHov = _isHoveringNewBtn;
            int prevRowHov = _hoveredRow;

            _isHoveringNewBtn = GetNewBtnRect().Contains(e.Location);

            if (_isDraggingScroll)
            {
                int trackY = ListTop + 2;
                int trackH = ListHeight - 4;
                float maxScroll = TotalContentHeight - ListHeight;
                float ratio = (float)ListHeight / TotalContentHeight;
                int thumbH = Math.Max(20, (int)(trackH * ratio));
                float availH = Math.Max(1, trackH - thumbH);
                float newY = e.Y - _dragGrabOffsetY - trackY;
                float scrollRatio = Math.Max(0f, Math.Min(1f, newY / availH));
                _scrollOffset = (int)Math.Round(scrollRatio * maxScroll);
                ClampScroll();
                Invalidate();
                return;
            }

            if (e.Y >= ListTop && e.Y <= ListTop + ListHeight)
            {
                int relY = e.Y - ListTop + _scrollOffset;
                int idx = relY / ItemH;
                _hoveredRow = (idx >= 0 && idx < _playlists.Count) ? idx : -1;
            }
            else
            {
                _hoveredRow = -1;
            }

            if (prevNewHov != _isHoveringNewBtn || prevRowHov != _hoveredRow)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredRow = -1;
            _isHoveringNewBtn = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (GetNewBtnRect().Contains(e.Location))
                {
                    CreateNewPlaylistClicked?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // Check scrollbar drag
                bool hasScroll = TotalContentHeight > ListHeight;
                if (hasScroll && e.X >= Width - ScrollBarW - 6 && e.X <= Width - 1 && e.Y >= ListTop)
                {
                    _isDraggingScroll = true;
                    _dragGrabOffsetY = 10;
                    Capture = true;
                    return;
                }

                if (e.Y >= ListTop && e.Y <= ListTop + ListHeight)
                {
                    int relY = e.Y - ListTop + _scrollOffset;
                    int idx = relY / ItemH;
                    if (idx >= 0 && idx < _playlists.Count)
                    {
                        PlaylistSelected?.Invoke(this, _playlists[idx]);
                    }
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = CreateRoundedRect(rect, 8))
            {
                // Dark glass background
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(242, 20, 8, 38)))
                {
                    g.FillPath(bg, path);
                }
                // Neon cyber gradient border
                using (LinearGradientBrush borderGrad = new LinearGradientBrush(
                    rect,
                    _theme.SecondaryAccentColor,
                    _theme.AccentColor,
                    LinearGradientMode.ForwardDiagonal))
                using (Pen borderPen = new Pen(borderGrad, 1.4f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // 1. Button: "+ Tạo bộ sưu tập mới"
            Rectangle newBtnRect = GetNewBtnRect();
            Color nbBg = _isHoveringNewBtn ? Color.FromArgb(85, _theme.AccentColor) : Color.FromArgb(45, _theme.AccentColor);
            Color nbBorder = _isHoveringNewBtn ? ColorExtractorService.Lerp(_theme.AccentColor, Color.White, 0.3f) : _theme.AccentColor;
            using (GraphicsPath nbp = CreateRoundedRect(newBtnRect, 5))
            using (SolidBrush nbb = new SolidBrush(nbBg))
            using (Pen nbpPen = new Pen(nbBorder, 1.1f))
            {
                g.FillPath(nbb, nbp);
                g.DrawPath(nbpPen, nbp);
            }
            using (Font nbf = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (SolidBrush nbt = new SolidBrush(Color.White))
            using (StringFormat nbsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("+ Tạo bộ sưu tập mới", nbf, nbt, newBtnRect, nbsf);
            }

            // Separator line
            using (Pen sepPen = new Pen(Color.FromArgb(35, 255, 255, 255), 1f))
            {
                g.DrawLine(sepPen, 8, HeaderH - 2, Width - 8, HeaderH - 2);
            }

            // 2. Playlists List Area
            int listTop = ListTop;
            int listH = ListHeight;
            Rectangle listBounds = new Rectangle(4, listTop, Width - 8, listH);
            Region oldClip = g.Clip;
            g.SetClip(listBounds);

            bool hasScroll = TotalContentHeight > listH;
            int itemW = hasScroll ? (Width - 8 - ScrollBarW - 4) : (Width - 8);

            if (_playlists.Count == 0)
            {
                using (Font ef = new Font("Segoe UI", 8.25f, FontStyle.Italic))
                using (SolidBrush eb = new SolidBrush(Color.FromArgb(150, 140, 175)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Chưa có bộ sưu tập nào", ef, eb, listBounds, sf);
                }
            }
            else
            {
                using (Font pf = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                using (Font cf = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                using (Font tickF = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (SolidBrush tickB = new SolidBrush(Color.FromArgb(60, 245, 140)))
                using (StringFormat sfNear = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
                using (StringFormat sfFar = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                {
                    for (int i = 0; i < _playlists.Count; i++)
                    {
                        int rowY = listTop + (i * ItemH) - _scrollOffset;
                        if (rowY + ItemH < listTop || rowY > listTop + listH) continue;

                        UserPlaylist pl = _playlists[i];
                        bool isHover = (i == _hoveredRow);
                        bool isSavedHere = PlaylistContainsTrack(pl);

                        Rectangle rowRect = new Rectangle(4, rowY + 1, itemW, ItemH - 2);

                        if (isHover)
                        {
                            using (GraphicsPath rp = CreateRoundedRect(rowRect, 5))
                            using (SolidBrush hb = new SolidBrush(Color.FromArgb(65, _theme.AccentColor)))
                            {
                                g.FillPath(hb, rp);
                            }
                        }

                        // Icon and Name
                        string icon = (pl.Id == "__FAVORITES__") ? "♥ " : "♫ ";
                        Color textColor = isHover ? Color.White : Color.FromArgb(235, 230, 245);
                        Color countColor = isHover ? Color.FromArgb(255, 140, 195) : Color.FromArgb(145, 135, 165);

                        int tickWidth = isSavedHere ? 18 : 0;
                        int countWidth = 44;
                        int rightReserved = countWidth + tickWidth + 6;

                        using (SolidBrush tb = new SolidBrush(textColor))
                        {
                            RectangleF nameRect = new RectangleF(rowRect.X + 6, rowRect.Y, Math.Max(20, rowRect.Width - rightReserved), rowRect.Height);
                            g.DrawString(icon + pl.Name, pf, tb, nameRect, sfNear);
                        }

                        // Green Checkmark
                        if (isSavedHere)
                        {
                            RectangleF tickRect = new RectangleF(rowRect.Right - rightReserved, rowRect.Y, 16, rowRect.Height);
                            g.DrawString("✓", tickF, tickB, tickRect, sfFar);
                        }

                        // Count
                        using (SolidBrush cb = new SolidBrush(countColor))
                        {
                            RectangleF cntRect = new RectangleF(rowRect.Right - countWidth - 2, rowRect.Y, countWidth, rowRect.Height);
                            string cnt = string.Format("{0} bài", pl.Items?.Count ?? 0);
                            g.DrawString(cnt, cf, cb, cntRect, sfFar);
                        }
                    }
                }
            }

            g.Clip = oldClip;

            // Scrollbar
            if (hasScroll)
            {
                int trackX = Width - ScrollBarW - 3;
                int trackY = listTop + 2;
                int trackH = listH - 4;
                float ratio = (float)listH / TotalContentHeight;
                int thumbH = Math.Max(18, (int)(trackH * ratio));
                float maxScroll = TotalContentHeight - listH;
                float availH = Math.Max(1, trackH - thumbH);
                int thumbY = trackY + (int)((_scrollOffset / maxScroll) * availH);

                using (GraphicsPath tp = CreateRoundedRect(new Rectangle(trackX, thumbY, ScrollBarW, thumbH), 2))
                using (SolidBrush tbr = new SolidBrush(Color.FromArgb(120, 255, 80, 160)))
                {
                    g.FillPath(tbr, tp);
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

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
