using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public class CenterDiscoveryPanel : Panel
    {
        private const int PadX = 18;
        private const int Gap = 14;
        private const int ScrollBarW = 6;
        private const int SectionHeaderH = 40;
        private const int SectionGap = 20;

        private DiscoveryFeedResult _feed = new DiscoveryFeedResult();
        private List<PlaylistItem> _recentTracks = new List<PlaylistItem>();
        private bool _hasLoadedOnce = false;
        private bool _isLoading = false;
        private int _scrollOffset = 0;
        private int _totalContentH = 0;

        private struct HitBox
        {
            public Rectangle Rect;
            public Rectangle AddRect;
            public string Kind; // "track", "artist", "refresh", "playall"
            public PlaylistItem Track;
            public ArtistItem Artist;
            public List<PlaylistItem> PlayAllList;
        }

        private readonly List<HitBox> _hitBoxes = new List<HitBox>();
        private int _hoveredHitIdx = -1;
        private bool _hoveredAdd = false;

        private DynamicThemePalette _theme = ColorExtractorService.GetDefaultTheme();

        public event EventHandler<PlaylistItem> TrackPlaySelected;
        public event EventHandler<PlaylistItem> TrackAddSelected;
        public event EventHandler<ArtistItem> ArtistSelected;
        public event EventHandler<List<PlaylistItem>> PlayAllRequested;

        public CenterDiscoveryPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        public void SetTheme(DynamicThemePalette theme)
        {
            if (theme != null)
            {
                _theme = theme;
                Invalidate();
            }
        }

        public void EnsureLoaded()
        {
            RefreshRecentTracks();
            if (_hasLoadedOnce || _isLoading) return;
            _ = LoadDiscoveryAsync(forceRefresh: false);
        }

        public void RefreshRecentTracks()
        {
            try
            {
                var hist = HistoryService.GetHistory();
                _recentTracks = (hist != null && hist.Count > 12)
                    ? hist.GetRange(0, 12)
                    : (hist ?? new List<PlaylistItem>());
            }
            catch
            {
                _recentTracks = new List<PlaylistItem>();
            }
        }

        public async Task LoadDiscoveryAsync(bool forceRefresh)
        {
            if (_isLoading) return;
            _isLoading = true;
            if (forceRefresh)
            {
                _scrollOffset = 0;
                _feed = new DiscoveryFeedResult();
            }
            Invalidate();

            try
            {
                RefreshRecentTracks();
                var feed = await YouTubeSearchService.GetDiscoveryFeedAsync(forceRefresh);
                if (IsDisposed) return;
                _feed = feed ?? new DiscoveryFeedResult();
                _hasLoadedOnce = true;
                _scrollOffset = 0;
            }
            catch
            {
                if (_feed == null) _feed = new DiscoveryFeedResult();
            }
            finally
            {
                _isLoading = false;
                if (!IsDisposed)
                {
                    try { BeginInvoke(new Action(Invalidate)); } catch { }
                }
            }
        }

        private int GetColumnCount()
        {
            int availW = Math.Max(200, Width - PadX * 2 - ScrollBarW - 6);
            if (availW >= 820) return 6;
            if (availW >= 620) return 5;
            if (availW >= 440) return 4;
            return 3;
        }

        private int GetCardWidth(int cols)
        {
            int availW = Math.Max(160, Width - PadX * 2 - ScrollBarW - 6);
            return Math.Max(92, (availW - Gap * (cols - 1)) / cols);
        }

        private void ClampScroll()
        {
            int viewH = Math.Max(1, Height - 16);
            int max = Math.Max(0, _totalContentH - viewH);
            if (_scrollOffset < 0) _scrollOffset = 0;
            if (_scrollOffset > max) _scrollOffset = max;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _scrollOffset -= e.Delta / 2;
            ClampScroll();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newIdx = -1;
            bool newAdd = false;

            for (int i = _hitBoxes.Count - 1; i >= 0; i--)
            {
                if (_hitBoxes[i].Rect.Contains(e.Location))
                {
                    newIdx = i;
                    newAdd = (_hitBoxes[i].Kind == "track" && _hitBoxes[i].AddRect.Contains(e.Location));
                    break;
                }
            }

            if (newIdx != _hoveredHitIdx || newAdd != _hoveredAdd)
            {
                _hoveredHitIdx = newIdx;
                _hoveredAdd = newAdd;
                Cursor = (newIdx >= 0) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredHitIdx != -1 || _hoveredAdd)
            {
                _hoveredHitIdx = -1;
                _hoveredAdd = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;

            for (int i = _hitBoxes.Count - 1; i >= 0; i--)
            {
                var hb = _hitBoxes[i];
                if (!hb.Rect.Contains(e.Location)) continue;

                if (hb.Kind == "refresh")
                {
                    _ = LoadDiscoveryAsync(forceRefresh: true);
                }
                else if (hb.Kind == "playall" && hb.PlayAllList != null && hb.PlayAllList.Count > 0)
                {
                    PlayAllRequested?.Invoke(this, new List<PlaylistItem>(hb.PlayAllList));
                }
                else if (hb.Kind == "track" && hb.Track != null)
                {
                    if (hb.AddRect.Contains(e.Location))
                    {
                        TrackAddSelected?.Invoke(this, hb.Track);
                    }
                    else
                    {
                        TrackPlaySelected?.Invoke(this, hb.Track);
                    }
                }
                else if (hb.Kind == "artist" && hb.Artist != null)
                {
                    ArtistSelected?.Invoke(this, hb.Artist);
                }
                return;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. Card background & border matching WebApp figma-panel
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

            _hitBoxes.Clear();
            Rectangle bodyRect = new Rectangle(2, 8, Width - 4, Math.Max(1, Height - 16));
            Region oldClip = g.Clip;
            g.SetClip(bodyRect);

            if (_isLoading && _feed.Trending.Count == 0)
            {
                using (Font f = new Font("Segoe UI", 11.5f, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(_theme.AccentColor))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Đang tải Khám phá (Trending, Nghệ sĩ, Gợi ý)...", f, b, bodyRect, sf);
                }
                g.Clip = oldClip;
                return;
            }

            int cols = GetColumnCount();
            int cardW = GetCardWidth(cols);
            int trackCardH = cardW + 46;
            int artistCardH = cardW + 46;

            int y = 12 - _scrollOffset;
            int startY = y;

            // Section 1: Nhạc Trending
            y = DrawTrackSection(g, "Nhạc Trending", _feed.Trending, y, cols, cardW, trackCardH, showRefresh: true);

            // Section 2: Mới Phát Hành
            y = DrawTrackSection(g, "Mới Phát Hành", _feed.NewReleases, y, cols, cardW, trackCardH, showRefresh: false);

            // Section 3: Nhạc Sĩ Nổi Bật
            y = DrawArtistSection(g, "Nghệ Sĩ Nổi Bật", _feed.Artists, y, cols, cardW, artistCardH);

            // Section 4: Gần đây
            if (_recentTracks != null && _recentTracks.Count > 0)
            {
                y = DrawTrackSection(g, "Gần đây", _recentTracks, y, cols, cardW, trackCardH, showRefresh: false);
            }

            // Section 5: Có thể bạn sẽ thích
            y = DrawTrackSection(g, "Có thể bạn sẽ thích", _feed.Recommended, y, cols, cardW, trackCardH, showRefresh: false);

            _totalContentH = (y - startY) + 12;
            ClampScroll();

            // Thin scrollbar
            int viewH = bodyRect.Height;
            int maxScroll = Math.Max(0, _totalContentH - viewH);
            if (maxScroll > 0)
            {
                int trackH = viewH - 12;
                int thumbH = Math.Max(28, (int)(trackH * (viewH / (float)(viewH + maxScroll))));
                int thumbY = bodyRect.Top + 6 + (int)((trackH - thumbH) * (_scrollOffset / (float)maxScroll));
                Rectangle scrollRect = new Rectangle(Width - ScrollBarW - 5, thumbY, ScrollBarW, thumbH);
                using (GraphicsPath sp = RoundedRect(scrollRect, 3))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(110, 255, 255, 255)))
                {
                    g.FillPath(sb, sp);
                }
            }

            g.Clip = oldClip;
        }

        private int DrawTrackSection(Graphics g, string title, List<PlaylistItem> items, int y, int cols, int cardW, int cardH, bool showRefresh)
        {
            if (items == null || items.Count == 0) return y;

            // Header
            if (y + SectionHeaderH > 0 && y < Height)
            {
                using (Font titleFont = new Font("Segoe UI", 12.5f, FontStyle.Bold))
                using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(250, 245, 255)))
                {
                    g.DrawString(title, titleFont, titleBrush, new PointF(PadX, y + 6));
                }

                int pillH = 26;
                int pillY = y + 6;
                int playAllW = 88;
                Rectangle playRect = new Rectangle(Width - PadX - ScrollBarW - 6 - playAllW, pillY, playAllW, pillH);
                int playHitIdx = _hitBoxes.Count;
                _hitBoxes.Add(new HitBox { Rect = playRect, Kind = "playall", PlayAllList = items });
                DrawHeaderPill(g, playRect, "Phát tất cả", _hoveredHitIdx == playHitIdx, true);

                if (showRefresh)
                {
                    int refW = 74;
                    Rectangle refRect = new Rectangle(playRect.Left - 8 - refW, pillY, refW, pillH);
                    int refHitIdx = _hitBoxes.Count;
                    _hitBoxes.Add(new HitBox { Rect = refRect, Kind = "refresh" });
                    DrawHeaderPill(g, refRect, "Làm mới", _hoveredHitIdx == refHitIdx, false);
                }
            }

            y += SectionHeaderH;
            int maxShow = Math.Min(items.Count, cols * 2);

            using (Font itemTitleFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            using (Font itemAuthorFont = new Font("Segoe UI", 8.0f, FontStyle.Regular))
            using (StringFormat sfTrim = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                for (int i = 0; i < maxShow; i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    int x = PadX + col * (cardW + Gap);
                    int cy = y + row * (cardH + Gap);

                    Rectangle fullCard = new Rectangle(x, cy, cardW, cardH);
                    Rectangle addRect = new Rectangle(x + cardW - 30, cy + cardW - 30, 24, 24);
                    int hitIdx = _hitBoxes.Count;
                    _hitBoxes.Add(new HitBox { Rect = fullCard, AddRect = addRect, Kind = "track", Track = items[i] });

                    if (fullCard.Bottom < 0 || fullCard.Top > Height) continue;

                    PlaylistItem item = items[i];
                    bool isHovered = (_hoveredHitIdx == hitIdx);

                    Rectangle thumbRect = new Rectangle(x, cy, cardW, cardW);
                    using (GraphicsPath tp = RoundedRect(thumbRect, 12))
                    {
                        using (SolidBrush ph = new SolidBrush(Color.FromArgb(200, 34, 16, 64)))
                        {
                            g.FillPath(ph, tp);
                        }

                        Bitmap thumb = ThumbnailManager.GetCardThumbnail("trk_" + item.Id);
                        if (thumb != null)
                        {
                            ThumbnailManager.DrawRoundedImage(g, thumb, thumbRect, tp);
                        }
                        else
                        {
                            ThumbnailManager.QueueLoadCard("trk_" + item.Id, item.Id, true, () =>
                            {
                                if (!IsDisposed)
                                {
                                    try { BeginInvoke(new Action(Invalidate)); } catch { }
                                }
                            });
                        }

                        if (isHovered)
                        {
                            using (SolidBrush ov = new SolidBrush(Color.FromArgb(115, 10, 4, 22)))
                            {
                                g.FillPath(ov, tp);
                            }
                            int playR = 34;
                            Rectangle playCircle = new Rectangle(x + (cardW - playR) / 2, cy + (cardW - playR) / 2, playR, playR);
                            using (SolidBrush pb = new SolidBrush(_theme.AccentColor))
                            {
                                g.FillEllipse(pb, playCircle);
                            }
                            PointF[] tri = new PointF[]
                            {
                                new PointF(playCircle.X + 13, playCircle.Y + 9),
                                new PointF(playCircle.X + 13, playCircle.Y + 25),
                                new PointF(playCircle.X + 25, playCircle.Y + 17)
                            };
                            using (SolidBrush wb = new SolidBrush(Color.White))
                            {
                                g.FillPolygon(wb, tri);
                            }

                            using (SolidBrush ab = new SolidBrush(_hoveredAdd ? _theme.AccentColor : Color.FromArgb(190, 26, 12, 50)))
                            {
                                g.FillEllipse(ab, addRect);
                            }
                            using (Pen ap = new Pen(Color.FromArgb(180, 255, 255, 255), 1f))
                            {
                                g.DrawEllipse(ap, addRect);
                            }
                            using (Font pf = new Font("Segoe UI", 10f, FontStyle.Bold))
                            using (StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            using (SolidBrush wb = new SolidBrush(Color.White))
                            {
                                g.DrawString("+", pf, wb, addRect, sfCenter);
                            }
                        }

                        using (Pen borderPen = new Pen(isHovered ? _theme.AccentColor : Color.FromArgb(45, 255, 255, 255), isHovered ? 1.5f : 1f))
                        {
                            g.DrawPath(borderPen, tp);
                        }
                    }

                    Rectangle titleRect = new Rectangle(x + 2, cy + cardW + 5, cardW - 4, 18);
                    using (SolidBrush tb = new SolidBrush(isHovered ? Color.White : Color.FromArgb(242, 238, 255)))
                    {
                        g.DrawString(item.Title ?? "Bài hát", itemTitleFont, tb, titleRect, sfTrim);
                    }

                    Rectangle authorRect = new Rectangle(x + 2, cy + cardW + 23, cardW - 4, 16);
                    using (SolidBrush ab = new SolidBrush(Color.FromArgb(175, 165, 205)))
                    {
                        g.DrawString(item.Author ?? string.Empty, itemAuthorFont, ab, authorRect, sfTrim);
                    }
                }
            }

            int rows = (int)Math.Ceiling(maxShow / (double)cols);
            return y + rows * cardH + Math.Max(0, rows - 1) * Gap + SectionGap;
        }

        private int DrawArtistSection(Graphics g, string title, List<ArtistItem> artists, int y, int cols, int cardW, int cardH)
        {
            if (artists == null || artists.Count == 0) return y;

            if (y + SectionHeaderH > 0 && y < Height)
            {
                using (Font titleFont = new Font("Segoe UI", 12.5f, FontStyle.Bold))
                using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(250, 245, 255)))
                {
                    g.DrawString(title, titleFont, titleBrush, new PointF(PadX, y + 6));
                }
            }

            y += SectionHeaderH;
            int maxShow = Math.Min(artists.Count, cols);

            using (Font nameFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 7.8f, FontStyle.Regular))
            using (StringFormat sfCenterTrim = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                for (int i = 0; i < maxShow; i++)
                {
                    int x = PadX + i * (cardW + Gap);
                    Rectangle fullCard = new Rectangle(x, y, cardW, cardH);
                    int hitIdx = _hitBoxes.Count;
                    _hitBoxes.Add(new HitBox { Rect = fullCard, Kind = "artist", Artist = artists[i] });

                    if (fullCard.Bottom < 0 || fullCard.Top > Height) continue;

                    ArtistItem artist = artists[i];
                    bool isHovered = (_hoveredHitIdx == hitIdx);

                    Rectangle avatarRect = new Rectangle(x + 4, y + 2, cardW - 8, cardW - 8);
                    using (GraphicsPath ap = new GraphicsPath())
                    {
                        ap.AddEllipse(avatarRect);
                        using (SolidBrush ph = new SolidBrush(Color.FromArgb(200, 38, 18, 72)))
                        {
                            g.FillPath(ph, ap);
                        }

                        string cacheKey = "artist_" + artist.Id;
                        Bitmap bmp = ThumbnailManager.GetCardThumbnail(cacheKey);
                        if (bmp != null)
                        {
                            ThumbnailManager.DrawRoundedImage(g, bmp, avatarRect, ap);
                        }
                        else if (!string.IsNullOrEmpty(artist.ThumbnailUrl))
                        {
                            ThumbnailManager.QueueLoadCard(cacheKey, artist.ThumbnailUrl, false, () =>
                            {
                                if (!IsDisposed)
                                {
                                    try { BeginInvoke(new Action(Invalidate)); } catch { }
                                }
                            });
                        }

                        using (Pen borderPen = new Pen(isHovered ? _theme.AccentColor : Color.FromArgb(80, _theme.SecondaryAccentColor), isHovered ? 2f : 1.2f))
                        {
                            g.DrawPath(borderPen, ap);
                        }
                    }

                    Rectangle nameRect = new Rectangle(x, y + cardW, cardW, 18);
                    using (SolidBrush nb = new SolidBrush(isHovered ? Color.White : Color.FromArgb(245, 240, 255)))
                    {
                        g.DrawString(artist.Name ?? "Nghệ sĩ", nameFont, nb, nameRect, sfCenterTrim);
                    }

                    if (!string.IsNullOrWhiteSpace(artist.Subscribers))
                    {
                        Rectangle subRect = new Rectangle(x, y + cardW + 18, cardW, 16);
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(175, 165, 205)))
                        {
                            g.DrawString(artist.Subscribers, subFont, sb, subRect, sfCenterTrim);
                        }
                    }
                }
            }

            return y + cardH + SectionGap;
        }

        private void DrawHeaderPill(Graphics g, Rectangle rect, string text, bool hovered, bool primary)
        {
            Color bg = primary
                ? (hovered ? _theme.AccentColor : Color.FromArgb(190, _theme.AccentColor))
                : (hovered ? Color.FromArgb(180, 48, 28, 82) : Color.FromArgb(130, 32, 18, 60));
            Color border = primary ? _theme.AccentColor : Color.FromArgb(100, _theme.SecondaryAccentColor);

            using (GraphicsPath p = RoundedRect(rect, 13))
            using (SolidBrush b = new SolidBrush(bg))
            using (Pen pen = new Pen(border, 1f))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }

            using (Font f = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.White))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(text, f, tb, rect, sf);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
