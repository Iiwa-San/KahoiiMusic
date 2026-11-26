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

        private bool _isMaximizedMode = false;
        public bool IsMaximizedMode
        {
            get => _isMaximizedMode;
            set
            {
                if (_isMaximizedMode != value)
                {
                    _isMaximizedMode = value;
                    UpdateScrollBounds();
                    Invalidate();
                }
            }
        }

        private bool IsLarge => _isMaximizedMode || Width >= 800;
        private int ActualTopHeaderHeight => IsLarge ? 54 : 44;
        private int ActualHeroCardHeight => IsLarge ? 120 : 84;
        private int ActualItemRowHeight => IsLarge ? 64 : 42;

        public event EventHandler<PlaylistItem> TrackPlaySelected;
        public event EventHandler<PlaylistItem> TrackAddSelected;
        public event EventHandler<List<PlaylistItem>> PlayAllRequested;
        public event EventHandler<List<PlaylistItem>> ShuffleAllRequested;
        public event EventHandler<KahoiiMusic.Services.ArtistItem> ArtistSelected;
        public event EventHandler<KahoiiMusic.Services.PlaylistSearchItem> PlaylistSelected;
        public event EventHandler CloseRequested;

        private bool _isArtistMode = false;
        private KahoiiMusic.Services.ArtistItem _profileArtist = null;
        private Rectangle _artistShuffleRect = Rectangle.Empty;
        private bool _isHoveringArtistShuffle = false;
        private readonly List<(Rectangle CardRect, Rectangle AddRect, PlaylistItem Track)> _trackCardRects = new List<(Rectangle, Rectangle, PlaylistItem)>();
        private int _hoveredTrackCardIdx = -1;
        private bool _hoveredTrackCardAdd = false;

        private List<KahoiiMusic.Services.ArtistItem> _artists = new List<KahoiiMusic.Services.ArtistItem>();
        private List<KahoiiMusic.Services.PlaylistSearchItem> _playlists = new List<KahoiiMusic.Services.PlaylistSearchItem>();
        private int _activeSearchTab = 0; // 0: Tất cả, 1: Nhạc, 2: Nhạc sĩ, 3: Playlist
        private readonly Rectangle[] _searchTabRects = new Rectangle[4];
        private int _hoveredSearchTab = -1;
        private readonly List<(Rectangle Rect, KahoiiMusic.Services.ArtistItem Artist)> _artistCardRects = new List<(Rectangle, KahoiiMusic.Services.ArtistItem)>();
        private readonly List<(Rectangle Rect, KahoiiMusic.Services.PlaylistSearchItem Playlist)> _playlistCardRects = new List<(Rectangle, KahoiiMusic.Services.PlaylistSearchItem)>();
        private int _hoveredArtistIdx = -1;
        private int _hoveredPlaylistIdx = -1;
        private int _tracksStartY = 0;
        private int TabBarHeight => IsLarge ? 42 : 0;
        private KahoiiMusic.Services.DynamicThemePalette _customTheme = null;
        private KahoiiMusic.Services.DynamicThemePalette _theme => _customTheme ?? KahoiiMusic.Services.ColorExtractorService.CurrentTheme ?? KahoiiMusic.Services.ColorExtractorService.GetDefaultTheme();

        public void SetTheme(KahoiiMusic.Services.DynamicThemePalette theme)
        {
            _customTheme = theme;
            Invalidate();
        }

        public SearchResultsView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            BackColor = Color.Transparent;
            DoubleBuffered = true;
        }

        private bool _isLoading = false;
        private string _nextContinuationToken = null;
        private bool _isLoadingMore = false;
        private System.Windows.Forms.Timer _loadingAnimTimer;
        private int _animFrame = 0;

        private void StartLoadingAnim()
        {
            if (_loadingAnimTimer == null)
            {
                _loadingAnimTimer = new System.Windows.Forms.Timer();
                _loadingAnimTimer.Interval = 70;
                _loadingAnimTimer.Tick += (s, e) =>
                {
                    if (!_isLoading && !_isLoadingMore)
                    {
                        StopLoadingAnim();
                        return;
                    }
                    _animFrame = (_animFrame + 1) % 120;
                    Invalidate();
                };
            }
            _animFrame = 0;
            _loadingAnimTimer.Start();
        }

        private void StopLoadingAnim()
        {
            try
            {
                _loadingAnimTimer?.Stop();
            }
            catch { }
        }

        private void DrawFilterTabBar(Graphics g, int tabY)
        {
            int tabH = 28;
            int tx = 16;
            string[] tabLabels = _isArtistMode
                ? new[] { "Tất cả", $"Nhạc Single ({_items.Count})", "", $"Danh sách phát ({_playlists?.Count ?? 0})" }
                : new[] { "Tất cả", $"Nhạc ({_items.Count})", $"Nhạc sĩ ({_artists?.Count ?? 0})", $"Playlist ({_playlists?.Count ?? 0})" };

            using (Font tabFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (StringFormat tSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                for (int t = 0; t < 4; t++)
                {
                    if (string.IsNullOrEmpty(tabLabels[t]))
                    {
                        _searchTabRects[t] = Rectangle.Empty;
                        continue;
                    }
                    int tw = (int)Math.Ceiling(g.MeasureString(tabLabels[t], tabFont).Width) + 24;
                    _searchTabRects[t] = new Rectangle(tx, tabY, tw, tabH);
                    bool isAct = (_activeSearchTab == t);
                    bool isHov = (_hoveredSearchTab == t);
                    Color bg = isAct ? _theme.AccentColor : (isHov ? Color.FromArgb(60, 42, 92) : Color.FromArgb(30, 22, 52));
                    Color brd = isAct ? Color.White : Color.FromArgb(65, _theme.SecondaryAccentColor);

                    using (GraphicsPath tp = CreateRoundedRectPath(_searchTabRects[t], 14))
                    using (SolidBrush tb = new SolidBrush(bg))
                    using (Pen tpen = new Pen(brd, 1f))
                    {
                        g.FillPath(tb, tp);
                        g.DrawPath(tpen, tp);
                    }
                    using (SolidBrush txtB = new SolidBrush(isAct || isHov ? Color.White : Color.FromArgb(190, 180, 215)))
                    {
                        g.DrawString(tabLabels[t], tabFont, txtB, _searchTabRects[t], tSf);
                    }
                    tx += tw + 8;
                }
            }
        }

        private int DrawArtistsSection(Graphics g, string title, List<KahoiiMusic.Services.ArtistItem> artists, int y, int padX, bool singleRow)
        {
            if (artists == null || artists.Count == 0) return y;
            using (Font secFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush secBrush = new SolidBrush(Color.White))
            {
                g.DrawString(title, secFont, secBrush, padX, y);
            }
            y += 26;

            int gap = 12;
            int cols = Math.Max(2, (Width - padX * 2 - 16) / 132);
            int cardW = Math.Min(136, Math.Max(92, (Width - padX * 2 - 16 - gap * (cols - 1)) / cols));
            int cardH = cardW + 36;
            int maxShow = singleRow ? Math.Min(artists.Count, cols) : artists.Count;

            using (Font nameFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                for (int i = 0; i < maxShow; i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    int x = padX + col * (cardW + gap);
                    int cy = y + row * (cardH + gap);
                    Rectangle cardRect = new Rectangle(x, cy, cardW, cardH);
                    int hitIdx = _artistCardRects.Count;
                    _artistCardRects.Add((cardRect, artists[i]));

                    if (cardRect.Bottom < ActualTopHeaderHeight || cardRect.Top > Height) continue;

                    bool isHov = (_hoveredArtistIdx == hitIdx);
                    Rectangle avRect = new Rectangle(x + 8, cy, cardW - 16, cardW - 16);
                    using (GraphicsPath ap = new GraphicsPath())
                    {
                        ap.AddEllipse(avRect);
                        using (SolidBrush ph = new SolidBrush(Color.FromArgb(40, 26, 68))) g.FillPath(ph, ap);
                        string key = "artist_" + artists[i].Id;
                        Bitmap bmp = KahoiiMusic.Services.ThumbnailManager.GetCardThumbnail(key);
                        if (bmp != null)
                        {
                            KahoiiMusic.Services.ThumbnailManager.DrawRoundedImage(g, bmp, avRect, ap);
                        }
                        else if (!string.IsNullOrEmpty(artists[i].ThumbnailUrl))
                        {
                            KahoiiMusic.Services.ThumbnailManager.QueueLoadCard(key, artists[i].ThumbnailUrl, false, () =>
                            {
                                if (!IsDisposed) try { BeginInvoke(new Action(Invalidate)); } catch { }
                            });
                        }
                        using (Pen p = new Pen(isHov ? _theme.AccentColor : Color.FromArgb(70, _theme.SecondaryAccentColor), isHov ? 2f : 1.2f))
                            g.DrawPath(p, ap);
                    }

                    using (SolidBrush nb = new SolidBrush(isHov ? Color.White : Color.FromArgb(240, 236, 255)))
                        g.DrawString(artists[i].Name ?? "", nameFont, nb, new RectangleF(x, avRect.Bottom + 4, cardW, 18), sf);
                    if (!string.IsNullOrEmpty(artists[i].Subscribers))
                    {
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(165, 155, 195)))
                            g.DrawString(artists[i].Subscribers, subFont, sb, new RectangleF(x, avRect.Bottom + 20, cardW, 15), sf);
                    }
                }
            }

            int rows = (int)Math.Ceiling(maxShow / (double)cols);
            return y + rows * cardH + Math.Max(0, rows - 1) * gap + 12;
        }

        private int DrawPlaylistsSection(Graphics g, string title, List<KahoiiMusic.Services.PlaylistSearchItem> playlists, int y, int padX, bool singleRow)
        {
            if (playlists == null || playlists.Count == 0) return y;
            using (Font secFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush secBrush = new SolidBrush(Color.White))
            {
                g.DrawString(title, secFont, secBrush, padX, y);
            }
            y += 26;

            int gap = 12;
            int cols = Math.Max(2, (Width - padX * 2 - 16) / 140);
            int cardW = Math.Min(144, Math.Max(96, (Width - padX * 2 - 16 - gap * (cols - 1)) / cols));
            int cardH = cardW + 38;
            int maxShow = singleRow ? Math.Min(playlists.Count, cols) : playlists.Count;

            using (Font nameFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                for (int i = 0; i < maxShow; i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    int x = padX + col * (cardW + gap);
                    int cy = y + row * (cardH + gap);
                    Rectangle cardRect = new Rectangle(x, cy, cardW, cardH);
                    int hitIdx = _playlistCardRects.Count;
                    _playlistCardRects.Add((cardRect, playlists[i]));

                    if (cardRect.Bottom < ActualTopHeaderHeight || cardRect.Top > Height) continue;

                    bool isHov = (_hoveredPlaylistIdx == hitIdx);
                    Rectangle thumbRect = new Rectangle(x, cy, cardW, cardW);
                    using (GraphicsPath tp = CreateRoundedRectPath(thumbRect, 10))
                    {
                        using (SolidBrush ph = new SolidBrush(Color.FromArgb(40, 26, 68))) g.FillPath(ph, tp);
                        string key = "pl_" + playlists[i].Id;
                        Bitmap bmp = KahoiiMusic.Services.ThumbnailManager.GetCardThumbnail(key);
                        if (bmp != null)
                        {
                            KahoiiMusic.Services.ThumbnailManager.DrawRoundedImage(g, bmp, thumbRect, tp);
                        }
                        else if (!string.IsNullOrEmpty(playlists[i].ThumbnailUrl))
                        {
                            KahoiiMusic.Services.ThumbnailManager.QueueLoadCard(key, playlists[i].ThumbnailUrl, false, () =>
                            {
                                if (!IsDisposed) try { BeginInvoke(new Action(Invalidate)); } catch { }
                            });
                        }
                        using (Pen p = new Pen(isHov ? _theme.AccentColor : Color.FromArgb(55, 255, 255, 255), isHov ? 1.5f : 1f))
                            g.DrawPath(p, tp);
                    }

                    using (SolidBrush nb = new SolidBrush(isHov ? Color.White : Color.FromArgb(240, 236, 255)))
                        g.DrawString(playlists[i].Title ?? "", nameFont, nb, new RectangleF(x + 2, thumbRect.Bottom + 4, cardW - 4, 18), sf);
                    if (!string.IsNullOrEmpty(playlists[i].TrackCountText))
                    {
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(165, 155, 195)))
                            g.DrawString(playlists[i].TrackCountText, subFont, sb, new RectangleF(x + 2, thumbRect.Bottom + 21, cardW - 4, 15), sf);
                    }
                }
            }

            int rows = (int)Math.Ceiling(maxShow / (double)cols);
            return y + rows * cardH + Math.Max(0, rows - 1) * gap + 12;
        }

        private int DrawTrackCardsGrid(Graphics g, string title, List<PlaylistItem> tracks, int y, int padX)
        {
            if (tracks == null || tracks.Count == 0) return y;
            using (Font secFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush secBrush = new SolidBrush(Color.White))
            {
                g.DrawString(title, secFont, secBrush, padX, y);
            }
            y += 28;

            int gap = 14;
            int cols = Math.Max(3, (Width - padX * 2 - 18) / 145);
            int cardW = Math.Max(96, (Width - padX * 2 - 18 - gap * (cols - 1)) / cols);
            int cardH = cardW + 44;

            using (Font tFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (Font aFont = new Font("Segoe UI", 7.8f, FontStyle.Regular))
            using (Font durFont = new Font("Segoe UI", 7.3f, FontStyle.Bold))
            using (StringFormat sfTrim = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    int x = padX + col * (cardW + gap);
                    int cy = y + row * (cardH + gap);

                    Rectangle cardRect = new Rectangle(x, cy, cardW, cardH);
                    Rectangle addRect = new Rectangle(x + cardW - 30, cy + cardW - 30, 24, 24);
                    int hitIdx = _trackCardRects.Count;
                    _trackCardRects.Add((cardRect, addRect, tracks[i]));

                    if (cardRect.Bottom < ActualTopHeaderHeight || cardRect.Top > Height) continue;

                    PlaylistItem item = tracks[i];
                    bool isHov = (_hoveredTrackCardIdx == hitIdx);
                    Rectangle thumbRect = new Rectangle(x, cy, cardW, cardW);

                    using (GraphicsPath tp = CreateRoundedRectPath(thumbRect, 14))
                    {
                        using (SolidBrush ph = new SolidBrush(Color.FromArgb(40, 26, 68))) g.FillPath(ph, tp);
                        string key = "trk_" + item.Id;
                        Bitmap bmp = KahoiiMusic.Services.ThumbnailManager.GetCardThumbnail(key);
                        if (bmp != null)
                        {
                            KahoiiMusic.Services.ThumbnailManager.DrawRoundedImage(g, bmp, thumbRect, tp);
                        }
                        else if (!string.IsNullOrEmpty(item.Id))
                        {
                            KahoiiMusic.Services.ThumbnailManager.QueueLoadCard(key, item.Id, true, () =>
                            {
                                if (!IsDisposed) try { BeginInvoke(new Action(Invalidate)); } catch { }
                            });
                        }

                        if (!isHov && item.DurationSeconds > 0)
                        {
                            string dur = "♬ " + item.FormattedDuration;
                            int dw = (int)Math.Ceiling(g.MeasureString(dur, durFont).Width) + 10;
                            Rectangle durRect = new Rectangle(thumbRect.Right - dw - 6, thumbRect.Bottom - 22, dw, 16);
                            using (GraphicsPath dp = CreateRoundedRectPath(durRect, 6))
                            using (SolidBrush db = new SolidBrush(Color.FromArgb(185, 10, 6, 20)))
                            {
                                g.FillPath(db, dp);
                            }
                            using (SolidBrush dtb = new SolidBrush(Color.White))
                            {
                                g.DrawString(dur, durFont, dtb, durRect, sfCenter);
                            }
                        }

                        if (isHov)
                        {
                            using (SolidBrush ov = new SolidBrush(Color.FromArgb(115, 10, 4, 22))) g.FillPath(ov, tp);
                            int playR = 34;
                            Rectangle playCircle = new Rectangle(x + (cardW - playR) / 2, cy + (cardW - playR) / 2, playR, playR);
                            using (SolidBrush pb = new SolidBrush(_theme.AccentColor)) g.FillEllipse(pb, playCircle);
                            PointF[] tri = new PointF[]
                            {
                                new PointF(playCircle.X + 13, playCircle.Y + 9),
                                new PointF(playCircle.X + 13, playCircle.Y + 25),
                                new PointF(playCircle.X + 25, playCircle.Y + 17)
                            };
                            using (SolidBrush wb = new SolidBrush(Color.White)) g.FillPolygon(wb, tri);

                            using (SolidBrush ab = new SolidBrush(_hoveredTrackCardAdd ? _theme.AccentColor : Color.FromArgb(190, 26, 12, 50)))
                                g.FillEllipse(ab, addRect);
                            using (Pen ap = new Pen(Color.FromArgb(180, 255, 255, 255), 1f))
                                g.DrawEllipse(ap, addRect);
                            using (Font pf = new Font("Segoe UI", 10f, FontStyle.Bold))
                            using (SolidBrush wb = new SolidBrush(Color.White))
                                g.DrawString("+", pf, wb, addRect, sfCenter);
                        }

                        using (Pen p = new Pen(isHov ? _theme.AccentColor : Color.FromArgb(45, 255, 255, 255), isHov ? 1.5f : 1f))
                            g.DrawPath(p, tp);
                    }

                    using (SolidBrush tb = new SolidBrush(isHov ? Color.White : Color.FromArgb(242, 238, 255)))
                        g.DrawString(item.Title ?? "Bài hát", tFont, tb, new RectangleF(x + 2, thumbRect.Bottom + 5, cardW - 4, 18), sfTrim);
                    using (SolidBrush ab = new SolidBrush(Color.FromArgb(175, 165, 205)))
                        g.DrawString(item.Author ?? string.Empty, aFont, ab, new RectangleF(x + 2, thumbRect.Bottom + 23, cardW - 4, 16), sfTrim);
                }
            }

            int rows = (int)Math.Ceiling(tracks.Count / (double)cols);
            return y + rows * cardH + Math.Max(0, rows - 1) * gap + 16;
        }

        private void DrawBouncingText(Graphics g, string text, Font font, Brush brush, float centerX, float centerY, float dotSize = 5f, float dotGap = 5f)
        {
            SizeF textSz = g.MeasureString(text, font);
            float totalDotsW = (3 * dotSize) + (2 * dotGap);
            float spacingBetween = 4f;
            float totalW = textSz.Width + spacingBetween + totalDotsW;

            float startX = centerX - (totalW / 2f);
            float textY = centerY - (textSz.Height / 2f);

            // Draw text
            g.DrawString(text, font, brush, startX, textY);

            // Draw 3 bouncing dots
            float dotsStartX = startX + textSz.Width + spacingBetween;
            float baseY = centerY + (textSz.Height * 0.15f);

            for (int i = 0; i < 3; i++)
            {
                double phase = (_animFrame * 0.35) - (i * 0.7);
                double sinVal = Math.Sin(phase);
                float jump = sinVal > 0 ? (float)(sinVal * 6.0) : 0f;
                float dotY = baseY - jump;
                float dotX = dotsStartX + (i * (dotSize + dotGap));

                g.FillEllipse(brush, dotX, dotY - (dotSize / 2f), dotSize, dotSize);
            }
        }

        public string CurrentQuery => _query;
        public event EventHandler<string> LoadMoreRequested;

        public void ShowLoading(string query)
        {
            _query = query ?? string.Empty;
            _items = new List<PlaylistItem>();
            _isLoading = true;
            _isLoadingMore = false;
            _nextContinuationToken = null;
            _scrollOffset = 0;
            _hoveredIndex = -1;
            StartLoadingAnim();
            Invalidate();
        }

        public void ShowArtistLoading(KahoiiMusic.Services.ArtistItem artist)
        {
            _isArtistMode = true;
            _profileArtist = artist;
            _artists = new List<KahoiiMusic.Services.ArtistItem>();
            _playlists = new List<KahoiiMusic.Services.PlaylistSearchItem>();
            _activeSearchTab = 0;
            ShowLoading(artist?.Name ?? "Nghệ sĩ");
        }

        public void SetArtistProfile(KahoiiMusic.Services.ArtistProfileResult profile)
        {
            _isArtistMode = true;
            _profileArtist = profile?.Artist ?? _profileArtist;
            _artists = new List<KahoiiMusic.Services.ArtistItem>();
            _playlists = profile?.Playlists ?? new List<KahoiiMusic.Services.PlaylistSearchItem>();
            _activeSearchTab = 0;
            SetResults(_profileArtist?.Name ?? "Nghệ sĩ", profile?.Tracks, null);
        }

        public void SetStructuredResults(string query, KahoiiMusic.Services.StructuredSearchResult result)
        {
            _isArtistMode = false;
            _profileArtist = null;
            _artists = result?.Artists ?? new List<KahoiiMusic.Services.ArtistItem>();
            _playlists = result?.Playlists ?? new List<KahoiiMusic.Services.PlaylistSearchItem>();
            _activeSearchTab = 0;
            SetResults(query, result?.Tracks, result?.NextContinuationToken);
        }

        public void SetResults(string query, List<PlaylistItem> items, string nextContinuationToken = null)
        {
            _isLoading = false;
            _isLoadingMore = false;
            StopLoadingAnim();
            _query = query ?? string.Empty;
            _items = items ?? new List<PlaylistItem>();
            _nextContinuationToken = nextContinuationToken;
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

        public void AppendResults(List<PlaylistItem> newItems, string nextContinuationToken = null)
        {
            _isLoadingMore = false;
            StopLoadingAnim();
            _nextContinuationToken = nextContinuationToken;

            if (newItems != null && newItems.Count > 0)
            {
                foreach (var it in newItems)
                {
                    if (it != null && !string.IsNullOrEmpty(it.Id) && !_items.Exists(x => string.Equals(x.Id, it.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        _items.Add(it);
                    }
                }
            }

            UpdateScrollBounds();
            Invalidate();
        }

        public void SetNoMoreResults()
        {
            _isLoadingMore = false;
            StopLoadingAnim();
            _nextContinuationToken = null;
            Invalidate();
        }

        private void CheckTriggerLoadMore()
        {
            if (!_isLoading && !_isLoadingMore && !string.IsNullOrEmpty(_nextContinuationToken))
            {
                if (_scrollOffset >= _maxScroll - (ActualItemRowHeight * 4))
                {
                    _isLoadingMore = true;
                    StartLoadingAnim();
                    Invalidate();
                    LoadMoreRequested?.Invoke(this, _nextContinuationToken);
                }
            }
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

        private int GetArtistStripHeight()
        {
            if (!IsLarge || _artists == null || _artists.Count == 0) return 0;
            return 162;
        }

        private int GetPlaylistStripHeight()
        {
            if (!IsLarge || _playlists == null || _playlists.Count == 0) return 0;
            return 172;
        }

        private int GetTrackGridHeight(int count)
        {
            if (count <= 0) return 0;
            int padX = IsLarge ? 16 : 12;
            int gap = 14;
            int cols = Math.Max(3, (Width - padX * 2 - 18) / 145);
            int cardW = Math.Max(96, (Width - padX * 2 - 18 - gap * (cols - 1)) / cols);
            int cardH = cardW + 44;
            int rows = (int)Math.Ceiling(count / (double)cols);
            return 28 + rows * cardH + Math.Max(0, rows - 1) * gap + 16;
        }

        private void UpdateScrollBounds()
        {
            int contentTop = ActualTopHeaderHeight + (_isArtistMode ? 0 : TabBarHeight);
            int totalContentHeight = contentTop + 24;

            if (_isArtistMode)
            {
                int heroH = ActualHeroCardHeight + 14;
                int tabsH = IsLarge ? 42 : 0;
                int trkH = (_activeSearchTab == 0 || _activeSearchTab == 1) ? GetTrackGridHeight(_items.Count) : 0;
                int plH = (_activeSearchTab == 0 || _activeSearchTab == 3) ? GetPlaylistStripHeight() + ((_activeSearchTab == 3) ? 160 : 0) : 0;
                totalContentHeight = contentTop + 10 + heroH + tabsH + trkH + plH + 24;
            }
            else if (_activeSearchTab == 0)
            {
                int heroH = _items.Count > 0 ? ActualHeroCardHeight + 12 : 0;
                int artH = GetArtistStripHeight();
                int plH = GetPlaylistStripHeight();
                int trkH = IsLarge ? GetTrackGridHeight(_items.Count) : (_items.Count * ActualItemRowHeight);
                int bottomExtra = (!string.IsNullOrEmpty(_nextContinuationToken) || _isLoadingMore) ? 64 : 24;
                totalContentHeight = contentTop + 8 + heroH + artH + plH + trkH + bottomExtra;
            }
            else if (_activeSearchTab == 1)
            {
                int trkH = IsLarge ? GetTrackGridHeight(_items.Count) : (_items.Count * ActualItemRowHeight);
                int bottomExtra = (!string.IsNullOrEmpty(_nextContinuationToken) || _isLoadingMore) ? 64 : 24;
                totalContentHeight = contentTop + 8 + trkH + bottomExtra;
            }
            else if (_activeSearchTab == 2)
            {
                int cols = Math.Max(2, (Width - 32) / 140);
                int rows = (int)Math.Ceiling((_artists?.Count ?? 0) / (double)cols);
                totalContentHeight = contentTop + 16 + rows * 168;
            }
            else if (_activeSearchTab == 3)
            {
                int cols = Math.Max(2, (Width - 32) / 150);
                int rows = (int)Math.Ceiling((_playlists?.Count ?? 0) / (double)cols);
                totalContentHeight = contentTop + 16 + rows * 184;
            }

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
                CheckTriggerLoadMore();
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
                CheckTriggerLoadMore();
                Invalidate();
                return;
            }

            bool prevBack = _isHoveringBack;
            bool prevBest = _isHoveringBestMatchPlay;
            int prevIdx = _hoveredIndex;
            int prevTab = _hoveredSearchTab;
            int prevArt = _hoveredArtistIdx;
            int prevPl = _hoveredPlaylistIdx;

            _isHoveringBack = _backBtnRect.Contains(e.Location);
            _isHoveringBestMatchPlay = _bestMatchPlayRect.Contains(e.Location);
            _hoveredSearchTab = -1;
            if (IsLarge)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_searchTabRects[i].Contains(e.Location))
                    {
                        _hoveredSearchTab = i;
                        break;
                    }
                }
            }

            _hoveredArtistIdx = -1;
            for (int i = 0; i < _artistCardRects.Count; i++)
            {
                if (_artistCardRects[i].Rect.Contains(e.Location))
                {
                    _hoveredArtistIdx = i;
                    break;
                }
            }

            _hoveredPlaylistIdx = -1;
            for (int i = 0; i < _playlistCardRects.Count; i++)
            {
                if (_playlistCardRects[i].Rect.Contains(e.Location))
                {
                    _hoveredPlaylistIdx = i;
                    break;
                }
            }

            _hoveredIndex = -1;
            if (!IsLarge && (_activeSearchTab == 0 || _activeSearchTab == 1) && e.X < Width - 18 && e.Y >= _tracksStartY && _tracksStartY > 0)
            {
                int row = (e.Y - _tracksStartY) / ActualItemRowHeight;
                if (row >= 0 && row < _items.Count)
                {
                    _hoveredIndex = row;
                }
            }

            int prevTrackCard = _hoveredTrackCardIdx;
            bool prevTrackAdd = _hoveredTrackCardAdd;
            bool prevShuf = _isHoveringArtistShuffle;
            _hoveredTrackCardIdx = -1;
            _hoveredTrackCardAdd = false;
            _isHoveringArtistShuffle = _isArtistMode && _artistShuffleRect.Contains(e.Location);

            for (int i = 0; i < _trackCardRects.Count; i++)
            {
                if (_trackCardRects[i].CardRect.Contains(e.Location))
                {
                    _hoveredTrackCardIdx = i;
                    _hoveredTrackCardAdd = _trackCardRects[i].AddRect.Contains(e.Location);
                    break;
                }
            }

            bool isOverScrollbar = _maxScroll > 0 && GetScrollTrackHitRect().Contains(e.Location);
            Cursor = (_isHoveringBack || _isHoveringBestMatchPlay || _isHoveringArtistShuffle || _hoveredSearchTab >= 0 || _hoveredArtistIdx >= 0 || _hoveredPlaylistIdx >= 0 || _hoveredTrackCardIdx >= 0 || _hoveredIndex >= 0 || isOverScrollbar)
                ? Cursors.Hand : Cursors.Default;

            if (prevBack != _isHoveringBack || prevBest != _isHoveringBestMatchPlay || prevShuf != _isHoveringArtistShuffle || prevIdx != _hoveredIndex ||
                prevTab != _hoveredSearchTab || prevArt != _hoveredArtistIdx || prevPl != _hoveredPlaylistIdx ||
                prevTrackCard != _hoveredTrackCardIdx || prevTrackAdd != _hoveredTrackCardAdd)
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

                if (_hoveredSearchTab >= 0 && _hoveredSearchTab < 4)
                {
                    _activeSearchTab = _hoveredSearchTab;
                    _scrollOffset = 0;
                    _hoveredIndex = -1;
                    _hoveredArtistIdx = -1;
                    _hoveredPlaylistIdx = -1;
                    UpdateScrollBounds();
                    Invalidate();
                    return;
                }

                if (_hoveredArtistIdx >= 0 && _hoveredArtistIdx < _artistCardRects.Count)
                {
                    ArtistSelected?.Invoke(this, _artistCardRects[_hoveredArtistIdx].Artist);
                    return;
                }

                if (_hoveredPlaylistIdx >= 0 && _hoveredPlaylistIdx < _playlistCardRects.Count)
                {
                    PlaylistSelected?.Invoke(this, _playlistCardRects[_hoveredPlaylistIdx].Playlist);
                    return;
                }

                if (_isHoveringArtistShuffle && _isArtistMode && _items.Count > 0)
                {
                    ShuffleAllRequested?.Invoke(this, new List<PlaylistItem>(_items));
                    return;
                }

                if (_hoveredTrackCardIdx >= 0 && _hoveredTrackCardIdx < _trackCardRects.Count)
                {
                    var tc = _trackCardRects[_hoveredTrackCardIdx];
                    if (tc.AddRect.Contains(e.Location))
                    {
                        TrackAddSelected?.Invoke(this, tc.Track);
                    }
                    else
                    {
                        TrackPlaySelected?.Invoke(this, tc.Track);
                    }
                    return;
                }

                if (_isHoveringBestMatchPlay && _items.Count > 0)
                {
                    if (_isArtistMode)
                    {
                        PlayAllRequested?.Invoke(this, new List<PlaylistItem>(_items));
                    }
                    else
                    {
                        TrackPlaySelected?.Invoke(this, _items[0]);
                    }
                    return;
                }

                if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count)
                {
                    PlaylistItem item = _items[_hoveredIndex];
                    int rowY = _tracksStartY + (_hoveredIndex * ActualItemRowHeight);
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

            // Background matching WebApp figma-panel in 3-col maxsize mode
            if (IsLarge)
            {
                Rectangle cardRect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath cardPath = CreateRoundedRectPath(cardRect, 22))
                {
                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(150, 22, 10, 42)))
                    {
                        g.FillPath(bgBrush, cardPath);
                    }
                    using (Pen borderPen = new Pen(_theme.CardBorderColor, 1.2f))
                    {
                        g.DrawPath(borderPen, cardPath);
                    }
                }
            }
            else
            {
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(20, 15, 38), Color.FromArgb(12, 9, 24), LinearGradientMode.Vertical))
                {
                    g.FillRectangle(bgBrush, ClientRectangle);
                }
            }

            // Top Header
            int backBtnW = IsLarge ? 160 : 126;
            int backBtnH = IsLarge ? 34 : 26;
            _backBtnRect = new Rectangle(IsLarge ? 16 : 12, (ActualTopHeaderHeight - backBtnH) / 2, backBtnW, backBtnH);
            using (GraphicsPath bPath = CreateRoundedRectPath(_backBtnRect, 6))
            using (SolidBrush bbBrush = new SolidBrush(_isHoveringBack ? _theme.AccentColor : Color.FromArgb(32, 25, 54)))
            using (Pen bbPen = new Pen(_isHoveringBack ? Color.White : Color.FromArgb(80, _theme.AccentColor), 1f))
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
            using (SolidBrush qBrush = new SolidBrush(_theme.AccentColor))
            {
                StringFormat tsf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center };
                int titleY = ActualTopHeaderHeight / 2;
                if (_isArtistMode)
                {
                    g.DrawString("Thông tin Nghệ sĩ", tFont, tBrush, titleX, titleY, tsf);
                }
                else
                {
                    string prefix = "Kết quả: ";
                    g.DrawString(prefix, tFont, tBrush, titleX, titleY, tsf);
                    float pW = g.MeasureString(prefix, tFont).Width;
                    int textH = IsLarge ? 26 : 20;
                    g.DrawString("\"" + _query + "\"", qFont, qBrush, new RectangleF(titleX + pW, titleY - textH / 2, titleW - pW, textH), tsf);
                }
            }

            using (Pen divPen = new Pen(Color.FromArgb(38, 30, 60)))
            {
                g.DrawLine(divPen, 10, ActualTopHeaderHeight - 1, Width - 10, ActualTopHeaderHeight - 1);
            }

            _artistCardRects.Clear();
            _playlistCardRects.Clear();
            _trackCardRects.Clear();
            _artistShuffleRect = Rectangle.Empty;

            if (IsLarge && !_isArtistMode)
            {
                DrawFilterTabBar(g, ActualTopHeaderHeight + 7);
            }

            int contentTop = ActualTopHeaderHeight + (_isArtistMode ? 0 : TabBarHeight);

            // Clip for scrollable area
            Rectangle contentArea = new Rectangle(0, contentTop, Width, Math.Max(1, Height - contentTop));
            g.SetClip(contentArea);

            if (_isLoading)
            {
                using (Font lFont = new Font("Segoe UI", IsLarge ? 12.5f : 10.0f, FontStyle.Bold))
                using (Font subFont = new Font("Segoe UI", IsLarge ? 10.5f : 8.5f, FontStyle.Regular))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(255, 45, 133)))
                using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(160, 150, 185)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    DrawBouncingText(g, "Đang tải", lFont, textBrush, Width / 2f, Height / 2f - 15, dotSize: IsLarge ? 5f : 4f, dotGap: 4.5f);
                    g.DrawString("Vui lòng đợi giây lát", subFont, subBrush, new RectangleF(0, Height / 2f + 10, Width, 24), sf);
                }
                return;
            }

            if (_items.Count == 0 && (_artists == null || _artists.Count == 0) && (_playlists == null || _playlists.Count == 0))
            {
                using (Font emptyFont = new Font("Segoe UI", IsLarge ? 12.0f : 9.5f, FontStyle.Regular))
                using (SolidBrush emptyBrush = new SolidBrush(Color.FromArgb(160, 150, 185)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("Không tìm thấy kết quả phù hợp cho \"" + _query + "\"", emptyFont, emptyBrush, new RectangleF(0, Height / 2 - 15, Width, 30), sf);
                }
                return;
            }

            int curY = contentTop + 8 - _scrollOffset;
            int cardPad = IsLarge ? 16 : 12;

            if (!_isArtistMode && _activeSearchTab == 2)
            {
                _tracksStartY = Height + 100;
                DrawArtistsSection(g, "Nhạc sĩ", _artists, curY, cardPad, singleRow: false);
                return;
            }
            if (!_isArtistMode && _activeSearchTab == 3)
            {
                _tracksStartY = Height + 100;
                DrawPlaylistsSection(g, "Playlist", _playlists, curY, cardPad, singleRow: false);
                return;
            }

            // Hero Card: Always shown at top in Artist mode, or in "Tất cả" tab of Search mode
            if ((_isArtistMode && _profileArtist != null) || (_activeSearchTab == 0 && _items.Count > 0))
            {
                PlaylistItem hero = _items.Count > 0 ? _items[0] : new PlaylistItem();
                _bestMatchRect = new Rectangle(cardPad, curY, Width - (cardPad * 2), ActualHeroCardHeight);

                using (GraphicsPath hPath = CreateRoundedRectPath(_bestMatchRect, IsLarge ? 12 : 8))
                using (LinearGradientBrush hBrush = new LinearGradientBrush(_bestMatchRect, Color.FromArgb(42, 28, 70), Color.FromArgb(24, 18, 46), LinearGradientMode.Horizontal))
                using (Pen hPen = new Pen(Color.FromArgb(90, _theme.AccentColor), 1f))
                {
                    g.FillPath(hBrush, hPath);
                    g.DrawPath(hPen, hPath);
                }

                // Thumbnail (Circular Artist Avatar in Artist mode, or 144x81 track thumb)
                int heroThumbW = _isArtistMode ? 88 : (IsLarge ? 144 : 96);
                int heroThumbH = _isArtistMode ? 88 : (IsLarge ? 81 : 54);
                Rectangle heroThumbRect = new Rectangle(_bestMatchRect.X + (IsLarge ? 16 : 10), _bestMatchRect.Y + (_bestMatchRect.Height - heroThumbH) / 2, heroThumbW, heroThumbH);

                if (_isArtistMode && _profileArtist != null)
                {
                    using (GraphicsPath ap = new GraphicsPath())
                    {
                        ap.AddEllipse(heroThumbRect);
                        string aKey = "artist_" + _profileArtist.Id;
                        Bitmap aBmp = KahoiiMusic.Services.ThumbnailManager.GetCardThumbnail(aKey);
                        if (aBmp != null)
                        {
                            KahoiiMusic.Services.ThumbnailManager.DrawRoundedImage(g, aBmp, heroThumbRect, ap);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(_profileArtist.ThumbnailUrl))
                            {
                                KahoiiMusic.Services.ThumbnailManager.QueueLoadCard(aKey, _profileArtist.ThumbnailUrl, false, () =>
                                {
                                    if (!IsDisposed) try { BeginInvoke(new Action(Invalidate)); } catch { }
                                });
                            }
                            using (SolidBrush tb = new SolidBrush(Color.FromArgb(40, 26, 68))) g.FillPath(tb, ap);
                        }
                        using (Pen apen = new Pen(_theme.AccentColor, 1.5f)) g.DrawPath(apen, ap);
                    }
                }
                else
                {
                    using (GraphicsPath tp = CreateRoundedRectPath(heroThumbRect, IsLarge ? 8 : 4))
                    {
                        if (_thumbCache.TryGetValue(hero.Id, out Image hThumb) && hThumb != null)
                        {
                            KahoiiMusic.Services.ThumbnailManager.DrawRoundedImage(g, hThumb, heroThumbRect, tp);
                        }
                        else
                        {
                            RequestThumbnail(hero.Id);
                            using (SolidBrush tb = new SolidBrush(Color.FromArgb(30, 22, 50)))
                            {
                                g.FillPath(tb, tp);
                            }
                        }
                    }
                }

                // Big Play button (+ Shuffle button in Artist mode)
                int heroPlayW = IsLarge ? 120 : 74;
                int heroPlayH = IsLarge ? 38 : 30;
                if (_isArtistMode && IsLarge)
                {
                    int shufW = 136;
                    _artistShuffleRect = new Rectangle(_bestMatchRect.Right - shufW - 16, _bestMatchRect.Y + (_bestMatchRect.Height - heroPlayH) / 2, shufW, heroPlayH);
                    _bestMatchPlayRect = new Rectangle(_artistShuffleRect.Left - 10 - heroPlayW, _bestMatchRect.Y + (_bestMatchRect.Height - heroPlayH) / 2, heroPlayW, heroPlayH);

                    using (GraphicsPath sp = CreateRoundedRectPath(_artistShuffleRect, 10))
                    using (SolidBrush sb = new SolidBrush(_isHoveringArtistShuffle ? Color.FromArgb(65, 45, 100) : Color.FromArgb(40, 26, 68)))
                    using (Pen spen = new Pen(Color.FromArgb(90, _theme.SecondaryAccentColor), 1f))
                    using (Font pFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                    using (SolidBrush ptBrush = new SolidBrush(Color.White))
                    using (StringFormat psf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.FillPath(sb, sp);
                        g.DrawPath(spen, sp);
                        g.DrawString("🔀 Phát ngẫu nhiên", pFont, ptBrush, _artistShuffleRect, psf);
                    }
                }
                else
                {
                    _bestMatchPlayRect = new Rectangle(_bestMatchRect.Right - heroPlayW - (IsLarge ? 16 : 10), _bestMatchRect.Y + (_bestMatchRect.Height - heroPlayH) / 2, heroPlayW, heroPlayH);
                }

                using (GraphicsPath pPath = CreateRoundedRectPath(_bestMatchPlayRect, IsLarge ? 10 : 6))
                using (SolidBrush pbBrush = new SolidBrush(_isHoveringBestMatchPlay ? KahoiiMusic.Services.ColorExtractorService.Lerp(_theme.AccentColor, Color.White, 0.25f) : _theme.AccentColor))
                {
                    g.FillPath(pbBrush, pPath);
                    using (Font pFont = new Font("Segoe UI", IsLarge ? 9.8f : 8.0f, FontStyle.Bold))
                    using (SolidBrush ptBrush = new SolidBrush(Color.White))
                    {
                        StringFormat psf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(_isArtistMode ? "▶ Phát tất cả" : "▶ Phát ngay", pFont, ptBrush, _bestMatchPlayRect, psf);
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

                    if (_isArtistMode && _profileArtist != null)
                    {
                        g.DrawString("NGHỆ SĨ CHÍNH THỨC", tagFont, tagBrush, infoX, tagY);
                        g.DrawString(_profileArtist.Name ?? "Nghệ sĩ", heroTitleFont, hTitleBrush, new RectangleF(infoX, titleY, infoW, lineH), tsf);
                        string subLine = !string.IsNullOrEmpty(_profileArtist.Subscribers)
                            ? $"{_profileArtist.Subscribers} • {_items.Count} bài hát"
                            : $"{_items.Count} bài hát • {_playlists.Count} danh sách phát";
                        g.DrawString(subLine, heroSubFont, hSubBrush, new RectangleF(infoX, subY, infoW, lineH), tsf);
                    }
                    else
                    {
                        g.DrawString("BÀI HÁT PHÙ HỢP NHẤT", tagFont, tagBrush, infoX, tagY);
                        g.DrawString(hero.Title ?? "Bài hát", heroTitleFont, hTitleBrush, new RectangleF(infoX, titleY, infoW, lineH), tsf);
                        g.DrawString((hero.Author ?? "Nghệ sĩ") + " • " + hero.FormattedDuration, heroSubFont, hSubBrush, new RectangleF(infoX, subY, infoW, lineH), tsf);
                    }
                }

                curY += ActualHeroCardHeight + 12;
            }

            if (_isArtistMode && IsLarge)
            {
                DrawFilterTabBar(g, curY);
                curY += 40;

                if (_activeSearchTab == 3)
                {
                    _tracksStartY = Height + 100;
                    DrawPlaylistsSection(g, $"Danh sách phát & Album ({_playlists?.Count ?? 0})", _playlists, curY, cardPad, singleRow: false);
                    return;
                }
                if (_activeSearchTab == 0 || _activeSearchTab == 1)
                {
                    _tracksStartY = Height + 100;
                    curY = DrawTrackCardsGrid(g, $"Nhạc Single & Bài hát ({_items.Count})", _items, curY, cardPad);
                }
                if (_activeSearchTab == 0 && _playlists != null && _playlists.Count > 0)
                {
                    DrawPlaylistsSection(g, $"Danh sách phát & Album ({_playlists.Count})", _playlists, curY, cardPad, singleRow: true);
                }
                return;
            }

            if (_activeSearchTab == 0 && IsLarge)
            {
                if (_artists != null && _artists.Count > 0)
                {
                    curY = DrawArtistsSection(g, "Nhạc sĩ", _artists, curY, cardPad, singleRow: true);
                }
                if (_playlists != null && _playlists.Count > 0)
                {
                    curY = DrawPlaylistsSection(g, "Playlist", _playlists, curY, cardPad, singleRow: true);
                }
            }

            if (IsLarge && (_activeSearchTab == 0 || _activeSearchTab == 1))
            {
                _tracksStartY = Height + 100;
                DrawTrackCardsGrid(g, $"Bài hát ({_items.Count})", _items, curY, cardPad);
                return;
            }

            _tracksStartY = curY;

            // Results List (Compact / Minisize mode)
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
                        if (_thumbCache.TryGetValue(item.Id, out Image thumb) && thumb != null)
                        {
                            KahoiiMusic.Services.ThumbnailManager.DrawRoundedImage(g, thumb, thumbRect, tp);
                        }
                        else
                        {
                            RequestThumbnail(item.Id);
                            using (SolidBrush tb = new SolidBrush(Color.FromArgb(28, 22, 48)))
                            {
                                g.FillPath(tb, tp);
                            }
                        }
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

                        string sub = (item.Author ?? "Nghệ sĩ") + " • " + item.FormattedDuration;
                        g.DrawString(sub, subFont, subBrush, new RectangleF(textX, sLineY, textW, sLineH), tsf);
                    }
                }

                curY += ActualItemRowHeight;
            }

            // Bottom loading / more status
            if (_isLoadingMore)
            {
                using (Font moreFont = new Font("Segoe UI", IsLarge ? 10.5f : 8.5f, FontStyle.Bold))
                using (SolidBrush moreBrush = new SolidBrush(Color.FromArgb(255, 45, 133)))
                {
                    DrawBouncingText(g, "Đang tải thêm kết quả", moreFont, moreBrush, Width / 2f, curY + 20, dotSize: IsLarge ? 4.5f : 3.5f, dotGap: 4f);
                }
            }
            else if (!string.IsNullOrEmpty(_nextContinuationToken) && _items.Count >= 15)
            {
                using (Font moreFont = new Font("Segoe UI", IsLarge ? 9.5f : 8.0f, FontStyle.Regular))
                using (SolidBrush moreBrush = new SolidBrush(Color.FromArgb(140, 130, 165)))
                {
                    StringFormat msf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("⤓ Cuộn xuống để tải thêm kết quả...", moreFont, moreBrush, new RectangleF(0, curY + 6, Width, 30), msf);
                }
            }
            else if (_items.Count >= 25)
            {
                using (Font endFont = new Font("Segoe UI", IsLarge ? 9.0f : 8.0f, FontStyle.Regular))
                using (SolidBrush endBrush = new SolidBrush(Color.FromArgb(100, 90, 120)))
                {
                    StringFormat esf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("— Đã hiển thị tất cả kết quả tìm kiếm —", endFont, endBrush, new RectangleF(0, curY + 6, Width, 30), esf);
                }
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

