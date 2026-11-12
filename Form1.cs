using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using KahoiiMusic.Services;
using KahoiiMusic.Models;
using KahoiiMusic.Controls;

namespace KahoiiMusic
{
    public partial class Form1 : Form, IMessageFilter
    {
        // Win32 API to enable smooth window dragging
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;

        public static Form1 Instance { get; private set; }
        private AudioPlayerService _audioPlayer;
        public AudioPlayerService AudioPlayer => _audioPlayer;
        private Controls.PlaylistPopup playlistPopup;
        private AccountForm accountPopup;
        private System.Windows.Forms.Timer _ramTimer;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayItemLoop;
        private bool _isLyricsOpen = false;
        private bool _lyricsSlideTargetOpen = false;
        private float _lyricsSlideProgress = 0f;
        private System.Windows.Forms.Timer _lyricsSlideTimer;
        private string _currentLyricsVideoId = null;
        private Controls.SearchDropdownPopup _searchDropdown;
        private Controls.SearchResultsView _searchResultsView;
        private Controls.SideMenuPanel _sideMenuPanel;
        private Controls.ActiveQueuePanel _activeQueuePanel;
        private Controls.RoundedCardPanel _topSectionCard;
        private Controls.RoundedCardPanel _lyricsCard;
        private Controls.ThumbnailMenuButton btnThumbnailMenu;
        private Controls.ThumbnailActionPopup thumbnailActionPopup;
        private Controls.CollectionPickerPopup _collectionPickerPopup;
        private bool _isMaximizedMode = false;
        private bool _wasMaximizedBeforeMinimize = false;
        private FormWindowState _lastNonMinimizedState = FormWindowState.Normal;
        private bool _isSideMenuUserClosed = false;
        private volatile bool _isFormClosed = false;
        public bool IsFormClosed => _isFormClosed;

        private DynamicThemePalette _currentPalette = ColorExtractorService.GetDefaultTheme();
        private DynamicThemePalette _targetPalette = ColorExtractorService.GetDefaultTheme();
        private DynamicThemePalette _sourcePalette = ColorExtractorService.GetDefaultTheme();
        private System.Windows.Forms.Timer _themeTransitionTimer;
        private int _themeTransitionStep = 0;
        private const int ThemeTransitionTotalSteps = 16;

        private bool _isCloseHovered = false;
        private bool _isMaxHovered = false;
        private bool _isMinHovered = false;

        public Form1() : this(null)
        {
        }

        public Form1(string initialUrl)
        {
            Instance = this;
            InitializeComponent();

            // Wire player card seamless continuous gradient without visible borders ("không còn viền")
            panelPlayerCard.Paint += panelPlayerCard_Paint;
            UpdateThemePanels();
            gradientPanel.TopColorChanged += (s, e) => UpdateThemePanels();
            gradientPanel.BottomColorChanged += (s, e) => UpdateThemePanels();

            // Wire dynamic theme from song thumbnail
            artworkBox.ArtworkChanged += ArtworkBox_ArtworkChanged;
            ApplyCurrentPaletteToUI();

            // Enable double buffering on the Form to prevent any flicker
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            marqueeTitle.Text = "Chưa phát hiện Audio";
            KeyPreview = true;

            Application.AddMessageFilter(this);

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            LoadAppLogo();

            _audioPlayer = new AudioPlayerService();
            MemoryOptimizer.IsDownloadActive = () =>
            {
                bool playerDownloading = _audioPlayer != null && _audioPlayer.IsDownloadingOrPreloading;
                bool savingOffline = SavedTracksService.Instance.IsSavingTrack;
                return playerDownloading || savingOffline;
            };

            SetupEvents();
            InitializeTrayIcon();
            StartSingleInstanceIpcServer();
            FirebaseSyncService.Instance.InitializeAutoSync();
            RegisterProtocolHandler();

            if (!string.IsNullOrEmpty(initialUrl))
            {
                Shown += delegate
                {
                    HandleIncomingUrl(initialUrl);
                };
            }

            Shown += (s, e) =>
            {
                if (HistoryService.IsMaximizedMode && WindowState != FormWindowState.Maximized)
                {
                    Screen scr = Screen.FromHandle(Handle);
                    MaximizedBounds = scr.WorkingArea;
                    WindowState = FormWindowState.Maximized;
                    _lastNonMinimizedState = FormWindowState.Maximized;
                    _wasMaximizedBeforeMinimize = true;
                    ApplyResponsiveLayout();
                }

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(2500);
                        await CheckForAppUpdatesAsync(manual: false);
                    }
                    catch { }
                });
            };

            if (HistoryService.IsMaximizedMode)
            {
                _wasMaximizedBeforeMinimize = true;
                _lastNonMinimizedState = FormWindowState.Maximized;
                WindowState = FormWindowState.Maximized;
            }
            ApplyResponsiveLayout();
            UpdateAudioControlsEnabledState();
        }

        private void LoadAppLogo()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("KahoiiMusic.Resources.logo_icon.png"))
                {
                    if (s != null)
                    {
                        picLogo.Image = Image.FromStream(s);
                        return;
                    }
                }
            }
            catch { }

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\Resources\logo_icon.png");
                if (File.Exists(iconPath))
                {
                    picLogo.Image = Image.FromFile(iconPath);
                    return;
                }
            }
            catch { }
        }

        private void SetupEvents()
        {
            playlistPopup = new Controls.PlaylistPopup();
            playlistPopup.Owner = this;
            playlistPopup.AudioPlayer = _audioPlayer;

            _sideMenuPanel = new Controls.SideMenuPanel();
            _sideMenuPanel.AudioPlayer = _audioPlayer;
            _sideMenuPanel.Visible = false;
            _sideMenuPanel.TrackSelected += (s, item) =>
            {
                if (item != null)
                {
                    if (_audioPlayer.CurrentQueueSource != ActiveQueueSource.Playlist || _audioPlayer.Playlist != _audioPlayer.SessionPlaylist)
                    {
                        if (_audioPlayer.SessionPlaylist != null && _audioPlayer.SessionPlaylist.Count > 0)
                        {
                            int idx = _audioPlayer.SessionPlaylist.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                            _audioPlayer.RestoreSessionPlaylist(idx >= 0 ? idx : 0);
                            return;
                        }
                    }

                    var pl = _audioPlayer.Playlist;
                    if (pl != null)
                    {
                        int idx = pl.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0 && idx < pl.Count)
                        {
                            _audioPlayer.PlayTrackAtIndex(idx);
                            return;
                        }
                    }
                    _audioPlayer.LoadAndPlay(item.Url);
                }
            };
            _sideMenuPanel.CloseRequested += (s, e) =>
            {
                _isSideMenuUserClosed = true;
                _sideMenuPanel.Visible = false;
                UpdateCenterAreaLayout(_isLyricsOpen ? 1f : 0f);
            };
            gradientPanel.Controls.Add(_sideMenuPanel);

            _activeQueuePanel = new Controls.ActiveQueuePanel();
            _activeQueuePanel.AudioPlayer = _audioPlayer;
            _activeQueuePanel.Visible = false;
            _activeQueuePanel.TrackSelected += (s, item) =>
            {
                if (item != null)
                {
                    _audioPlayer?.LoadAndPlay(item.Url);
                }
            };
            gradientPanel.Controls.Add(_activeQueuePanel);

            _topSectionCard = new Controls.RoundedCardPanel();
            _topSectionCard.CornerRadius = 14;
            _topSectionCard.CardBackColor = Color.FromArgb(150, 22, 10, 42);
            _topSectionCard.CardBorderColor = Color.FromArgb(140, 180, 50, 220);
            _topSectionCard.Visible = false;
            gradientPanel.Controls.Add(_topSectionCard);

            _lyricsCard = new Controls.RoundedCardPanel();
            _lyricsCard.CornerRadius = 12;
            _lyricsCard.CardBackColor = Color.FromArgb(120, 18, 8, 36);
            _lyricsCard.CardBorderColor = Color.FromArgb(235, 230, 20, 140);
            _lyricsCard.Visible = false;
            _topSectionCard.Controls.Add(_lyricsCard);

            btnThumbnailMenu = new Controls.ThumbnailMenuButton();
            btnThumbnailMenu.Visible = false;
            btnThumbnailMenu.Click += (s, e) =>
            {
                if (!btnThumbnailMenu.Enabled) return;
                var curItem = GetCurrentPlaylistItem();
                if (curItem == null || string.IsNullOrWhiteSpace(curItem.Id)) return;

                if (thumbnailActionPopup != null)
                {
                    if (thumbnailActionPopup.Visible)
                    {
                        HideThumbnailPopups();
                    }
                    else
                    {
                        thumbnailActionPopup.IsTrackDownloaded = (curItem != null && SavedTracksService.Instance.IsTrackSaved(curItem.Id));
                        thumbnailActionPopup.Visible = true;
                        btnThumbnailMenu.IsOpen = true;
                        thumbnailActionPopup.BringToFront();
                    }
                }
            };
            artworkBox.Controls.Add(btnThumbnailMenu);

            thumbnailActionPopup = new Controls.ThumbnailActionPopup();
            thumbnailActionPopup.Visible = false;

            // Submenu side popup showing user collections
            _collectionPickerPopup = new Controls.CollectionPickerPopup();
            _collectionPickerPopup.Visible = false;

            Action showCollectionPicker = () =>
            {
                if (thumbnailActionPopup != null && thumbnailActionPopup.Visible && _collectionPickerPopup != null)
                {
                    var item = GetCurrentPlaylistItem();
                    string curTrackId = item?.Id;
                    _collectionPickerPopup.LoadCollections(curTrackId);

                    Control targetParent = (_topSectionCard != null && _topSectionCard.Visible) ? _topSectionCard : (Control)gradientPanel;
                    if (_collectionPickerPopup.Parent != targetParent)
                    {
                        targetParent.Controls.Add(_collectionPickerPopup);
                    }

                    // Convert thumbnailActionPopup's top-right corner to screen, then to targetParent client coords
                    Point screenPt = thumbnailActionPopup.PointToScreen(new Point(thumbnailActionPopup.Width + 6, 0));
                    Point localPt = targetParent.PointToClient(screenPt);

                    // Ensure it stays within parent bounds without overlapping thumbnailActionPopup
                    int maxRight = targetParent.Width - 10;
                    if (localPt.X + _collectionPickerPopup.Width > maxRight)
                    {
                        Point screenLeftPt = thumbnailActionPopup.PointToScreen(new Point(-_collectionPickerPopup.Width - 6, 0));
                        localPt = targetParent.PointToClient(screenLeftPt);
                    }

                    _collectionPickerPopup.Location = localPt;
                    thumbnailActionPopup.IsSubmenuOpen = true;
                    _collectionPickerPopup.Visible = true;
                    _collectionPickerPopup.BringToFront();
                }
            };

            thumbnailActionPopup.SaveLibraryClicked += (s, e) => showCollectionPicker();
            thumbnailActionPopup.LibraryHoverTimerElapsed += (s, e) => showCollectionPicker();

            thumbnailActionPopup.DownloadClicked += (s, e) =>
            {
                var item = GetCurrentPlaylistItem();
                if (item != null)
                {
                    if (thumbnailActionPopup.IsTrackDownloaded || SavedTracksService.Instance.IsTrackSaved(item.Id))
                    {
                        SavedTracksService.Instance.DeleteSavedTrack(item.Id);
                        thumbnailActionPopup.IsTrackDownloaded = false;
                        thumbnailActionPopup.Invalidate();
                        _audioPlayer?.RaiseStatus(string.Format("✓ Đã gỡ '{0}' khỏi mục Đã tải!", item.Title));
                        _sideMenuPanel?.RefreshData();
                        return;
                    }

                    _audioPlayer.RaiseStatus(string.Format("Đang bắt đầu tải '{0}'...", item.Title));
                    SavedTracksService.Instance.SaveTrackAsync(item, (success, msg) =>
                    {
                        BeginInvoke(new Action(delegate
                        {
                            if (success)
                            {
                                thumbnailActionPopup.IsTrackDownloaded = true;
                                thumbnailActionPopup.Invalidate();
                                _audioPlayer?.RaiseStatus("✓ Đã tải xong: " + item.Title);
                            }
                            else
                            {
                                _audioPlayer?.RaiseStatus("Lỗi tải: " + msg);
                            }
                            _sideMenuPanel?.RefreshData();
                        }));
                    });
                }
            };

            thumbnailActionPopup.CloseSubmenuRequested += (s, e) =>
            {
                if (_collectionPickerPopup != null)
                {
                    _collectionPickerPopup.Visible = false;
                }
                if (thumbnailActionPopup != null)
                {
                    thumbnailActionPopup.IsSubmenuOpen = false;
                }
            };
            artworkBox.Controls.Add(thumbnailActionPopup);

            _collectionPickerPopup.PlaylistSelected += (s, playlist) =>
            {
                var item = GetCurrentPlaylistItem();
                if (item != null && playlist != null)
                {
                    bool wasInPlaylist = _collectionPickerPopup.PlaylistContainsTrack(playlist);
                    if (wasInPlaylist)
                    {
                        // Gỡ bài hát khỏi bộ sưu tập và mất tick xanh
                        if (playlist.Id == "__FAVORITES__")
                        {
                            FavoritesService.RemoveFavorite(item.Id);
                            if (playlist.Items != null) playlist.Items.RemoveAll(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                            _audioPlayer.RaiseStatus("✓ Đã gỡ khỏi Yêu thích!");
                        }
                        else
                        {
                            PlaylistCollectionService.Instance.RemoveTrackFromPlaylist(playlist.Id, item.Id);
                            if (playlist.Items != null) playlist.Items.RemoveAll(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                            _audioPlayer.RaiseStatus(string.Format("✓ Đã gỡ khỏi bộ sưu tập: {0}", playlist.Name));
                        }
                    }
                    else
                    {
                        // Thêm bài hát vào bộ sưu tập và có tick xanh
                        if (playlist.Id == "__FAVORITES__")
                        {
                            FavoritesService.AddTrack(item);
                            if (playlist.Items == null) playlist.Items = new List<PlaylistItem>();
                            if (!playlist.Items.Exists(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
                                playlist.Items.Insert(0, item);
                            _audioPlayer.RaiseStatus("✓ Đã thêm vào Yêu thích!");
                        }
                        else
                        {
                            PlaylistCollectionService.Instance.AddTrackToPlaylist(playlist.Id, item);
                            if (playlist.Items == null) playlist.Items = new List<PlaylistItem>();
                            if (!playlist.Items.Exists(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
                                playlist.Items.Insert(0, item);
                            _audioPlayer.RaiseStatus(string.Format("✓ Đã thêm vào bộ sưu tập: {0}", playlist.Name));
                        }
                    }
                    _collectionPickerPopup.Invalidate();
                    _sideMenuPanel?.RefreshData();
                }
            };

            _collectionPickerPopup.CreateNewPlaylistClicked += (s, e) =>
            {
                string name = InputDialog.Show(this, "Tạo bộ sưu tập mới", "Nhập tên bộ sưu tập:");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var item = GetCurrentPlaylistItem();
                    var pl = PlaylistCollectionService.Instance.CreatePlaylist(name.Trim());
                    if (item != null)
                    {
                        PlaylistCollectionService.Instance.AddTrackToPlaylist(pl.Id, item);
                        _audioPlayer.RaiseStatus(string.Format("✓ Đã tạo và thêm bài hát vào: {0}", pl.Name));
                    }
                    else
                    {
                        _audioPlayer.RaiseStatus(string.Format("✓ Đã tạo bộ sưu tập: {0}", pl.Name));
                    }
                    _sideMenuPanel?.RefreshData();
                    _collectionPickerPopup.LoadCollections(item?.Id);
                }
            };
            artworkBox.Controls.Add(_collectionPickerPopup);

            accountPopup = new AccountForm();
            accountPopup.Owner = this;

            if (btnLyrics != null)
            {
                btnLyrics.Click += (s, e) =>
                {
                    if (btnLyrics.Enabled)
                    {
                        ToggleLyrics();
                    }
                };
            }

            InitializeLyrics();

            // Window Dragging & Double Click Maximize
            MouseDown += Window_MouseDown;
            picLogo.MouseDown += Window_MouseDown;
            lblLogo.MouseDown += Window_MouseDown;
            panelTopBar.MouseDown += Window_MouseDown;
            lblCopyright.MouseDown += Window_MouseDown;

            picLogo.DoubleClick += (s, e) => ToggleMaximize();
            lblLogo.DoubleClick += (s, e) => ToggleMaximize();
            panelTopBar.DoubleClick += (s, e) => ToggleMaximize();

            // panelTopBar remains transparent to seamlessly inherit the continuous Cyber gradient

            // Title logo gradient matching logo_icon (violet to magenta/pink) - Crisp ClearType
            lblLogo.Text = string.Empty;
            lblLogo.Paint += (s, e) =>
            {
                if (lblLogo.Width <= 0 || lblLogo.Height <= 0) return;
                const string logoText = "Kahoii Music";
                SizeF sz = e.Graphics.MeasureString(logoText, lblLogo.Font);
                float gradWidth = Math.Max(sz.Width, 10f);
                using (LinearGradientBrush lgb = new LinearGradientBrush(
                    new RectangleF(0, 0, gradWidth, lblLogo.Height),
                    Color.FromArgb(190, 120, 255),
                    Color.FromArgb(255, 95, 195),
                    LinearGradientMode.Horizontal))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                    {
                        e.Graphics.DrawString(logoText, lblLogo.Font, lgb, new RectangleF(0, 0, lblLogo.Width, lblLogo.Height), sf);
                    }
                }
            };

            // Window Controls (Close & Minimize & Maximize) - Vector crisp, NO dark box blocks
            btnClose.Text = string.Empty;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => Close();
            btnClose.MouseEnter += (s, e) => { _isCloseHovered = true; btnClose.Invalidate(); };
            btnClose.MouseLeave += (s, e) => { _isCloseHovered = false; btnClose.Invalidate(); };
            btnClose.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (_isCloseHovered)
                {
                    using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(210, 230, 45, 80)))
                    using (GraphicsPath path = FormRoundedRect(new Rectangle(1, 1, btnClose.Width - 2, btnClose.Height - 2), 4))
                    {
                        g.FillPath(hoverBg, path);
                    }
                }
                Color c = _isCloseHovered ? Color.White : Color.FromArgb(160, 160, 175);
                using (Pen p = new Pen(c, 1.35f))
                {
                    int cx = btnClose.Width / 2;
                    int cy = btnClose.Height / 2;
                    int r = 4;
                    g.DrawLine(p, cx - r, cy - r, cx + r, cy + r);
                    g.DrawLine(p, cx + r, cy - r, cx - r, cy + r);
                }
            };

            if (btnMaximize != null)
            {
                btnMaximize.Text = string.Empty;
                btnMaximize.Cursor = Cursors.Hand;
                btnMaximize.Click += (s, e) => ToggleMaximize();
                btnMaximize.MouseEnter += (s, e) => { _isMaxHovered = true; btnMaximize.Invalidate(); };
                btnMaximize.MouseLeave += (s, e) => { _isMaxHovered = false; btnMaximize.Invalidate(); };
                btnMaximize.Paint += (s, e) =>
                {
                    Graphics g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    if (_isMaxHovered)
                    {
                        using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                        using (GraphicsPath path = FormRoundedRect(new Rectangle(1, 1, btnMaximize.Width - 2, btnMaximize.Height - 2), 4))
                        {
                            g.FillPath(hoverBg, path);
                        }
                    }
                    Color c = _isMaxHovered ? Color.White : Color.FromArgb(160, 160, 175);
                    using (Pen p = new Pen(c, 1.25f))
                    {
                        int cx = btnMaximize.Width / 2;
                        int cy = btnMaximize.Height / 2;
                        if (WindowState == FormWindowState.Maximized)
                        {
                            // Restore overlapping squares
                            g.DrawRectangle(p, cx - 2, cy - 5, 7, 7);
                            g.DrawRectangle(p, cx - 5, cy - 2, 7, 7);
                        }
                        else
                        {
                            // Single clean square
                            g.DrawRectangle(p, cx - 4, cy - 4, 8, 8);
                        }
                    }
                };
            }

            btnMinimize.Text = string.Empty;
            btnMinimize.Cursor = Cursors.Hand;
            btnMinimize.Click += (s, e) => MinimizeToTray();
            btnMinimize.MouseEnter += (s, e) => { _isMinHovered = true; btnMinimize.Invalidate(); };
            btnMinimize.MouseLeave += (s, e) => { _isMinHovered = false; btnMinimize.Invalidate(); };
            btnMinimize.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (_isMinHovered)
                {
                    using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                    using (GraphicsPath path = FormRoundedRect(new Rectangle(1, 1, btnMinimize.Width - 2, btnMinimize.Height - 2), 4))
                    {
                        g.FillPath(hoverBg, path);
                    }
                }
                Color c = _isMinHovered ? Color.White : Color.FromArgb(160, 160, 175);
                using (Pen p = new Pen(c, 1.35f))
                {
                    int cx = btnMinimize.Width / 2;
                    int cy = btnMinimize.Height / 2 + 3;
                    g.DrawLine(p, cx - 5, cy, cx + 5, cy);
                }
            };

            _userToolTip = new ToolTip();
            if (lblVersion != null)
            {
                lblVersion.UseCompatibleTextRendering = true;
                lblVersion.Text = "v" + AppUpdateService.CurrentVersionName;
                lblVersion.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                lblVersion.ForeColor = Color.FromArgb(200, 185, 235);
                lblVersion.Location = new Point(panelBottomControls.ClientSize.Width - lblVersion.Width - 10, panelBottomControls.ClientSize.Height - lblVersion.Height - 6);
                lblVersion.BringToFront();
                lblVersion.Click += async (s, e) => await CheckForAppUpdatesAsync(manual: true);
                lblVersion.MouseEnter += (s, e) => lblVersion.ForeColor = Color.FromArgb(255, 240, 255);
                lblVersion.MouseLeave += (s, e) => lblVersion.ForeColor = Color.FromArgb(200, 185, 235);
            }

            if (panelBottomControls != null)
            {
                panelBottomControls.Resize += (s, e) =>
                {
                    if (lblVersion != null)
                    {
                        lblVersion.Location = new Point(panelBottomControls.ClientSize.Width - lblVersion.Width - 10, panelBottomControls.ClientSize.Height - lblVersion.Height - 6);
                        lblVersion.BringToFront();
                    }
                    if (lblCopyright != null)
                    {
                        lblCopyright.Location = new Point((panelBottomControls.ClientSize.Width - lblCopyright.Width) / 2, panelBottomControls.ClientSize.Height - lblCopyright.Height - 6);
                    }
                };
            }
            UpdateUserButtonAppearance();
            FirebaseAuthService.Instance.AuthStateChanged += (s, e) =>
            {
                if (InvokeRequired) Invoke(new Action(UpdateUserButtonAppearance));
                else UpdateUserButtonAppearance();
            };

            // Periodic lightweight memory trimming timer (every 30s) running on threadpool to avoid any animation hitch
            _ramTimer = new Timer();
            _ramTimer.Interval = 30000;
            _ramTimer.Tick += (s, e) =>
            {
                bool isBackground = WindowState == FormWindowState.Minimized || !Visible;
                bool isDownloading = MemoryOptimizer.IsDownloadActive != null && MemoryOptimizer.IsDownloadActive();
                if (isBackground && !isDownloading)
                {
                    MemoryOptimizer.TrimMemory(true);
                }
                else
                {
                    MemoryOptimizer.TrimMemory(false);
                }
            };
            _ramTimer.Start();

            // Audio Player Events
            _audioPlayer.StatusMessageChanged += AudioPlayer_StatusMessageChanged;
            _audioPlayer.AuthorChanged += (s, author) =>
            {
                if (InvokeRequired)
                {
                    try { BeginInvoke(new Action(() => marqueeAuthor.Text = author ?? string.Empty)); } catch { }
                }
                else
                {
                    marqueeAuthor.Text = author ?? string.Empty;
                }
            };
            _audioPlayer.TitleChanged += (s, title) =>
            {
                string cleanTitle = AudioPlayerService.CleanDisplayTitle(title);
                marqueeTitle.Text = cleanTitle;
                UpdateTrayTooltip(cleanTitle);
                if (!string.IsNullOrEmpty(_audioPlayer.CurrentVideoId))
                {
                    var trackItem = new PlaylistItem
                    {
                        Id = _audioPlayer.CurrentVideoId,
                        Title = cleanTitle,
                        Author = marqueeAuthor.Text,
                        DurationSeconds = (int)_audioPlayer.TotalDuration.TotalSeconds
                    };
                    if (btnQuickDownload != null) btnQuickDownload.SetCurrentTrack(trackItem);
                    if (btnFavorite != null) btnFavorite.SetCurrentTrack(trackItem);
                }
                UpdateAudioControlsEnabledState();
            };
            _audioPlayer.ThumbnailUrlReady += AudioPlayer_ThumbnailUrlReady;
            _audioPlayer.PlaybackStateChanged += AudioPlayer_PlaybackStateChanged;
            _audioPlayer.PlaybackEnded += AudioPlayer_PlaybackEnded;
            _audioPlayer.PlaybackLooped += AudioPlayer_PlaybackLooped;
            _audioPlayer.TrackLoaded += (s, args) =>
            {
                string curT = !string.IsNullOrEmpty(args.Title) ? args.Title : _audioPlayer.CurrentTitle;
                if (!string.IsNullOrEmpty(curT))
                {
                    string cleanT = AudioPlayerService.CleanDisplayTitle(curT);
                    marqueeTitle.Text = cleanT;
                    UpdateTrayTooltip(cleanT);
                }
                string curA = !string.IsNullOrEmpty(args.Author) ? args.Author : _audioPlayer.CurrentAuthor;
                marqueeAuthor.Text = AudioPlayerService.CleanAuthor(curA ?? string.Empty);

                var loadedItem = new PlaylistItem
                {
                    Id = args.VideoId,
                    Title = args.Title,
                    Author = marqueeAuthor.Text,
                    DurationSeconds = (int)args.Duration.TotalSeconds
                };
                if (btnQuickDownload != null) btnQuickDownload.SetCurrentTrack(loadedItem);
                if (btnFavorite != null) btnFavorite.SetCurrentTrack(loadedItem);

                if (lyricsPanel != null && args.Duration > TimeSpan.Zero)
                {
                    lyricsPanel.TotalDuration = args.Duration;
                }
                LoadLyricsForTrack(args.VideoId, curT, curA, (int)args.Duration.TotalSeconds);
                UpdateAudioControlsEnabledState();
            };

            if (btnFavorite != null)
            {
                btnFavorite.FavoriteChanged += (s, isFav) =>
                {
                    string t = !string.IsNullOrEmpty(_audioPlayer.CurrentTitle) ? AudioPlayerService.CleanDisplayTitle(_audioPlayer.CurrentTitle) : "Bài hát";
                    _audioPlayer.RaiseStatus(isFav ? ("❤️ Đã thêm vào Yêu thích: " + t) : ("💔 Đã xóa khỏi Yêu thích: " + t));
                };
            }

            // Loop Button
            btnLoop.LoopChanged += (s, e) =>
            {
                _audioPlayer.LoopMode = btnLoop.Mode;
                UpdateLoopTooltip();
                if (_trayItemLoop != null)
                {
                    _trayItemLoop.Checked = btnLoop.IsLooping;
                    _trayItemLoop.Text = GetLoopMenuText(btnLoop.Mode);
                }
            };
            UpdateLoopTooltip();

            // Sleep Timer
            KahoiiMusic.Services.SleepTimerService.Instance.TimerExpired += (s, e) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => StopAudioForTimer()));
                }
                else
                {
                    StopAudioForTimer();
                }
            };

            // Play / Pause Button Click
            btnPlayPause.Click += BtnPlayPause_Click;

            // Volume Button and Popup Events
            btnVolume.Click += (s, e) =>
            {
                volumePopup.Visible = !volumePopup.Visible;
                if (volumePopup.Visible)
                {
                    UpdateVolumePopupPosition();
                }
            };

            volumePopup.VolumeChanged += (s, v) =>
            {
                _audioPlayer.Volume = v;
                btnVolume.Volume = v;
            };

            // Playlist Button and Popup Events
            Action togglePlaylistAction = () =>
            {
                if (accountPopup != null && accountPopup.Visible)
                {
                    accountPopup.Hide();
                }
                if (playlistPopup.Visible || playlistPopup.JustDeactivated)
                {
                    playlistPopup.Hide();
                    btnPlaylist.IsOpen = false;
                    this.Activate();
                }
                else
                {
                    Point screenPt = panelTopBar.PointToScreen(new Point(12, btnPlaylist.Bottom + 4));
                    playlistPopup.Location = screenPt;
                    playlistPopup.Show(this);
                    btnPlaylist.IsOpen = true;
                    this.Activate();
                }
            };

            btnPlaylist.Click += (s, e) =>
            {
                int screenW = Screen.FromControl(this).WorkingArea.Width;
                int thresholdW = Math.Max(560, screenW / 3);
                if (ClientSize.Width >= thresholdW)
                {
                    if (playlistPopup != null && playlistPopup.Visible) playlistPopup.Hide();
                    if (_sideMenuPanel != null)
                    {
                        _isSideMenuUserClosed = _sideMenuPanel.Visible;
                        _sideMenuPanel.Visible = !_isSideMenuUserClosed;
                        if (_sideMenuPanel.Visible) _sideMenuPanel.RefreshData();
                        UpdateCenterAreaLayout(_isLyricsOpen ? 1f : 0f);
                    }
                    return;
                }
                togglePlaylistAction();
            };

            // Close popup when main window loses focus (e.g. user Alt+Tabs or clicks another application)
            this.Deactivate += (s, e) =>
            {
                if (playlistPopup != null && playlistPopup.Visible)
                {
                    Point cur = Cursor.Position;
                    if (playlistPopup.Bounds.Contains(cur)) return;
                    if (playlistPopup.ContainsFocus) return;

                    playlistPopup.Hide();
                    btnPlaylist.IsOpen = false;
                }
            };

            playlistPopup.CloseRequested += (s, e) =>
            {
                btnPlaylist.IsOpen = false;
            };

            this.LocationChanged += (s, e) =>
            {
                if (playlistPopup != null && playlistPopup.Visible)
                {
                    playlistPopup.Location = panelTopBar.PointToScreen(new Point(12, btnPlaylist.Bottom + 4));
                }
                if (accountPopup != null && accountPopup.Visible)
                {
                    Screen scr = Screen.FromControl(this);
                    Rectangle bounds = scr.WorkingArea;
                    int x = this.Location.X + (this.Width - accountPopup.Width) / 2;
                    int y = this.Location.Y + (this.Height - accountPopup.Height) / 2;
                    x = Math.Max(bounds.Left, Math.Min(x, bounds.Right - accountPopup.Width));
                    y = Math.Max(bounds.Top, Math.Min(y, bounds.Bottom - accountPopup.Height));
                    accountPopup.Location = new Point(x, y);
                }
                if (_searchDropdown != null && _searchDropdown.Visible)
                {
                    _searchDropdown.Location = panelTopBar.PointToScreen(new Point(searchPillBox.Left, searchPillBox.Bottom + 4));
                    _searchDropdown.Width = searchPillBox.Width;
                }
            };

            playlistPopup.TrackSelected += (s, item) =>
            {
                if (item != null)
                {
                    if (_audioPlayer.CurrentQueueSource != ActiveQueueSource.Playlist || _audioPlayer.Playlist != _audioPlayer.SessionPlaylist)
                    {
                        int idx = _audioPlayer.SessionPlaylist.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                        _audioPlayer.RestoreSessionPlaylist(idx >= 0 ? idx : playlistPopup.CurrentPlaylistIndex);
                        return;
                    }

                    var pl = _audioPlayer.Playlist;
                    if (pl != null)
                    {
                        int idx = pl.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0 && idx < pl.Count)
                        {
                            _audioPlayer.PlayTrackAtIndex(idx);
                            return;
                        }
                    }
                    _audioPlayer.LoadAndPlay(item.Url);
                }
            };

            _audioPlayer.PlaylistLoaded += (s, list) =>
            {
                bool hasList = (list != null && list.Count > 0);
                btnPlaylist.HasPlaylist = hasList;
                if (hasList)
                {
                    playlistPopup.SetPlaylist(list, _audioPlayer.CurrentPlaylistIndex);
                }
                else
                {
                    btnPlaylist.IsOpen = false;
                    playlistPopup.Clear();
                    playlistPopup.Hide();
                }
            };

            _audioPlayer.PlaylistIndexChanged += (s, idx) =>
            {
                if (_audioPlayer.CurrentQueueSource == ActiveQueueSource.Playlist)
                {
                    playlistPopup.SetCurrentIndex(idx);
                }
                else
                {
                    // Playing from Collection or History - don't hijack Tab 0 current index
                    string currentId = _audioPlayer.CurrentVideoId;
                    int plIdx = !string.IsNullOrEmpty(currentId)
                        ? playlistPopup.PlaylistItems.FindIndex(x => string.Equals(x.Id, currentId, StringComparison.OrdinalIgnoreCase))
                        : -1;
                    playlistPopup.SetCurrentIndex(plIdx);
                }
            };

            // Initialize volume levels
            volumePopup.Volume = _audioPlayer.Volume;
            btnVolume.Volume = _audioPlayer.Volume;
            btnLoop.IsLooping = _audioPlayer.IsLooping;

            // Timer for updating track progress during audio playback (30 FPS for smooth sync)
            playbackTimer.Interval = 33;
            playbackTimer.Tick += PlaybackTimer_Tick;

            // TrackBar Seek & Preview
            musicTrackBar.ValueChanged += (s, val) =>
            {
                if (musicTrackBar.IsDragging && _audioPlayer.HasTrack && _audioPlayer.TotalDuration > TimeSpan.Zero)
                {
                    TimeSpan previewTime = TimeSpan.FromSeconds(_audioPlayer.TotalDuration.TotalSeconds * val);
                    artworkBox.TimeText = FormatTimeSpan(previewTime, _audioPlayer.TotalDuration);
                }
            };
            musicTrackBar.SeekCommitted += MusicTrackBar_SeekCommitted;

            // Directional Buttons (Previous / Next)
            btnPrevious.Click += BtnPrevious_Click;
            btnNext.Click += BtnNext_Click;

            // YouTube URL entered
            searchPillBox.UrlSubmitted += SearchPillBox_UrlSubmitted;

            // Hybrid YouTube Search (Real-time dropdown + Full search results tab)
            SetupHybridSearch();
        }

        private void HideThumbnailPopups()
        {
            if (thumbnailActionPopup != null)
            {
                thumbnailActionPopup.Visible = false;
                thumbnailActionPopup.IsSubmenuOpen = false;
            }
            if (_collectionPickerPopup != null)
            {
                _collectionPickerPopup.Visible = false;
            }
            if (btnThumbnailMenu != null)
            {
                btnThumbnailMenu.IsOpen = false;
            }
        }

        private PlaylistItem GetCurrentPlaylistItem()
        {
            if (_audioPlayer == null || !_audioPlayer.HasTrack || string.IsNullOrEmpty(_audioPlayer.CurrentVideoId))
                return null;
            string tit = _audioPlayer.CurrentTitle;
            return new PlaylistItem
            {
                Id = _audioPlayer.CurrentVideoId,
                Title = !string.IsNullOrEmpty(tit) ? AudioPlayerService.CleanDisplayTitle(tit) : "Bài hát YouTube",
                Author = _audioPlayer.CurrentAuthor ?? "YouTube",
                DurationSeconds = (int)_audioPlayer.TotalDuration.TotalSeconds
            };
        }

        private void SetupHybridSearch()
        {
            _searchDropdown = new Controls.SearchDropdownPopup();
            _searchDropdown.TrackPlaySelected += (s, item) =>
            {
                if (item != null)
                {
                    PlayTrackItem(item);
                }
            };
            _searchDropdown.TrackAddSelected += (s, item) =>
            {
                if (item != null)
                {
                    _audioPlayer.AddTrackToQueue(item);
                }
            };
            _searchDropdown.ViewAllSelected += (s, query) =>
            {
                _searchDropdown.Hide();
                searchPillBox.RemoveFocus();
                ShowFullSearchResultsAsync(query);
            };

            searchPillBox.SearchQueryDebounced += async (s, query) =>
            {
                if (!searchPillBox.IsSearchFocused || string.IsNullOrWhiteSpace(query))
                {
                    _searchDropdown.Hide();
                    return;
                }
                var results = await Services.YouTubeSearchService.SearchAsync(query, 5);
                if (!searchPillBox.IsSearchFocused)
                {
                    _searchDropdown.Hide();
                    return;
                }
                if (results != null && results.Count > 0 && searchPillBox.HasRealText && string.Equals(searchPillBox.Text.Trim(), query, StringComparison.OrdinalIgnoreCase))
                {
                    Point pt = panelTopBar.PointToScreen(new Point(searchPillBox.Left, searchPillBox.Bottom + 4));
                    _searchDropdown.Location = pt;
                    _searchDropdown.SetResults(query, results, searchPillBox.Width);
                    if (!_searchDropdown.Visible && searchPillBox.IsSearchFocused)
                    {
                        _searchDropdown.Show(this);
                    }
                }
                else
                {
                    _searchDropdown.Hide();
                }
            };

            searchPillBox.SearchSubmitted += (s, query) =>
            {
                _searchDropdown.Hide();
                searchPillBox.RemoveFocus();
                ShowFullSearchResultsAsync(query);
            };

            searchPillBox.SearchCleared += (s, e) =>
            {
                _searchDropdown.Hide();
            };

            _searchResultsView = new Controls.SearchResultsView();
            _searchResultsView.Dock = DockStyle.Fill;
            _searchResultsView.Visible = false;
            _searchResultsView.TrackPlaySelected += (s, item) =>
            {
                if (item != null)
                {
                    PlayTrackItem(item);
                }
            };
            _searchResultsView.TrackAddSelected += (s, item) =>
            {
                if (item != null)
                {
                    _audioPlayer.AddTrackToQueue(item);
                }
            };
            _searchResultsView.CloseRequested += (s, e) =>
            {
                _searchResultsView.Visible = false;
                if (lyricsPanel != null && _isLyricsOpen)
                {
                    lyricsPanel.BringToFront();
                }
            };
            _searchResultsView.LoadMoreRequested += async (s, token) =>
            {
                string curQ = _searchResultsView.CurrentQuery;
                if (!string.IsNullOrEmpty(curQ))
                {
                    var nextBatch = await Services.YouTubeSearchService.SearchBatchAsync(curQ, token);
                    if (nextBatch != null && nextBatch.Items.Count > 0)
                    {
                        _searchResultsView.AppendResults(nextBatch.Items, nextBatch.NextContinuationToken);
                    }
                    else
                    {
                        _searchResultsView.SetNoMoreResults();
                    }
                }
            };
            gradientPanel.Controls.Add(_searchResultsView);
        }

        private void PlayTrackItem(PlaylistItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) return;
            _audioPlayer.IsPlayingFromHistory = false;
            _audioPlayer.CurrentQueueSource = ActiveQueueSource.Playlist;
            btnPlaylist.HasPlaylist = true;

            _searchDropdown?.Hide();
            searchPillBox?.RemoveFocus();

            _audioPlayer.EnqueueAndPlayTrack(item.Id, item.Title, item.Author, item.DurationSeconds, item.Url);
            UpdateAudioControlsEnabledState();
        }

        private async void ShowFullSearchResultsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            if (searchPillBox != null && searchPillBox.Text != query) searchPillBox.Text = query;
            _searchResultsView.BringToFront();
            _searchResultsView.Visible = true;
            _searchResultsView.ShowLoading(query);
            var batch = await Services.YouTubeSearchService.SearchBatchAsync(query);
            _searchResultsView.SetResults(query, batch.Items, batch.NextContinuationToken);
            _searchResultsView.BringToFront();
        }

        private bool _isMaximizedDragTracking = false;
        private Point _maximizedDragStartPos;

        public bool PreFilterMessage(ref Message m)
        {
            // Support dragging header to quickly unmaximize/restore when maximized
            if (m.Msg == 0x0201 /* WM_LBUTTONDOWN */)
            {
                if (WindowState == FormWindowState.Maximized && panelTopBar != null && panelTopBar.IsHandleCreated)
                {
                    Point cur = Cursor.Position;
                    Rectangle barScreen = panelTopBar.RectangleToScreen(panelTopBar.ClientRectangle);
                    if (barScreen.Contains(cur))
                    {
                        bool onSearch = searchPillBox != null && searchPillBox.Visible && searchPillBox.RectangleToScreen(searchPillBox.ClientRectangle).Contains(cur);
                        bool onControls = (btnClose != null && btnClose.RectangleToScreen(btnClose.ClientRectangle).Contains(cur)) ||
                                          (btnMaximize != null && btnMaximize.RectangleToScreen(btnMaximize.ClientRectangle).Contains(cur)) ||
                                          (btnMinimize != null && btnMinimize.RectangleToScreen(btnMinimize.ClientRectangle).Contains(cur)) ||
                                          (btnUser != null && btnUser.RectangleToScreen(btnUser.ClientRectangle).Contains(cur));
                        if (!onSearch && !onControls)
                        {
                            _isMaximizedDragTracking = true;
                            _maximizedDragStartPos = cur;
                        }
                    }
                }
            }
            else if (m.Msg == 0x0200 /* WM_MOUSEMOVE */)
            {
                if (_isMaximizedDragTracking)
                {
                    if ((Control.MouseButtons & MouseButtons.Left) != 0 && WindowState == FormWindowState.Maximized)
                    {
                        Point cur = Cursor.Position;
                        int dx = Math.Abs(cur.X - _maximizedDragStartPos.X);
                        int dy = Math.Abs(cur.Y - _maximizedDragStartPos.Y);
                        if (dx > 4 || dy > 4)
                        {
                            _isMaximizedDragTracking = false;
                            float ratio = (float)_maximizedDragStartPos.X / Math.Max(1, Width);
                            WindowState = FormWindowState.Normal;
                            if (btnMaximize != null) btnMaximize.Invalidate();

                            int restoredW = Width;
                            int targetX = cur.X - (int)(restoredW * ratio);
                            int targetY = Math.Max(0, cur.Y - 18);
                            Location = new Point(targetX, targetY);

                            ReleaseCapture();
                            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                            return true;
                        }
                    }
                    else if ((Control.MouseButtons & MouseButtons.Left) == 0)
                    {
                        _isMaximizedDragTracking = false;
                    }
                }
            }
            else if (m.Msg == 0x0202 /* WM_LBUTTONUP */)
            {
                _isMaximizedDragTracking = false;
            }

            // Forward mouse wheel to searchResultsView, playlistPopup, or lyricsPanel if hovering over them
            if (m.Msg == 0x020A /* WM_MOUSEWHEEL */)
            {
                Point cur = Cursor.Position;
                if (_searchResultsView != null && _searchResultsView.Visible && _searchResultsView.IsHandleCreated)
                {
                    Point ptSearch = _searchResultsView.PointToClient(cur);
                    if (_searchResultsView.ClientRectangle.Contains(ptSearch))
                    {
                        SendMessage(_searchResultsView.Handle, m.Msg, (int)m.WParam, (int)m.LParam);
                        return true;
                    }
                }
                else if (playlistPopup != null && playlistPopup.Visible && playlistPopup.Bounds.Contains(cur))
                {
                    if (m.HWnd != playlistPopup.Handle)
                    {
                        SendMessage(playlistPopup.Handle, m.Msg, (int)m.WParam, (int)m.LParam);
                        return true;
                    }
                }
                else if (lyricsPanel != null && lyricsPanel.Visible && lyricsPanel.IsHandleCreated)
                {
                    Point ptLyrics = lyricsPanel.PointToClient(cur);
                    if (lyricsPanel.ClientRectangle.Contains(ptLyrics))
                    {
                        SendMessage(lyricsPanel.Handle, m.Msg, (int)m.WParam, (int)m.LParam);
                        return true;
                    }
                }
                else if (_sideMenuPanel != null && _sideMenuPanel.Visible && _sideMenuPanel.IsHandleCreated)
                {
                    Point ptSide = _sideMenuPanel.PointToClient(cur);
                    if (_sideMenuPanel.ClientRectangle.Contains(ptSide))
                    {
                        SendMessage(_sideMenuPanel.Handle, m.Msg, (int)m.WParam, (int)m.LParam);
                        return true;
                    }
                }
                else if (_activeQueuePanel != null && _activeQueuePanel.Visible && _activeQueuePanel.IsHandleCreated)
                {
                    Point ptQ = _activeQueuePanel.PointToClient(cur);
                    if (_activeQueuePanel.ClientRectangle.Contains(ptQ))
                    {
                        SendMessage(_activeQueuePanel.Handle, m.Msg, (int)m.WParam, (int)m.LParam);
                        return true;
                    }
                }
                else
                {
                    bool isOverVolume = (volumePopup != null && volumePopup.Visible && volumePopup.RectangleToScreen(volumePopup.ClientRectangle).Contains(cur)) ||
                                        (btnVolume != null && btnVolume.Visible && btnVolume.RectangleToScreen(btnVolume.ClientRectangle).Contains(cur));
                    if (isOverVolume)
                    {
                        short delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                        float volChange = (delta > 0) ? 0.04f : -0.04f;
                        AdjustVolume(volChange);
                        return true;
                    }
                }
            }

            // Close popups when user clicks anywhere outside of them
            // WM_LBUTTONDOWN = 0x0201, WM_NCLBUTTONDOWN = 0x00A1, WM_RBUTTONDOWN = 0x0204
            if (m.Msg == 0x0201 || m.Msg == 0x00A1 || m.Msg == 0x0204)
            {
                Point cur = Cursor.Position;
                if (volumePopup != null && volumePopup.Visible)
                {
                    Point ptPopup = volumePopup.PointToClient(cur);
                    Point ptBtn = btnVolume.PointToClient(cur);
                    if (!volumePopup.ClientRectangle.Contains(ptPopup) && !btnVolume.ClientRectangle.Contains(ptBtn))
                    {
                        volumePopup.Visible = false;
                    }
                }
                if ((thumbnailActionPopup != null && thumbnailActionPopup.Visible) || (_collectionPickerPopup != null && _collectionPickerPopup.Visible))
                {
                    Rectangle popupScreenRect = (thumbnailActionPopup != null && thumbnailActionPopup.Visible)
                        ? thumbnailActionPopup.RectangleToScreen(thumbnailActionPopup.ClientRectangle)
                        : Rectangle.Empty;
                    Rectangle subPopupScreenRect = (_collectionPickerPopup != null && _collectionPickerPopup.Visible)
                        ? _collectionPickerPopup.RectangleToScreen(_collectionPickerPopup.ClientRectangle)
                        : Rectangle.Empty;
                    Rectangle btnScreenRect = (btnThumbnailMenu != null && btnThumbnailMenu.Visible)
                        ? btnThumbnailMenu.RectangleToScreen(btnThumbnailMenu.ClientRectangle)
                        : Rectangle.Empty;
                    if (!popupScreenRect.Contains(cur) && !subPopupScreenRect.Contains(cur) && !btnScreenRect.Contains(cur))
                    {
                        HideThumbnailPopups();
                    }
                }
                if (playlistPopup != null && playlistPopup.Visible)
                {
                    Rectangle popupScreenRect = playlistPopup.Bounds;
                    Rectangle btnScreenRect = panelTopBar.RectangleToScreen(btnPlaylist.Bounds);
                    if (!popupScreenRect.Contains(cur) && !btnScreenRect.Contains(cur))
                    {
                        playlistPopup.Hide();
                        btnPlaylist.IsOpen = false;
                        this.Activate();
                    }
                }
                if (accountPopup != null && accountPopup.Visible && !accountPopup.IsShowingChildDialog)
                {
                    Rectangle popupScreenRect = accountPopup.Bounds;
                    Rectangle btnScreenRect = panelTopBar.RectangleToScreen(btnUser.Bounds);
                    if (!popupScreenRect.Contains(cur) && !btnScreenRect.Contains(cur))
                    {
                        accountPopup.Hide();
                        this.Activate();
                    }
                }
                if (searchPillBox != null && searchPillBox.Visible && searchPillBox.ContainsFocus)
                {
                    Rectangle searchScreenRect = panelTopBar.RectangleToScreen(searchPillBox.Bounds);
                    bool insideDropdown = (_searchDropdown != null && _searchDropdown.Visible && new Rectangle(_searchDropdown.Location, _searchDropdown.Size).Contains(cur));
                    if (!searchScreenRect.Contains(cur) && !insideDropdown)
                    {
                        searchPillBox.RemoveFocus();
                        _searchDropdown?.Hide();
                    }
                }
                else if (_searchDropdown != null && _searchDropdown.Visible)
                {
                    Rectangle dropRect = new Rectangle(_searchDropdown.Location, _searchDropdown.Size);
                    Rectangle searchScreenRect = panelTopBar.RectangleToScreen(searchPillBox.Bounds);
                    if (!dropRect.Contains(cur) && !searchScreenRect.Contains(cur))
                    {
                        _searchDropdown.Hide();
                    }
                }
            }
            return false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_searchDropdown != null && _searchDropdown.Visible && keyData == Keys.Escape)
            {
                _searchDropdown.Hide();
                searchPillBox.RemoveFocus();
                return true;
            }

            if (_searchResultsView != null && _searchResultsView.Visible && keyData == Keys.Escape)
            {
                _searchResultsView.Visible = false;
                return true;
            }

            if (searchPillBox != null && searchPillBox.ContainsFocus)
            {
                if (keyData == Keys.Escape)
                {
                    searchPillBox.RemoveFocus();
                    _searchDropdown?.Hide();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            if (_sideMenuPanel != null && _sideMenuPanel.Visible && _sideMenuPanel.IsSearchFocused)
            {
                if (keyData == Keys.Escape)
                {
                    _sideMenuPanel.ClearSearchFocus();
                    this.Focus();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            switch (keyData)
            {
                case Keys.Space:
                case Keys.MediaPlayPause:
                    BtnPlayPause_Click(this, EventArgs.Empty);
                    return true;

                case Keys.MediaNextTrack:
                case (Keys.Control | Keys.Right):
                    BtnNext_Click(this, EventArgs.Empty);
                    return true;

                case Keys.MediaPreviousTrack:
                case (Keys.Control | Keys.Left):
                    BtnPrevious_Click(this, EventArgs.Empty);
                    return true;

                case Keys.Right:
                    SeekRelative(5.0);
                    return true;

                case Keys.Left:
                    SeekRelative(-5.0);
                    return true;

                case Keys.Up:
                    AdjustVolume(0.05f);
                    return true;

                case Keys.Down:
                    AdjustVolume(-0.05f);
                    return true;

                case Keys.M:
                    ToggleMute();
                    return true;

                case Keys.L:
                    btnLoop.CycleNextMode();
                    _audioPlayer.LoopMode = btnLoop.Mode;
                    UpdateLoopTooltip();
                    if (_trayItemLoop != null)
                    {
                        _trayItemLoop.Checked = btnLoop.IsLooping;
                        _trayItemLoop.Text = GetLoopMenuText(btnLoop.Mode);
                    }
                    return true;

                case Keys.F:
                case (Keys.Control | Keys.D):
                    if (btnFavorite != null)
                    {
                        if (string.IsNullOrEmpty(_audioPlayer.CurrentVideoId))
                        {
                            _audioPlayer.RaiseStatus("Chưa có bài hát nào đang phát");
                        }
                        else
                        {
                            btnFavorite.ToggleFavorite();
                        }
                        return true;
                    }
                    break;

                case (Keys.Control | Keys.L):
                    if (btnLyrics != null && btnLyrics.Enabled)
                    {
                        ToggleLyrics();
                        return true;
                    }
                    break;

                case Keys.OemOpenBrackets:
                    if (!searchPillBox.ContainsFocus && lyricsPanel != null)
                    {
                        int newOffset = HistoryService.LyricsDelayOffsetMs - 50;
                        if (newOffset < -500) newOffset = -500;
                        HistoryService.LyricsDelayOffsetMs = newOffset;
                        lyricsPanel.DelayOffsetMs = newOffset;
                        _audioPlayer.RaiseStatus(string.Format("⏱️ Độ trễ lời: {0}ms {1}", newOffset, newOffset >= 150 ? "(Khớp tai nghe Bluetooth)" : ""));
                        return true;
                    }
                    break;

                case Keys.OemCloseBrackets:
                    if (!searchPillBox.ContainsFocus && lyricsPanel != null)
                    {
                        int newOffset = HistoryService.LyricsDelayOffsetMs + 50;
                        if (newOffset > 1000) newOffset = 1000;
                        HistoryService.LyricsDelayOffsetMs = newOffset;
                        lyricsPanel.DelayOffsetMs = newOffset;
                        _audioPlayer.RaiseStatus(string.Format("⏱️ Độ trễ lời: +{0}ms {1}", newOffset, newOffset >= 150 ? "(Khớp tai nghe Bluetooth)" : ""));
                        return true;
                    }
                    break;

                case Keys.Escape:
                    if (playlistPopup != null && playlistPopup.Visible)
                    {
                        playlistPopup.Hide();
                        btnPlaylist.IsOpen = false;
                        return true;
                    }
                    if (volumePopup != null && volumePopup.Visible)
                    {
                        volumePopup.Visible = false;
                        return true;
                    }
                    if (_isLyricsOpen)
                    {
                        ToggleLyrics(false);
                        return true;
                    }
                    break;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private float _previousNonZeroVolume = 1.0f;
        private void ToggleMute()
        {
            if (_audioPlayer == null) return;
            if (_audioPlayer.Volume > 0.001f)
            {
                _previousNonZeroVolume = _audioPlayer.Volume;
                _audioPlayer.Volume = 0f;
                btnVolume.Volume = 0f;
                volumePopup.Volume = 0f;
            }
            else
            {
                float target = _previousNonZeroVolume > 0.05f ? _previousNonZeroVolume : 0.5f;
                _audioPlayer.Volume = target;
                btnVolume.Volume = target;
                volumePopup.Volume = target;
            }
        }

        private void AdjustVolume(float delta)
        {
            if (_audioPlayer == null) return;
            float newVol = Math.Max(0.0f, Math.Min(1.0f, _audioPlayer.Volume + delta));
            _audioPlayer.Volume = newVol;
            btnVolume.Volume = newVol;
            volumePopup.Volume = newVol;
        }

        private void SeekRelative(double seconds)
        {
            if (_audioPlayer != null && _audioPlayer.HasTrack && _audioPlayer.TotalDuration > TimeSpan.Zero)
            {
                double currentSec = _audioPlayer.Position.TotalSeconds;
                double targetSec = Math.Max(0, Math.Min(_audioPlayer.TotalDuration.TotalSeconds, currentSec + seconds));
                _audioPlayer.Position = TimeSpan.FromSeconds(targetSec);
                artworkBox.TimeText = FormatTimeSpan(_audioPlayer.Position, _audioPlayer.TotalDuration);
                if (lyricsPanel != null && lyricsPanel.Visible)
                {
                    lyricsPanel.CurrentPosition = _audioPlayer.Position;
                }
                musicTrackBar.Value = (float)(targetSec / _audioPlayer.TotalDuration.TotalSeconds);
            }
        }

        private void Window_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (WindowState == FormWindowState.Maximized)
                {
                    _isMaximizedDragTracking = true;
                    _maximizedDragStartPos = Cursor.Position;
                    return;
                }

                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void AudioPlayer_StatusMessageChanged(object sender, string status)
        {
            if (_audioPlayer == null) return;
            if (string.IsNullOrEmpty(status))
            {
                if (!string.IsNullOrEmpty(_audioPlayer.CurrentTitle))
                {
                    marqueeTitle.Text = _audioPlayer.CurrentTitle;
                }
                else
                {
                    marqueeTitle.Text = "Chưa phát hiện Audio";
                }
            }
            else
            {
                // Khi dang phat hoac da co track, khong ghi de thong bao loading len tieu de bai hat
                if (_audioPlayer.HasTrack || _audioPlayer.IsPlaying)
                {
                    if (!string.IsNullOrEmpty(_audioPlayer.CurrentTitle))
                    {
                        marqueeTitle.Text = _audioPlayer.CurrentTitle;
                    }
                    else if (!string.IsNullOrEmpty(_audioPlayer.CurrentVideoId))
                    {
                        marqueeTitle.Text = "YouTube: " + _audioPlayer.CurrentVideoId;
                    }
                }
                else
                {
                    marqueeTitle.Text = status;
                }
            }
        }

        private void AudioPlayer_ThumbnailUrlReady(object sender, string thumbUrl)
        {
            artworkBox.LoadFromUrlAsync(thumbUrl);
        }

        private void AudioPlayer_PlaybackStateChanged(object sender, EventArgs e)
        {
            if (_audioPlayer == null) return;
            UpdateAudioControlsEnabledState();
            btnPlayPause.IsPlaying = _audioPlayer.IsPlaying;
            playbackTimer.Enabled = _audioPlayer.IsPlaying;
            artworkBox.IsSpinning = _audioPlayer.IsPlaying;
            if (_audioPlayer.HasTrack)
            {
                artworkBox.TimeText = FormatTimeSpan(_audioPlayer.Position, _audioPlayer.TotalDuration);
                string titleToDisplay = !string.IsNullOrWhiteSpace(_audioPlayer.CurrentTitle)
                    ? _audioPlayer.CurrentTitle
                    : (!string.IsNullOrWhiteSpace(_audioPlayer.CurrentVideoId) ? "YouTube: " + _audioPlayer.CurrentVideoId : "Đang phát nhạc");
                if (marqueeTitle.Text != titleToDisplay)
                {
                    marqueeTitle.Text = titleToDisplay;
                    UpdateTrayTooltip(titleToDisplay);
                }
            }
        }

        private void AudioPlayer_PlaybackEnded(object sender, EventArgs e)
        {
            _audioPlayer.Position = TimeSpan.Zero;
            UpdateAudioControlsEnabledState();
            musicTrackBar.Value = 0f;
            btnPlayPause.IsPlaying = false;
            playbackTimer.Enabled = false;
            artworkBox.IsSpinning = false;
            if (_audioPlayer.HasTrack)
            {
                artworkBox.TimeText = FormatTimeSpan(TimeSpan.Zero, _audioPlayer.TotalDuration);
            }
            if (!string.IsNullOrEmpty(_audioPlayer.CurrentTitle))
            {
                marqueeTitle.Text = _audioPlayer.CurrentTitle;
            }
        }

        private void AudioPlayer_PlaybackLooped(object sender, EventArgs e)
        {
            musicTrackBar.Value = 0f;
            if (_audioPlayer.HasTrack)
            {
                artworkBox.TimeText = FormatTimeSpan(TimeSpan.Zero, _audioPlayer.TotalDuration);
            }
        }

        private void BtnPlayPause_Click(object sender, EventArgs e)
        {
            // If audio track is already loaded, toggle play/pause directly
            if (_audioPlayer.HasTrack)
            {
                _audioPlayer.TogglePlayPause();
            }
            else
            {
                // If no track is loaded yet, check if user has pasted a link into searchPillBox
                string input = searchPillBox.Text.Trim();
                if (!string.IsNullOrEmpty(input) && input != "link youtube..." && input != "linkYT" &&
                    (searchPillBox == null || (input != searchPillBox.PlaceholderText && !input.StartsWith("Tìm bài hát"))))
                {
                    // Automatically load and play the link in the search box!
                    SearchPillBox_UrlSubmitted(searchPillBox, input);
                }
                else
                {
                    btnPlayPause.IsPlaying = false;
                    marqueeTitle.Text = "Chưa phát hiện Audio";
                }
            }
        }


        private void UpdateAudioControlsEnabledState()
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(UpdateAudioControlsEnabledState)); } catch { }
                return;
            }

            bool hasAudio = _audioPlayer != null && (_audioPlayer.HasTrack || !string.IsNullOrEmpty(_audioPlayer.CurrentVideoId) || _audioPlayer.IsLoading);

            if (btnPrevious != null) btnPrevious.Enabled = hasAudio;
            if (btnPlayPause != null) btnPlayPause.Enabled = hasAudio;
            if (btnNext != null) btnNext.Enabled = hasAudio;
            if (btnFavorite != null) btnFavorite.Enabled = hasAudio;
            if (btnQuickDownload != null) btnQuickDownload.Enabled = hasAudio;
            if (btnThumbnailMenu != null) btnThumbnailMenu.Enabled = hasAudio;
            if (musicTrackBar != null)
            {
                musicTrackBar.Enabled = hasAudio;
                if (!hasAudio) musicTrackBar.Value = 0f;
            }

            if (!hasAudio)
            {
                if (thumbnailActionPopup != null && thumbnailActionPopup.Visible)
                {
                    thumbnailActionPopup.Visible = false;
                }
                if (_collectionPickerPopup != null && _collectionPickerPopup.Visible)
                {
                    _collectionPickerPopup.Visible = false;
                }
                if (btnThumbnailMenu != null)
                {
                    btnThumbnailMenu.IsOpen = false;
                }
            }
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (_isUserResizing) return;
            if (_audioPlayer == null) return;
            // Only update trackbar position when audio is actually loaded and playing
            if (_audioPlayer.HasTrack)
            {
                if (!musicTrackBar.IsDragging && _audioPlayer.IsPlaying && _audioPlayer.TotalDuration > TimeSpan.Zero)
                {
                    musicTrackBar.Value = _audioPlayer.Progress;
                }
                artworkBox.TimeText = FormatTimeSpan(_audioPlayer.Position, _audioPlayer.TotalDuration);
                if (lyricsPanel != null && lyricsPanel.Visible)
                {
                    if (_audioPlayer.TotalDuration > TimeSpan.Zero && lyricsPanel.TotalDuration != _audioPlayer.TotalDuration)
                    {
                        lyricsPanel.TotalDuration = _audioPlayer.TotalDuration;
                    }
                    lyricsPanel.CurrentPosition = _audioPlayer.Position;
                }
            }
        }

        private void MusicTrackBar_SeekCommitted(object sender, float val)
        {
            if (_audioPlayer.HasTrack)
            {
                _audioPlayer.SeekNormalized(val);
                artworkBox.TimeText = FormatTimeSpan(_audioPlayer.Position, _audioPlayer.TotalDuration);
                if (lyricsPanel != null && lyricsPanel.Visible)
                {
                    if (_audioPlayer.TotalDuration > TimeSpan.Zero && lyricsPanel.TotalDuration != _audioPlayer.TotalDuration)
                    {
                        lyricsPanel.TotalDuration = _audioPlayer.TotalDuration;
                    }
                    lyricsPanel.CurrentPosition = _audioPlayer.Position;
                }
            }
        }

        private void BtnPrevious_Click(object sender, EventArgs e)
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.PlayPrevious();
                if (_audioPlayer.CurrentPlaylistIndex <= 0)
                {
                    musicTrackBar.Value = 0f;
                    artworkBox.TimeText = FormatTimeSpan(TimeSpan.Zero, _audioPlayer.TotalDuration);
                }
            }
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (_audioPlayer.HasPlaylist && _audioPlayer.CurrentPlaylistIndex >= 0)
            {
                _audioPlayer.PlayNext();
            }
            else if (_audioPlayer.HasTrack)
            {
                _audioPlayer.Position = TimeSpan.Zero;
                musicTrackBar.Value = 0f;
                artworkBox.TimeText = FormatTimeSpan(TimeSpan.Zero, _audioPlayer.TotalDuration);
            }
        }

        private void SearchPillBox_UrlSubmitted(object sender, string url)
        {
            _audioPlayer.IsPlayingFromHistory = false;
            _audioPlayer.CurrentQueueSource = ActiveQueueSource.Playlist;
            if (string.IsNullOrWhiteSpace(url) || url == "link youtube..." || url == "linkYT" ||
                (searchPillBox != null && (url == searchPillBox.PlaceholderText || url.StartsWith("Tìm bài hát"))))
            {
                _audioPlayer.StopAndClear();
                _audioPlayer.ClearPlaylist();
                artworkBox.ClearImage();
                artworkBox.TimeText = string.Empty;
                musicTrackBar.Value = 0f;
                btnPlayPause.IsPlaying = false;
                playbackTimer.Stop();
                marqueeTitle.Text = "Chưa phát hiện Audio";
                marqueeAuthor.Text = string.Empty;
                btnPlaylist.HasPlaylist = false;
                btnPlaylist.IsOpen = false;
                if (btnLyrics != null)
                {
                    btnLyrics.HasLyrics = false;
                    btnLyrics.IsOpen = false;
                }
                if (_isLyricsOpen)
                {
                    ToggleLyrics(false);
                }
                if (lyricsPanel != null)
                {
                    lyricsPanel.Lyrics = null;
                }
                playlistPopup.Visible = false;
                playlistPopup.Clear();
                return;
            }

            string videoId = AudioPlayerService.ExtractVideoId(url);
            string listId = AudioPlayerService.ExtractListId(url);

            // 1. If the video exists in the currently loaded playlist:
            var currentPl = _audioPlayer.Playlist;
            if (!string.IsNullOrEmpty(videoId) && currentPl != null && currentPl.Count > 0)
            {
                int existingIdx = currentPl.FindIndex(x => x.Id == videoId);
                if (existingIdx >= 0 && existingIdx < currentPl.Count)
                {
                    // If no playlist ID in URL or same playlist ID as current:
                    if (string.IsNullOrEmpty(listId) || string.Equals(listId, _audioPlayer.CurrentPlaylistId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Play this track directly from current playlist and highlight it!
                        _audioPlayer.PlayTrackAtIndex(existingIdx);
                        return;
                    }
                }
            }

            // 2. If a playlist is specified in URL:
            if (!string.IsNullOrEmpty(listId))
            {
                if (!string.Equals(listId, _audioPlayer.CurrentPlaylistId, StringComparison.OrdinalIgnoreCase))
                {
                    btnPlaylist.HasPlaylist = true;
                    playlistPopup.StartLoading();
                }

                if (!string.IsNullOrEmpty(videoId))
                {
                    // User submitted a link containing both a track and a playlist -> play track immediately and queue playlist
                    _audioPlayer.EnqueueAndPlayTrack(videoId, null, null, 0, url);
                    _audioPlayer.LoadPlaylistQueueOnly(url);
                    return;
                }

                // If only a pure playlist URL was entered and something is currently playing, load queue in background
                if (_audioPlayer.IsPlaying || !string.IsNullOrEmpty(_audioPlayer.CurrentVideoId))
                {
                    _audioPlayer.LoadPlaylistQueueOnly(url);
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(videoId))
            {
                btnPlaylist.HasPlaylist = true;
                _audioPlayer.EnqueueAndPlayTrack(videoId, null, null, 0, url);
                return;
            }
            else
            {
                // Standalone track -> DO NOT wipe out user's session playlist in Tab 0!
                bool hasPl = (_audioPlayer.HasSessionPlaylist || (playlistPopup.PlaylistItems != null && playlistPopup.PlaylistItems.Count > 0));
                btnPlaylist.HasPlaylist = hasPl;
            }

            // Immediately load audio in background
            _audioPlayer.LoadAndPlay(url);
        }

        private string FormatTimeSpan(TimeSpan current, TimeSpan total)
        {
            if (total > TimeSpan.Zero && current > total)
            {
                current = total;
            }
            if (total.TotalHours >= 1)
            {
                return string.Format("{0:h\\:mm\\:ss} / {1:h\\:mm\\:ss}", current, total);
            }
            return string.Format("{0:mm\\:ss} / {1:mm\\:ss}", current, total);
        }

        private void InitializeLyrics()
        {
            _lyricsSlideTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _lyricsSlideTimer.Tick += LyricsSlideTimer_Tick;

            if (lyricsPanel != null)
            {
                lyricsPanel.SeekRequested += (targetTime) =>
                {
                    if (_audioPlayer != null && _audioPlayer.HasTrack && _audioPlayer.TotalDuration > TimeSpan.Zero)
                    {
                        _audioPlayer.Position = targetTime;
                        artworkBox.TimeText = FormatTimeSpan(targetTime, _audioPlayer.TotalDuration);
                        lyricsPanel.CurrentPosition = targetTime;
                    }
                };

                lyricsPanel.ReloadRequested += (s, e) =>
                {
                    if (_audioPlayer != null && _audioPlayer.HasTrack)
                    {
                        string vid = _currentLyricsVideoId ?? _audioPlayer.CurrentVideoId;
                        string tit = !string.IsNullOrWhiteSpace(_audioPlayer.CurrentTitle) ? _audioPlayer.CurrentTitle : marqueeTitle.Text;
                        string aut = !string.IsNullOrWhiteSpace(_audioPlayer.CurrentAuthor) ? _audioPlayer.CurrentAuthor : marqueeAuthor.Text;
                        int dur = (int)(_audioPlayer.TotalDuration.TotalSeconds > 0 ? _audioPlayer.TotalDuration.TotalSeconds : 0);
                        if (!string.IsNullOrEmpty(vid))
                        {
                            _audioPlayer.RaiseStatus("Đang tải lại lời bài hát từ mạng...");
                            LoadLyricsForTrack(vid, tit, aut, dur, forceRefresh: true);
                        }
                    }
                };
            }
        }

        private void ToggleLyrics(bool? forceOpen = null)
        {
            bool target = forceOpen.HasValue ? forceOpen.Value : !_isLyricsOpen;

            // Cannot open if lyrics are not available
            if (target && (btnLyrics == null || !btnLyrics.Enabled || lyricsPanel == null || lyricsPanel.Lyrics == null || !lyricsPanel.Lyrics.HasLyrics))
            {
                return;
            }

            if (_isLyricsOpen == target && _lyricsSlideTargetOpen == target) return;

            _lyricsSlideTargetOpen = target;
            _isLyricsOpen = target;

            if (btnLyrics != null)
            {
                btnLyrics.IsOpen = _isLyricsOpen;
            }

            if (_lyricsSlideTargetOpen && lyricsPanel != null)
            {
                lyricsPanel.Visible = true;
                if (_audioPlayer != null && _audioPlayer.TotalDuration > TimeSpan.Zero)
                {
                    lyricsPanel.TotalDuration = _audioPlayer.TotalDuration;
                    lyricsPanel.CurrentPosition = _audioPlayer.Position;
                }
                if (_searchResultsView != null && _searchResultsView.Visible)
                {
                    _searchResultsView.BringToFront();
                }
            }

            if (_lyricsSlideTimer != null && !_lyricsSlideTimer.Enabled)
            {
                _lyricsSlideTimer.Start();
            }
        }

        private void LyricsSlideTimer_Tick(object sender, EventArgs e)
        {
            float step = 0.14f;
            if (_lyricsSlideTargetOpen)
            {
                _lyricsSlideProgress += step;
                if (_lyricsSlideProgress >= 1f)
                {
                    _lyricsSlideProgress = 1f;
                    _lyricsSlideTimer.Stop();
                }
            }
            else
            {
                _lyricsSlideProgress -= step;
                if (_lyricsSlideProgress <= 0f)
                {
                    _lyricsSlideProgress = 0f;
                    _lyricsSlideTimer.Stop();
                }
            }

            // Ease-out cubic: 1 - (1 - t)^3
            float t = _lyricsSlideProgress;
            float ease = (float)(1 - Math.Pow(1 - t, 3));

            UpdateCenterAreaLayout(ease);
            if (_searchResultsView != null && _searchResultsView.Visible)
            {
                _searchResultsView.BringToFront();
            }
        }

        private void LoadLyricsForTrack(string videoId, string title, string author, int durationSeconds, bool forceRefresh = false)
        {
            _currentLyricsVideoId = videoId;

            if (btnLyrics != null)
            {
                btnLyrics.IsLoading = true;
                btnLyrics.IsOpen = _isLyricsOpen;
            }

            if (lyricsPanel != null)
            {
                lyricsPanel.IsLoading = true;
                if (forceRefresh) lyricsPanel.Lyrics = null;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(async delegate
            {
                try
                {
                    var lyricsData = await LyricsService.Instance.FetchLyricsAsync(videoId, title, author, durationSeconds, forceRefresh);
                    if (_currentLyricsVideoId == videoId && !IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(delegate
                        {
                            if (btnLyrics != null) btnLyrics.IsLoading = false;
                            if (lyricsPanel != null) lyricsPanel.IsLoading = false;

                            if (lyricsData != null && lyricsData.HasLyrics)
                            {
                                if (btnLyrics != null)
                                {
                                    btnLyrics.HasLyrics = true;
                                    btnLyrics.IsOpen = _isLyricsOpen;
                                }
                                if (lyricsPanel != null)
                                {
                                    if (_audioPlayer != null && _audioPlayer.TotalDuration > TimeSpan.Zero)
                                    {
                                        lyricsPanel.TotalDuration = _audioPlayer.TotalDuration;
                                    }
                                    else if (durationSeconds > 0)
                                    {
                                        lyricsPanel.TotalDuration = TimeSpan.FromSeconds(durationSeconds);
                                    }
                                    lyricsPanel.Lyrics = lyricsData;
                                    if (_audioPlayer != null)
                                    {
                                        lyricsPanel.CurrentPosition = _audioPlayer.Position;
                                    }
                                }
                            }
                            else
                            {
                                if (btnLyrics != null)
                                {
                                    btnLyrics.HasLyrics = false;
                                }
                                if (lyricsPanel != null)
                                {
                                    lyricsPanel.Lyrics = null;
                                }
                                if (_isLyricsOpen)
                                {
                                    ToggleLyrics(false);
                                }
                            }
                        }));
                    }

                    // Pre-fetch next track in queue
                    TryPrefetchNextTrackLyrics();
                }
                catch { }
            });
        }

        public void NotifyLyricsUpdated(string videoId)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => NotifyLyricsUpdated(videoId)));
                return;
            }

            if (_currentLyricsVideoId == videoId && lyricsPanel != null)
            {
                lyricsPanel.RebuildAndRefresh();
            }
        }

        private void TryPrefetchNextTrackLyrics()
        {
            try
            {
                var pl = _audioPlayer?.Playlist;
                int curIdx = _audioPlayer != null ? _audioPlayer.CurrentPlaylistIndex : -1;
                if (pl != null && curIdx >= 0 && curIdx + 1 < pl.Count)
                {
                    var next = pl[curIdx + 1];
                    if (next != null && !string.IsNullOrEmpty(next.Id))
                    {
                        LyricsService.Instance.PrefetchLyricsAsync(next.Id, next.Title, next.Author, next.DurationSeconds);
                    }
                }
            }
            catch { }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 16, 38)))
            {
                e.Graphics.FillRectangle(bg, e.ClipRectangle);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 16, 38)))
            {
                e.Graphics.FillRectangle(bg, e.ClipRectangle);
            }
            base.OnPaint(e);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000; // WS_MINIMIZEBOX (enables minimize from taskbar)
                cp.Style |= 0x10000; // WS_MAXIMIZEBOX
                cp.Style |= 0x80000; // WS_SYSMENU (enables Alt+F4 and standard close signals)
                return cp;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _isFormClosed = true;
            try { this.Hide(); } catch { }
            if (playlistPopup != null) try { playlistPopup.Hide(); } catch { }
            if (accountPopup != null) try { accountPopup.Hide(); } catch { }
            base.OnFormClosing(e);
            try { FirebaseSyncService.Instance.FlushSyncOnExit(); } catch { }
            try { SavedTracksService.Instance.CancelAllDownloads(); } catch { }
            Application.RemoveMessageFilter(this);

            try { ThumbnailManager.ClearMemory(); } catch { }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                try { _trayIcon.Dispose(); } catch { }
                _trayIcon = null;
            }
            if (_trayMenu != null)
            {
                try { _trayMenu.Dispose(); } catch { }
                _trayMenu = null;
            }
            _trayItemLoop = null;

            if (_ramTimer != null)
            {
                try { _ramTimer.Stop(); _ramTimer.Dispose(); } catch { }
                _ramTimer = null;
            }
            if (_lyricsSlideTimer != null)
            {
                try { _lyricsSlideTimer.Stop(); _lyricsSlideTimer.Dispose(); } catch { }
                _lyricsSlideTimer = null;
            }
            if (playbackTimer != null)
            {
                try { playbackTimer.Stop(); } catch { }
            }
            if (artworkBox != null)
            {
                artworkBox.ClearImage();
            }
            if (playlistPopup != null)
            {
                try { playlistPopup.Dispose(); } catch { }
                playlistPopup = null;
            }
            if (accountPopup != null)
            {
                try { accountPopup.Dispose(); } catch { }
                accountPopup = null;
            }
            if (_audioPlayer != null)
            {
                _audioPlayer.Dispose();
                _audioPlayer = null;
            }
            if (_themeTransitionTimer != null)
            {
                try { _themeTransitionTimer.Stop(); _themeTransitionTimer.Dispose(); } catch { }
                _themeTransitionTimer = null;
            }
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            try { Environment.Exit(0); } catch { }
        }


        private void panelPlayerCard_Paint(object sender, PaintEventArgs e)
        {
            int w = panelPlayerCard.ClientSize.Width;
            int h = panelPlayerCard.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;

            if (_isMaximizedMode)
            {
                using (LinearGradientBrush lgb = new LinearGradientBrush(
                    new Rectangle(0, 0, w, h),
                    Color.Black, Color.Black,
                    LinearGradientMode.Vertical))
                {
                    ColorBlend cb = new ColorBlend(_currentPalette.MaximizedColors.Length);
                    cb.Colors = _currentPalette.MaximizedColors;
                    cb.Positions = _currentPalette.MaximizedPositions;
                    lgb.InterpolationColors = cb;
                    e.Graphics.FillRectangle(lgb, 0, 0, w, h);
                }
            }
            else
            {
                Color topColor = _currentPalette.CompactTop;
                Color bottomColor = _currentPalette.CompactBottom;

                using (LinearGradientBrush lgb = new LinearGradientBrush(
                    new Rectangle(0, 0, w, h),
                    topColor,
                    bottomColor,
                    LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(lgb, 0, 0, w, h);
                }
            }
        }

        private void ArtworkBox_ArtworkChanged(object sender, Image image)
        {
            if (image == null)
            {
                TransitionToTheme(ColorExtractorService.GetDefaultTheme());
                return;
            }

            // Downscale safely on the UI thread to a tiny 24x24 bitmap (instant: ~0.15ms)
            Bitmap tinySample = null;
            try
            {
                tinySample = new Bitmap(24, 24, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(tinySample))
                {
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                    g.DrawImage(image, 0, 0, 24, 24);
                }
            }
            catch
            {
                if (tinySample != null)
                {
                    try { tinySample.Dispose(); } catch { }
                    tinySample = null;
                }
            }

            if (tinySample == null)
            {
                TransitionToTheme(ColorExtractorService.GetDefaultTheme());
                return;
            }

            // Extract theme asynchronously in background (takes ~0.05ms) so UI never hitches
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                DynamicThemePalette palette = null;
                try
                {
                    palette = ColorExtractorService.ExtractThemeFromBitmap(tinySample);
                }
                catch { }
                finally
                {
                    try { tinySample.Dispose(); } catch { }
                }

                if (palette != null && IsHandleCreated && !_isFormClosed)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (!_isFormClosed)
                            {
                                TransitionToTheme(palette);
                            }
                        }));
                    }
                    catch { }
                }
            });
        }

        private void TransitionToTheme(DynamicThemePalette newPalette)
        {
            if (newPalette == null) newPalette = ColorExtractorService.GetDefaultTheme();
            _targetPalette = newPalette;
            _sourcePalette = _currentPalette.Clone();
            _themeTransitionStep = 0;

            if (_themeTransitionTimer == null)
            {
                _themeTransitionTimer = new System.Windows.Forms.Timer();
                _themeTransitionTimer.Interval = 16;
                _themeTransitionTimer.Tick += ThemeTransitionTimer_Tick;
            }

            _themeTransitionTimer.Stop();
            _themeTransitionTimer.Start();
        }

        private void ThemeTransitionTimer_Tick(object sender, EventArgs e)
        {
            _themeTransitionStep++;
            float t = (float)_themeTransitionStep / ThemeTransitionTotalSteps;

            if (t >= 1.0f)
            {
                t = 1.0f;
                _themeTransitionTimer.Stop();
                _currentPalette = _targetPalette.Clone();
            }
            else
            {
                float smoothT = t * t * (3f - 2f * t); // Smoothstep curve
                _currentPalette = ColorExtractorService.LerpPalette(_sourcePalette, _targetPalette, smoothT);
            }

            ApplyCurrentPaletteToUI();
        }

        private void ApplyCurrentPaletteToUI()
        {
            if (_currentPalette == null) return;
            ColorExtractorService.CurrentTheme = _currentPalette;

            // Compact mode colors
            gradientPanel.TopColor = _currentPalette.CompactTop;
            gradientPanel.BottomColor = _currentPalette.CompactBottom;

            // Maximized card borders
            if (_topSectionCard != null)
            {
                _topSectionCard.CardBorderColor = Color.FromArgb(140, _currentPalette.SecondaryAccentColor);
            }
            if (_lyricsCard != null)
            {
                _lyricsCard.BorderGradientColor1 = _currentPalette.SecondaryAccentColor;
                _lyricsCard.BorderGradientColor2 = _currentPalette.AccentColor;
            }

            _sideMenuPanel?.SetTheme(_currentPalette);
            _activeQueuePanel?.SetTheme(_currentPalette);
            searchPillBox?.SetTheme(_currentPalette);
            lyricsPanel?.SetTheme(_currentPalette);
            if (btnThumbnailMenu != null) btnThumbnailMenu.IconColor = _currentPalette.AccentColor;
            btnFavorite?.SetTheme(_currentPalette);
            volumePopup?.SetTheme(_currentPalette);
            playlistPopup?.SetTheme(_currentPalette);
            _searchResultsView?.SetTheme(_currentPalette);
            _searchDropdown?.SetTheme(_currentPalette);
            _collectionPickerPopup?.SetTheme(_currentPalette);
            thumbnailActionPopup?.SetTheme(_currentPalette);

            // Buttons theme gradient
            btnPlayPause.ThemeGradientTop = _currentPalette.ButtonGradientTop;
            btnPlayPause.ThemeGradientBottom = _currentPalette.ButtonGradientBottom;
            btnPrevious.ThemeGradientTop = _currentPalette.ButtonGradientTop;
            btnPrevious.ThemeGradientBottom = _currentPalette.ButtonGradientBottom;
            btnNext.ThemeGradientTop = _currentPalette.ButtonGradientTop;
            btnNext.ThemeGradientBottom = _currentPalette.ButtonGradientBottom;

            if (_isMaximizedMode)
            {
                musicTrackBar.PlayedColor = _currentPalette.AccentColor;
                btnVolume.IconColor = _currentPalette.AccentColor;
                btnVolume.HoverColor = ColorExtractorService.Lerp(_currentPalette.AccentColor, Color.White, 0.4f);
                btnLoop.InactiveColor = _currentPalette.AccentColor;
                btnLoop.ActiveColor = ColorExtractorService.Lerp(_currentPalette.AccentColor, Color.White, 0.2f);
                if (btnTimer != null)
                {
                    btnTimer.InactiveColor = _currentPalette.AccentColor;
                    btnTimer.ActiveColor = ColorExtractorService.Lerp(_currentPalette.AccentColor, Color.White, 0.2f);
                }
            }
            else
            {
                musicTrackBar.PlayedColor = _currentPalette.AccentColor;
                btnPlayPause.IconColor = _currentPalette.AccentColor;
                btnLoop.ActiveColor = _currentPalette.AccentColor;
                if (btnTimer != null) btnTimer.ActiveColor = _currentPalette.AccentColor;
                if (btnPlaylist != null) btnPlaylist.ActiveColor = _currentPalette.AccentColor;
                if (btnLyrics != null) btnLyrics.ActiveColor = _currentPalette.AccentColor;
            }

            panelPlayerCard.Invalidate();
        }

        private void UpdateThemePanels()
        {
            panelTopBar.BackColor = Color.Transparent;
            gradientPanel.BackColor = Color.Transparent;
            panelBottomControls.BackColor = Color.Transparent;

            panelPlayerCard.Invalidate();
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _trayMenu = new ContextMenuStrip();
                _trayMenu.ShowImageMargin = false;
                _trayMenu.ShowCheckMargin = false;
                _trayMenu.MinimumSize = new Size(165, 0);
                _trayMenu.BackColor = Color.FromArgb(24, 24, 28);
                _trayMenu.ForeColor = Color.FromArgb(235, 235, 235);
                _trayMenu.Renderer = new DarkMenuRenderer();
                _trayMenu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

                var itemOpen = new ToolStripMenuItem("M\u1edf Kahoii Music");
                itemOpen.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                itemOpen.Click += (s, e) => RestoreFromTray();

                var itemPlayPause = new ToolStripMenuItem("Ph\u00e1t / T\u1ea1m d\u1eebng");
                itemPlayPause.Click += (s, e) =>
                {
                    if (_audioPlayer != null && _audioPlayer.HasTrack)
                    {
                        _audioPlayer.TogglePlayPause();
                    }
                    else
                    {
                        RestoreFromTray();
                    }
                };

                var itemNext = new ToolStripMenuItem("B\u00e0i ti\u1ebfp theo");
                itemNext.Click += (s, e) =>
                {
                    if (_audioPlayer != null && _audioPlayer.HasPlaylist)
                    {
                        _audioPlayer.PlayNext();
                    }
                };

                var itemPrevious = new ToolStripMenuItem("B\u00e0i tr\u01b0\u1edbc");
                itemPrevious.Click += (s, e) =>
                {
                    if (_audioPlayer != null && _audioPlayer.HasPlaylist)
                    {
                        _audioPlayer.PlayPrevious();
                    }
                };

                _trayItemLoop = new ToolStripMenuItem("L\u1eb7p l\u1ea1i");
                _trayItemLoop.CheckOnClick = true;
                _trayItemLoop.Checked = (btnLoop != null) ? btnLoop.IsLooping : (_audioPlayer != null && _audioPlayer.IsLooping);
                _trayItemLoop.Click += (s, e) =>
                {
                    if (btnLoop != null)
                    {
                        btnLoop.CycleNextMode();
                        _audioPlayer.LoopMode = btnLoop.Mode;
                        _trayItemLoop.Checked = btnLoop.IsLooping;
                        _trayItemLoop.Text = GetLoopMenuText(btnLoop.Mode);
                        UpdateLoopTooltip();
                    }
                };

                var itemExit = new ToolStripMenuItem("Tho\u00e1t");
                itemExit.Click += (s, e) => Close();

                _trayMenu.Items.Add(itemOpen);
                _trayMenu.Items.Add(new ToolStripSeparator());
                _trayMenu.Items.Add(itemPlayPause);
                _trayMenu.Items.Add(itemNext);
                _trayMenu.Items.Add(itemPrevious);
                var trayItemTimer = new ToolStripMenuItem("H\u1eb9n gi\u1edd t\u1eaft");
                Action<string, KahoiiMusic.Services.SleepTimerOption> addTrayTimer = (lbl, opt) =>
                {
                    var optItem = new ToolStripMenuItem(lbl);
                    optItem.Click += (s, e) => KahoiiMusic.Services.SleepTimerService.Instance.SetTimer(opt);
                    trayItemTimer.DropDownItems.Add(optItem);
                };
                addTrayTimer("5 ph\u00fat", KahoiiMusic.Services.SleepTimerOption.Minutes5);
                addTrayTimer("10 ph\u00fat", KahoiiMusic.Services.SleepTimerOption.Minutes10);
                addTrayTimer("15 ph\u00fat", KahoiiMusic.Services.SleepTimerOption.Minutes15);
                addTrayTimer("20 ph\u00fat", KahoiiMusic.Services.SleepTimerOption.Minutes20);
                addTrayTimer("30 ph\u00fat", KahoiiMusic.Services.SleepTimerOption.Minutes30);
                addTrayTimer("45 ph\u00fat", KahoiiMusic.Services.SleepTimerOption.Minutes45);
                addTrayTimer("60 ph\u00fat", KahoiiMusic.Services.SleepTimerOption.Minutes60);
                trayItemTimer.DropDownItems.Add(new ToolStripSeparator());
                addTrayTimer("H\u1ebft b\u00e0i h\u00e1t", KahoiiMusic.Services.SleepTimerOption.EndOfTrack);
                trayItemTimer.DropDownItems.Add(new ToolStripSeparator());
                addTrayTimer("T\u1eaft h\u1eb9n gi\u1edd", KahoiiMusic.Services.SleepTimerOption.Off);

                var itemCheckUpdate = new ToolStripMenuItem("Kiểm tra bản cập nhật...");
                itemCheckUpdate.Click += async (s, e) => await CheckForAppUpdatesAsync(manual: true);

                _trayMenu.Items.Add(_trayItemLoop);
                _trayMenu.Items.Add(trayItemTimer);
                _trayMenu.Items.Add(new ToolStripSeparator());
                _trayMenu.Items.Add(itemCheckUpdate);
                _trayMenu.Items.Add(new ToolStripSeparator());
                _trayMenu.Items.Add(itemExit);

                _trayMenu.Opening += (s, e) =>
                {
                    if (_audioPlayer != null && _audioPlayer.IsPlaying)
                    {
                        itemPlayPause.Text = "T\u1ea1m d\u1eebng";
                    }
                    else
                    {
                        itemPlayPause.Text = "Ph\u00e1t ti\u1ebfp";
                    }

                    bool hasPlaylist = (_audioPlayer != null && _audioPlayer.HasPlaylist);
                    itemNext.Enabled = hasPlaylist;
                    itemPrevious.Enabled = hasPlaylist;

                    if (_trayItemLoop != null)
                    {
                        _trayItemLoop.Checked = (btnLoop != null) ? btnLoop.IsLooping : (_audioPlayer != null && _audioPlayer.IsLooping);
                    }
                    if (trayItemTimer != null)
                    {
                        trayItemTimer.Text = KahoiiMusic.Services.SleepTimerService.Instance.IsActive ? KahoiiMusic.Services.SleepTimerService.Instance.GetStatusString() : "H\u1eb9n gi\u1edd t\u1eaft";
                        foreach (ToolStripItem it in trayItemTimer.DropDownItems)
                        {
                            if (it is ToolStripMenuItem mi)
                            {
                                if (mi.Text.StartsWith("5")) mi.Checked = KahoiiMusic.Services.SleepTimerService.Instance.CurrentOption == KahoiiMusic.Services.SleepTimerOption.Minutes5;
                                else if (mi.Text.StartsWith("10")) mi.Checked = KahoiiMusic.Services.SleepTimerService.Instance.CurrentOption == KahoiiMusic.Services.SleepTimerOption.Minutes10;
                                else if (mi.Text.StartsWith("15")) mi.Checked = KahoiiMusic.Services.SleepTimerService.Instance.CurrentOption == KahoiiMusic.Services.SleepTimerOption.Minutes15;
                                else if (mi.Text.StartsWith("20")) mi.Checked = KahoiiMusic.Services.SleepTimerService.Instance.CurrentOption == KahoiiMusic.Services.SleepTimerOption.Minutes20;
                                else if (mi.Text.StartsWith("30")) mi.Checked = KahoiiMusic.Services.SleepTimerService.Instance.CurrentOption == KahoiiMusic.Services.SleepTimerOption.Minutes30;
                                else if (mi.Text.StartsWith("45")) mi.Checked = KahoiiMusic.Services.SleepTimerService.Instance.CurrentOption == KahoiiMusic.Services.SleepTimerOption.Minutes45;
                                else if (mi.Text.StartsWith("60")) mi.Checked = KahoiiMusic.Services.SleepTimerService.Instance.CurrentOption == KahoiiMusic.Services.SleepTimerOption.Minutes60;
                                else if (mi.Text.Contains("b\u00e0i")) mi.Checked = KahoiiMusic.Services.SleepTimerService.Instance.CurrentOption == KahoiiMusic.Services.SleepTimerOption.EndOfTrack;
                                else if (mi.Text.Contains("T\u1eaft")) mi.Checked = KahoiiMusic.Services.SleepTimerService.Instance.CurrentOption == KahoiiMusic.Services.SleepTimerOption.Off;
                            }
                        }
                    }
                };

                Icon appIcon = this.Icon;
                if (appIcon == null)
                {
                    try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
                }
                if (appIcon == null && picLogo != null && picLogo.Image is Bitmap bmp)
                {
                    try
                    {
                        using (Bitmap smallBmp = new Bitmap(bmp, new Size(32, 32)))
                        {
                            IntPtr hIcon = smallBmp.GetHicon();
                            appIcon = Icon.FromHandle(hIcon);
                        }
                    }
                    catch { }
                }

                _trayIcon = new NotifyIcon
                {
                    Text = "Kahoii Music",
                    Icon = appIcon ?? SystemIcons.Application,
                    ContextMenuStrip = _trayMenu,
                    Visible = true
                };

                _trayIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        RestoreFromTray();
                    }
                };

                _trayIcon.DoubleClick += (s, e) =>
                {
                    RestoreFromTray();
                };
            }
            catch { }
        }

        private void UpdateTrayTooltip(string title)
        {
            if (_trayIcon == null) return;
            try
            {
                string text = string.IsNullOrWhiteSpace(title) ? "Kahoii Music" : ("Kahoii Music - " + title);
                if (text.Length > 63)
                {
                    text = text.Substring(0, 60) + "...";
                }
                _trayIcon.Text = text;
            }
            catch { }
        }

        private void MinimizeToTray()
        {
            _wasMaximizedBeforeMinimize = _isMaximizedMode || (WindowState == FormWindowState.Maximized);
            HistoryService.IsMaximizedMode = _wasMaximizedBeforeMinimize;

            HideThumbnailPopups();
            if (playlistPopup != null && playlistPopup.Visible)
            {
                playlistPopup.Hide();
                btnPlaylist.IsOpen = false;
            }
            if (accountPopup != null && accountPopup.Visible)
            {
                accountPopup.Hide();
            }
            if (volumePopup != null && volumePopup.Visible)
            {
                volumePopup.Visible = false;
            }

            Hide();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = true;
            }

            // Immediately flush RAM to minimal working set
            MemoryOptimizer.TrimMemory(true);
            MemoryOptimizer.TrimMemoryAsync(50, true);
        }

        public void RestoreFromTray()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RestoreFromTray));
                return;
            }

            try
            {
                bool shouldBeMaximized = _wasMaximizedBeforeMinimize || HistoryService.IsMaximizedMode;

                Show();
                if (shouldBeMaximized)
                {
                    Screen scr = Screen.FromHandle(Handle);
                    MaximizedBounds = scr.WorkingArea;
                    WindowState = FormWindowState.Maximized;
                    _lastNonMinimizedState = FormWindowState.Maximized;
                    _wasMaximizedBeforeMinimize = true;
                    HistoryService.IsMaximizedMode = true;
                    if (btnMaximize != null) { btnMaximize.Invalidate(); }
                    ShowWindow(this.Handle, 3); // SW_MAXIMIZE
                }
                else
                {
                    WindowState = FormWindowState.Normal;
                    _lastNonMinimizedState = FormWindowState.Normal;
                    _wasMaximizedBeforeMinimize = false;
                    HistoryService.IsMaximizedMode = false;
                    ShowWindow(this.Handle, 9); // SW_RESTORE
                }
                BringToFront();
                Activate();
                SetForegroundWindow(this.Handle);
                ApplyResponsiveLayout();
            }
            catch { }
        }

        private async Task CheckForAppUpdatesAsync(bool manual = false)
        {
            if (manual)
            {
                _audioPlayer?.RaiseStatus("Đang kiểm tra bản cập nhật...");
            }

            try
            {
                var update = await AppUpdateService.Instance.CheckForUpdateAsync();
                if (manual)
                {
                    _audioPlayer?.RaiseStatus(null);
                }

                if (update != null)
                {
                    if (InvokeRequired)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            RestoreFromTray();
                            CyberUpdateDialog.ShowUpdate(this, update);
                        }));
                    }
                    else
                    {
                        RestoreFromTray();
                        CyberUpdateDialog.ShowUpdate(this, update);
                    }
                }
                else if (manual)
                {
                    Action showLatestDlg = () =>
                    {
                        CyberConfirmDialog.ShowAlert(
                            "Kiểm tra cập nhật",
                            $"Bạn đang sử dụng phiên bản mới nhất (v{AppUpdateService.CurrentVersionName})!",
                            okText: "Đã hiểu",
                            owner: this);
                    };

                    if (InvokeRequired)
                    {
                        BeginInvoke(showLatestDlg);
                    }
                    else
                    {
                        showLatestDlg();
                    }
                }
            }
            catch
            {
                if (manual)
                {
                    _audioPlayer?.RaiseStatus(null);
                    Action showErrorDlg = () =>
                    {
                        CyberConfirmDialog.ShowAlert(
                            "Lỗi kiểm tra cập nhật",
                            "Không thể kết nối đến máy chủ cập nhật. Vui lòng kiểm tra lại kết nối mạng.",
                            okText: "Đóng",
                            owner: this);
                    };

                    if (InvokeRequired)
                    {
                        BeginInvoke(showErrorDlg);
                    }
                    else
                    {
                        showErrorDlg();
                    }
                }
            }
            finally
            {
                if (manual)
                {
                    _audioPlayer?.RaiseStatus(null);
                }
            }
        }

        private void StartSingleInstanceIpcServer()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string pipeName = "KahoiiMusic_Pipe_" + Environment.UserName;
                while (!_isFormClosed)
                {
                    try
                    {
                        using (var server = new System.IO.Pipes.NamedPipeServerStream(pipeName, System.IO.Pipes.PipeDirection.In, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous))
                        {
                            server.WaitForConnection();
                            using (var reader = new System.IO.StreamReader(server, System.Text.Encoding.UTF8))
                            {
                                string msg = reader.ReadLine();
                                if (!string.IsNullOrEmpty(msg))
                                {
                                    try
                                    {
                                        BeginInvoke(new Action(delegate
                                        {
                                            HandleIncomingUrl(msg);
                                        }));
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch
                    {
                        if (_isFormClosed) break;
                        System.Threading.Thread.Sleep(200);
                    }
                }
            });
        }

        private void RegisterProtocolHandler()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\kahoiimusic"))
                {
                    key.SetValue("", "URL:KahoiiMusic Protocol");
                    key.SetValue("URL Protocol", "");
                    using (var iconKey = key.CreateSubKey("DefaultIcon"))
                    {
                        iconKey.SetValue("", $"\"{exePath}\",0");
                    }
                    using (var cmdKey = key.CreateSubKey(@"shell\open\command"))
                    {
                        cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }
            }
            catch { }
        }

        private async void HandleIncomingUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            RestoreFromTray();
            BringToFront();
            Activate();

            string trimmed = url.Trim().Trim('"');
            if (trimmed.Equals("__ACTIVATE__", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Trích xuất các tham số đăng nhập nhanh (auth_rt, user) nếu có
            string commonRt = null;
            string commonUser = null;
            string incomingItems = null;
            int qIdxCommon = trimmed.IndexOf('?');
            if (qIdxCommon >= 0)
            {
                string qStr = trimmed.Substring(qIdxCommon + 1);
                foreach (var part in qStr.Split('&'))
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2)
                    {
                        string k = Uri.UnescapeDataString(kv[0]);
                        string v = Uri.UnescapeDataString(kv[1]);
                        if (k.Equals("auth_rt", StringComparison.OrdinalIgnoreCase) || k.Equals("refreshToken", StringComparison.OrdinalIgnoreCase)) commonRt = v;
                        if (k.Equals("user", StringComparison.OrdinalIgnoreCase)) commonUser = v;
                        if (k.Equals("items", StringComparison.OrdinalIgnoreCase)) incomingItems = v;
                    }
                }
            }

            // Hỗ trợ Đăng Nhập Nhanh (Quick Login) từ Extension:
            if (!string.IsNullOrEmpty(commonRt))
            {
                string appUserNow = FirebaseAuthService.Instance.CurrentUser?.email;
                if (!FirebaseAuthService.Instance.IsLoggedIn)
                {
                    // Ứng dụng chưa đăng nhập -> Tự động đăng nhập ngay lập tức không cần gõ mật khẩu!
                    var res = await FirebaseAuthService.Instance.SignInWithRefreshTokenAsync(commonRt, commonUser);
                    if (res.Item1)
                    {
                        _audioPlayer?.RaiseStatus(string.Format("✓ Đã đăng nhập nhanh từ Extension: {0}", FirebaseAuthService.Instance.CurrentUser?.email));
                        _ = FirebaseSyncService.Instance.TriggerAutoFetchAsync();
                    }
                }
                else if (!string.IsNullOrEmpty(commonUser) && !string.Equals(commonUser, appUserNow, StringComparison.OrdinalIgnoreCase))
                {
                    // Ứng dụng đang đăng nhập tài khoản khác -> Hỏi xác nhận chuyển tài khoản nhanh bằng Cyber Dialog
                    bool switchOk = CyberConfirmDialog.ShowConfirm(
                        "Chuyển đổi tài khoản từ Extension",
                        string.Format("Tiện ích mở rộng trên trình duyệt đang dùng tài khoản:\n👉 {0}\n\nỨng dụng KahoiiMusic đang đăng nhập:\n👉 {1}\n\nBạn có muốn tự động chuyển sang tài khoản '{0}' trên ứng dụng không?", commonUser, appUserNow),
                        yesText: "Chuyển ngay",
                        noText: "Để sau",
                        owner: this);
                    if (switchOk)
                    {
                        FirebaseAuthService.Instance.SignOut();
                        var res = await FirebaseAuthService.Instance.SignInWithRefreshTokenAsync(commonRt, commonUser);
                        if (res.Item1)
                        {
                            _audioPlayer?.RaiseStatus(string.Format("✓ Đã chuyển sang tài khoản: {0}", FirebaseAuthService.Instance.CurrentUser?.email));
                            _ = FirebaseSyncService.Instance.TriggerAutoFetchAsync();
                        }
                    }
                }
            }

            if (trimmed.Equals("kahoiimusic://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("kahoiimusic://open", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("kahoiimusic://open?", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("kahoiimusic://open/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (trimmed.StartsWith("kahoiimusic://sync", StringComparison.OrdinalIgnoreCase))
            {
                _ = FirebaseSyncService.Instance.TriggerAutoFetchAsync();
                return;
            }

            // Handle Save Playlist / Collection directly from URL
            if (trimmed.StartsWith("kahoiimusic://save_collection", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("kahoiimusic://save_playlist", StringComparison.OrdinalIgnoreCase))
            {
                string targetUrl = null;
                string playlistName = null;
                string reqUser = null;
                try
                {
                    int qIdx = trimmed.IndexOf('?');
                    if (qIdx >= 0)
                    {
                        string query = trimmed.Substring(qIdx + 1);
                        foreach (var part in query.Split('&'))
                        {
                            var kv = part.Split('=');
                            if (kv.Length == 2)
                            {
                                string k = Uri.UnescapeDataString(kv[0]);
                                string v = Uri.UnescapeDataString(kv[1]);
                                if (k.Equals("url", StringComparison.OrdinalIgnoreCase)) targetUrl = v;
                                if (k.Equals("title", StringComparison.OrdinalIgnoreCase) || k.Equals("name", StringComparison.OrdinalIgnoreCase)) playlistName = v;
                                if (k.Equals("user", StringComparison.OrdinalIgnoreCase)) reqUser = v;
                                if (k.Equals("items", StringComparison.OrdinalIgnoreCase)) incomingItems = v;
                            }
                        }
                    }
                }
                catch { }

                // Kiểm tra tài khoản đã đăng nhập
                if (!FirebaseAuthService.Instance.IsLoggedIn)
                {
                    _audioPlayer?.RaiseStatus("🔒 Tính năng Lưu BST yêu cầu đăng nhập tài khoản!");
                    CyberConfirmDialog.ShowAlert(
                        "Yêu cầu đăng nhập KahoiiMusic",
                        "Chức năng Lưu vào Bộ sưu tập chỉ có thể sử dụng khi bạn đã đăng nhập tài khoản KahoiiMusic.\n\nVui lòng đăng nhập vào ứng dụng để tiếp tục!",
                        okText: "Đã hiểu",
                        owner: this);
                    ShowAccountPopup();
                    return;
                }

                // Kiểm tra tài khoản khớp giữa Extension và App
                string appUser = FirebaseAuthService.Instance.CurrentUser?.email;
                if (!string.IsNullOrEmpty(reqUser) && !string.IsNullOrEmpty(appUser) && !string.Equals(reqUser, appUser, StringComparison.OrdinalIgnoreCase))
                {
                    _audioPlayer?.RaiseStatus(string.Format("⚠️ Extension dùng '{0}', khác tài khoản App ('{1}')!", reqUser, appUser));
                    CyberConfirmDialog.ShowAlert(
                        "Không khớp tài khoản KahoiiMusic",
                        string.Format("Tiện ích mở rộng trên trình duyệt đang dùng tài khoản:\n👉 {0}\n\nNhưng ứng dụng KahoiiMusic trên máy tính đang đăng nhập:\n👉 {1}\n\nVui lòng đăng nhập cùng một tài khoản trên cả 2 nơi để đồng bộ chính xác!", reqUser, appUser),
                        okText: "Đã hiểu",
                        owner: this);
                    return;
                }

                if (!string.IsNullOrEmpty(targetUrl) || !string.IsNullOrEmpty(incomingItems))
                {
                    SavePlaylistUrlToCollectionAsync(targetUrl, playlistName, incomingItems);
                    return;
                }
            }

            // Handle Direct Download Track from URL
            if (trimmed.StartsWith("kahoiimusic://download", StringComparison.OrdinalIgnoreCase))
            {
                string vId = null;
                string vTitle = null;
                string vAuthor = null;
                int vDur = 0;
                string reqUser = null;
                try
                {
                    int qIdx = trimmed.IndexOf('?');
                    if (qIdx >= 0)
                    {
                        string query = trimmed.Substring(qIdx + 1);
                        foreach (var part in query.Split('&'))
                        {
                            var kv = part.Split('=');
                            if (kv.Length == 2)
                            {
                                string k = Uri.UnescapeDataString(kv[0]);
                                string v = Uri.UnescapeDataString(kv[1]);
                                if (k.Equals("v", StringComparison.OrdinalIgnoreCase) || k.Equals("id", StringComparison.OrdinalIgnoreCase)) vId = v;
                                if (k.Equals("title", StringComparison.OrdinalIgnoreCase)) vTitle = v;
                                if (k.Equals("author", StringComparison.OrdinalIgnoreCase)) vAuthor = v;
                                if (k.Equals("dur", StringComparison.OrdinalIgnoreCase)) int.TryParse(v, out vDur);
                                if (k.Equals("user", StringComparison.OrdinalIgnoreCase)) reqUser = v;
                            }
                        }
                    }
                }
                catch { }

                // Kiểm tra tài khoản đã đăng nhập
                if (!FirebaseAuthService.Instance.IsLoggedIn)
                {
                    _audioPlayer?.RaiseStatus("🔒 Tính năng Tải về yêu cầu đăng nhập tài khoản!");
                    CyberConfirmDialog.ShowAlert(
                        "Yêu cầu đăng nhập KahoiiMusic",
                        "Chức năng Tải về (Offline) chỉ có thể sử dụng khi bạn đã đăng nhập tài khoản KahoiiMusic.\n\nVui lòng đăng nhập vào ứng dụng để tiếp tục!",
                        okText: "Đã hiểu",
                        owner: this);
                    ShowAccountPopup();
                    return;
                }

                // Kiểm tra tài khoản khớp giữa Extension và App
                string appUser = FirebaseAuthService.Instance.CurrentUser?.email;
                if (!string.IsNullOrEmpty(reqUser) && !string.IsNullOrEmpty(appUser) && !string.Equals(reqUser, appUser, StringComparison.OrdinalIgnoreCase))
                {
                    _audioPlayer?.RaiseStatus(string.Format("⚠️ Extension dùng '{0}', khác tài khoản App ('{1}')!", reqUser, appUser));
                    CyberConfirmDialog.ShowAlert(
                        "Không khớp tài khoản KahoiiMusic",
                        string.Format("Tiện ích mở rộng trên trình duyệt đang dùng tài khoản:\n👉 {0}\n\nNhưng ứng dụng KahoiiMusic trên máy tính đang đăng nhập:\n👉 {1}\n\nVui lòng đăng nhập cùng một tài khoản trên cả 2 nơi để đồng bộ chính xác!", reqUser, appUser),
                        okText: "Đã hiểu",
                        owner: this);
                    return;
                }

                if (!string.IsNullOrEmpty(vId))
                {
                    string cleanTitle = !string.IsNullOrEmpty(vTitle) ? AudioPlayerService.CleanDisplayTitle(vTitle) : "Bài hát YouTube";
                    if (SavedTracksService.Instance.IsTrackSaved(vId))
                    {
                        _audioPlayer?.RaiseStatus(string.Format("✓ '{0}' đã có sẵn trong mục Đã tải!", cleanTitle));
                        return;
                    }

                    var track = new PlaylistItem
                    {
                        Id = vId,
                        Title = cleanTitle,
                        Author = !string.IsNullOrEmpty(vAuthor) ? AudioPlayerService.CleanAuthor(vAuthor) : "",
                        DurationSeconds = vDur
                    };
                    _audioPlayer?.RaiseStatus(string.Format("Đang bắt đầu tải '{0}' về máy...", track.Title));
                    SavedTracksService.Instance.SaveTrackAsync(track, (success, msg) =>
                    {
                        BeginInvoke(new Action(delegate
                        {
                            _audioPlayer?.RaiseStatus(success ? ("✓ Đã tải xong: " + track.Title) : ("Lỗi tải: " + msg));
                        }));
                    });
                    return;
                }
            }

            string targetUrlToPlay = trimmed;
            string incomingV = null;
            string incomingTitle = null;
            string incomingAuthor = null;
            int incomingDur = 0;
            string incomingList = null;

            if (trimmed.StartsWith("kahoiimusic://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    int qIdx = trimmed.IndexOf('?');
                    if (qIdx >= 0)
                    {
                        string query = trimmed.Substring(qIdx + 1);
                        foreach (var part in query.Split('&'))
                        {
                            var kv = part.Split('=');
                            if (kv.Length == 2)
                            {
                                string k = Uri.UnescapeDataString(kv[0]);
                                string v = Uri.UnescapeDataString(kv[1]);
                                if (k.Equals("url", StringComparison.OrdinalIgnoreCase)) targetUrlToPlay = v;
                                else if (k.Equals("v", StringComparison.OrdinalIgnoreCase) || k.Equals("id", StringComparison.OrdinalIgnoreCase)) incomingV = v;
                                else if (k.Equals("title", StringComparison.OrdinalIgnoreCase)) incomingTitle = v;
                                else if (k.Equals("author", StringComparison.OrdinalIgnoreCase)) incomingAuthor = v;
                                else if (k.Equals("dur", StringComparison.OrdinalIgnoreCase)) int.TryParse(v, out incomingDur);
                                else if (k.Equals("list", StringComparison.OrdinalIgnoreCase)) incomingList = v;
                                else if (k.Equals("items", StringComparison.OrdinalIgnoreCase)) incomingItems = v;
                            }
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                catch
                {
                    return;
                }
            }

            if (string.IsNullOrEmpty(incomingV) && !string.IsNullOrEmpty(targetUrlToPlay))
            {
                incomingV = AudioPlayerService.ExtractVideoId(targetUrlToPlay);
            }
            if (string.IsNullOrEmpty(incomingList) && !string.IsNullOrEmpty(targetUrlToPlay))
            {
                incomingList = AudioPlayerService.ExtractListId(targetUrlToPlay);
            }

            searchPillBox.Text = targetUrlToPlay;

            // Nếu nhận được danh sách bài hát cụ thể từ YouTube Mix/Radio (khớp 100% giao diện YouTube)
            if (!string.IsNullOrEmpty(incomingItems))
            {
                string[] vids = incomingItems.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (vids.Length > 0)
                {
                    btnPlaylist.HasPlaylist = true;
                    playlistPopup.StartLoading();
                    _audioPlayer.EnqueueTrackList(vids, incomingV, incomingTitle, incomingAuthor, incomingDur);
                    if (!string.IsNullOrEmpty(incomingList))
                    {
                        _audioPlayer.ResolveMissingTrackDurationsAsync(incomingList, targetUrlToPlay);
                    }
                    return;
                }
            }

            if (!string.IsNullOrEmpty(incomingV) && !string.IsNullOrEmpty(incomingList))
            {
                btnPlaylist.HasPlaylist = true;
                playlistPopup.StartLoading();
                _audioPlayer.EnqueueAndPlayTrack(incomingV, incomingTitle, incomingAuthor, incomingDur, targetUrlToPlay);
                _audioPlayer.LoadPlaylistQueueOnly(targetUrlToPlay);
                return;
            }

            if (!string.IsNullOrEmpty(incomingList))
            {
                btnPlaylist.HasPlaylist = true;
                playlistPopup.StartLoading();
                _audioPlayer.LoadPlaylistAsync(targetUrlToPlay, incomingV, playFirstIfNoVideo: true);
                return;
            }

            if (!string.IsNullOrEmpty(incomingV))
            {
                btnPlaylist.HasPlaylist = true;
                _audioPlayer.EnqueueAndPlayTrack(incomingV, incomingTitle, incomingAuthor, incomingDur, targetUrlToPlay);
                return;
            }

            SearchPillBox_UrlSubmitted(this, targetUrlToPlay);
        }

        private void SavePlaylistUrlToCollectionAsync(string playlistUrl, string customName, string incomingItems = null)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string detectedTitle = !string.IsNullOrWhiteSpace(customName) ? customName : "Danh sách kết hợp YouTube";

                    // Nếu có danh sách cụ thể từ YouTube Mix
                    if (!string.IsNullOrEmpty(incomingItems))
                    {
                        string[] vids = incomingItems.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (vids.Length > 0)
                        {
                            _audioPlayer?.RaiseStatus(string.Format("Đang lưu '{0}' ({1} bài) vào Bộ sưu tập...", detectedTitle, vids.Length));
                            List<PlaylistItem> exactItems = new List<PlaylistItem>();
                            foreach (var vid in vids)
                            {
                                string v = vid.Trim();
                                if (!string.IsNullOrEmpty(v))
                                {
                                    string actualId = v;
                                    int actualDur = 0;
                                    int colonIdx = v.IndexOf(':');
                                    if (colonIdx > 0)
                                    {
                                        actualId = v.Substring(0, colonIdx).Trim();
                                        int.TryParse(v.Substring(colonIdx + 1).Trim(), out actualDur);
                                    }
                                    if (actualDur <= 0)
                                    {
                                        actualDur = SavedTracksService.Instance.GetTrackDuration(actualId);
                                        if (actualDur <= 0) actualDur = HistoryService.GetTrackDuration(actualId);
                                    }
                                    exactItems.Add(new PlaylistItem
                                    {
                                        Id = actualId,
                                        Title = "Đang tải tên bài hát...",
                                        Author = "",
                                        DurationSeconds = actualDur
                                    });
                                }
                            }

                            var newCol = new UserPlaylist
                            {
                                Name = detectedTitle,
                                Items = exactItems
                            };

                            PlaylistCollectionService.Instance.SavePlaylist(detectedTitle, exactItems);
                            _audioPlayer?.RaiseStatus(string.Format("✓ Đã lưu '{0}' vào Bộ sưu tập!", detectedTitle));

                            // Nạp ngầm tiêu đề qua oEmbed và sync Firebase
                            System.Threading.ThreadPool.QueueUserWorkItem(delegate
                            {
                                foreach (var it in exactItems)
                                {
                                    try
                                    {
                                        using (var wc = new System.Net.WebClient())
                                        {
                                            wc.Encoding = Encoding.UTF8;
                                            wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                                            string json = wc.DownloadString("https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v=" + it.Id + "&format=json");
                                            if (!string.IsNullOrEmpty(json))
                                            {
                                                var mTitle = System.Text.RegularExpressions.Regex.Match(json, @"""title""\s*:\s*""([^""]+)""");
                                                if (mTitle.Success) it.Title = AudioPlayerService.CleanDisplayTitle(System.Text.RegularExpressions.Regex.Unescape(mTitle.Groups[1].Value));
                                                var mAuthor = System.Text.RegularExpressions.Regex.Match(json, @"""author_name""\s*:\s*""([^""]+)""");
                                                if (mAuthor.Success) it.Author = AudioPlayerService.CleanAuthor(System.Text.RegularExpressions.Regex.Unescape(mAuthor.Groups[1].Value));
                                            }
                                        }
                                    }
                                    catch { }
                                }
                                PlaylistCollectionService.Instance.SavePlaylist(detectedTitle, exactItems);
                                FirebaseSyncService.Instance.RequestAutoUpload();
                            });
                            return;
                        }
                    }

                    _audioPlayer?.RaiseStatus("Đang tải danh sách phát để lưu vào Bộ sưu tập...");

                    string ytdlpPath = AudioPlayerService.FindYtDlp();
                    if (string.IsNullOrEmpty(ytdlpPath)) return;

                    string listId = AudioPlayerService.ExtractListId(playlistUrl);
                    string targetUrl = playlistUrl.Trim();
                    if (!string.IsNullOrEmpty(listId) && (listId.StartsWith("PL", StringComparison.OrdinalIgnoreCase) || listId.StartsWith("OLAK", StringComparison.OrdinalIgnoreCase)))
                    {
                        targetUrl = "https://www.youtube.com/playlist?list=" + listId;
                    }

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = ytdlpPath,
                        Arguments = string.Format("--encoding utf-8 --yes-playlist --flat-playlist --print \"%(playlist_title)s\t%(id)s\t%(title)s\t%(duration)s\t%(uploader)s\" \"{0}\"", targetUrl),
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = false,
                        StandardOutputEncoding = Encoding.UTF8
                    };
                    psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                    List<PlaylistItem> items = new List<PlaylistItem>();
                    if (string.IsNullOrEmpty(detectedTitle)) detectedTitle = customName;

                    using (Process proc = Process.Start(psi))
                    {
                        string line;
                        while ((line = proc.StandardOutput.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (string.IsNullOrEmpty(line)) continue;
                            string[] parts = line.Split('\t');
                            if (parts.Length >= 3)
                            {
                                string plTitle = parts[0].Trim();
                                string id = parts[1].Trim();
                                string title = parts[2].Trim();
                                int dur = 0;
                                string author = (parts.Length >= 5) ? parts[4].Trim() : "";

                                if (string.IsNullOrEmpty(detectedTitle) && !string.IsNullOrEmpty(plTitle) && plTitle != "NA" && plTitle != "None")
                                {
                                    detectedTitle = plTitle;
                                }

                                if (!string.IsNullOrEmpty(id) && id.Length == 11)
                                {
                                    if (!items.Exists(x => x.Id == id))
                                    {
                                        if (parts.Length >= 4) int.TryParse(parts[3].Trim(), out dur);
                                        items.Add(new PlaylistItem
                                        {
                                            Id = id,
                                            Title = AudioPlayerService.CleanDisplayTitle(title),
                                            Author = AudioPlayerService.CleanAuthor(author),
                                            DurationSeconds = dur
                                        });
                                    }
                                }
                            }
                        }
                        proc.WaitForExit(15000);
                    }

                    if (items.Count > 0)
                    {
                        string finalName = !string.IsNullOrWhiteSpace(detectedTitle) ? detectedTitle.Trim() : "Playlist YouTube";
                        var saved = PlaylistCollectionService.Instance.SavePlaylist(finalName, items);
                        if (saved != null)
                        {
                            BeginInvoke(new Action(delegate
                            {
                                _audioPlayer?.RaiseStatus(string.Format("✓ Đã lưu '{0}' ({1} bài) vào Bộ sưu tập!", saved.Name, saved.Items.Count));
                                if (playlistPopup != null)
                                {
                                    playlistPopup.RefreshPlaylists();
                                }
                            }));
                        }
                    }
                    else
                    {
                        _audioPlayer?.RaiseStatus("Không tìm thấy bài hát nào trong playlist để lưu.");
                    }
                }
                catch (Exception ex)
                {
                    _audioPlayer?.RaiseStatus("Lỗi lưu playlist: " + ex.Message);
                }
            });
        }

        private void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
                _lastNonMinimizedState = FormWindowState.Normal;
                _wasMaximizedBeforeMinimize = false;
                HistoryService.IsMaximizedMode = false;
                if (btnMaximize != null) { btnMaximize.Invalidate(); }
            }
            else
            {
                Screen scr = Screen.FromHandle(Handle);
                MaximizedBounds = scr.WorkingArea;
                WindowState = FormWindowState.Maximized;
                _lastNonMinimizedState = FormWindowState.Maximized;
                _wasMaximizedBeforeMinimize = true;
                HistoryService.IsMaximizedMode = true;
                if (btnMaximize != null) { btnMaximize.Invalidate(); }
            }
        }

        private const int WM_ERASEBKGND = 0x0014;
        private const int WM_NCCALCSIZE = 0x83;
        private const int WM_NCHITTEST = 0x84;
        private const int WM_NCPAINT = 0x85;
        private const int WM_NCACTIVATE = 0x86;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private const int RESIZE_BORDER = 8;

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, int uMsg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(int crColor);

        [DllImport("user32.dll")]
        private static extern int FillRect(IntPtr hDC, [In] ref RECT lprc, IntPtr hbr);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
        private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetClassLongA")]
        private static extern int SetClassLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private static readonly IntPtr _darkBrush = CreateSolidBrush(ColorTranslator.ToWin32(Color.FromArgb(20, 16, 38)));

        private const int WM_ACTIVATE = 0x0006;
        private const int WA_INACTIVE = 0;
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        private bool _isUserResizing = false;

        private static readonly Font _titleFontLarge = new Font("Segoe UI", 12f, FontStyle.Bold);
        private static readonly Font _titleFontNormal = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        private static readonly Font _authorFontLarge = new Font("Segoe UI", 12f, FontStyle.Regular);
        private static readonly Font _authorFontNormal = new Font("Segoe UI", 10.25f, FontStyle.Regular);

        private static IntPtr SetClassLongWrapper(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetClassLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetClassLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                const int GCLP_HBRBACKGROUND = -10;
                SetClassLongWrapper(this.Handle, GCLP_HBRBACKGROUND, _darkBrush);

                if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 6)
                {
                    MARGINS margins = new MARGINS { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 };
                    DwmExtendFrameIntoClientArea(this.Handle, ref margins);
                }

                if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 10)
                {
                    int darkMode = 1;
                    int res = DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                    if (res != 0)
                    {
                        DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, sizeof(int));
                    }
                }
            }
            catch { }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if ((searchPillBox == null || !searchPillBox.ContainsFocus) && (_sideMenuPanel == null || !_sideMenuPanel.IsSearchFocused))
            {
                this.Focus();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ENTERSIZEMOVE)
            {
                _isUserResizing = true;
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                _isUserResizing = false;
                ApplyResponsiveLayout();
                Invalidate(true);
            }

            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;
                return;
            }

            if (m.Msg == WM_ACTIVATE)
            {
                int wa = unchecked((short)(m.WParam.ToInt64() & 0xFFFF));
                if (wa != WA_INACTIVE)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if ((searchPillBox == null || !searchPillBox.ContainsFocus) && (_sideMenuPanel == null || !_sideMenuPanel.IsSearchFocused))
                        {
                            this.Focus();
                        }
                    }));
                }
            }

            if (m.Msg == WM_NCCALCSIZE)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_NCACTIVATE)
            {
                m.Result = DefWindowProc(m.HWnd, WM_NCACTIVATE, m.WParam, new IntPtr(-1));
                return;
            }

            if (m.Msg == WM_NCPAINT)
            {
                m.Result = IntPtr.Zero;
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
                Point pt = PointToClient(screenPt);
                // Allow dragging window from top caption bar, avoiding window controls on right & search box
                int captionH = _isMaximizedMode ? 50 : 24;
                int rightMargin = _isMaximizedMode ? 140 : 85;
                bool insideSearch = false;
                if (searchPillBox != null && searchPillBox.Visible)
                {
                    Point searchScreenPt = searchPillBox.PointToScreen(Point.Empty);
                    Rectangle searchScreenRect = new Rectangle(searchScreenPt, searchPillBox.Size);
                    insideSearch = searchScreenRect.Contains(screenPt);
                }
                if (pt.Y <= captionH && pt.X < ClientSize.Width - rightMargin && !insideSearch)
                {
                    m.Result = (IntPtr)HTCAPTION;
                    return;
                }
                m.Result = (IntPtr)HTCLIENT;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                MinimizeToTray();
                return;
            }
            _lastNonMinimizedState = WindowState;
            if (WindowState == FormWindowState.Maximized)
            {
                _wasMaximizedBeforeMinimize = true;
                HistoryService.IsMaximizedMode = true;
                if (btnMaximize != null) { btnMaximize.Invalidate(); }
            }
            else if (WindowState == FormWindowState.Normal)
            {
                _wasMaximizedBeforeMinimize = false;
                HistoryService.IsMaximizedMode = false;
                if (btnMaximize != null) { btnMaximize.Invalidate(); }
                if (ClientSize.Width != 480 || ClientSize.Height != 362)
                {
                    ClientSize = new Size(480, 362);
                }
            }
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            int clientW = ClientSize.Width;
            int clientH = ClientSize.Height;
            if (clientW < 300 || clientH < 250) return;

            this.SuspendLayout();
            panelPlayerCard.SuspendLayout();
            panelTopBar.SuspendLayout();
            gradientPanel.SuspendLayout();
            panelBottomControls.SuspendLayout();

            try
            {
                if (WindowState == FormWindowState.Maximized || clientW >= 800)
                {
                    ApplyMaximizedTemplate(clientW, clientH);
                }
                else
                {
                    ApplyCompactTemplate(clientW, clientH);
                }

                // Search dropdown popup position if visible
                if (_searchDropdown != null && _searchDropdown.Visible)
                {
                    Point dropPt = panelTopBar.PointToScreen(new Point(searchPillBox.Left, searchPillBox.Bottom + 4));
                    _searchDropdown.Location = dropPt;
                    _searchDropdown.Width = searchPillBox.Width;
                }
            }
            finally
            {
                panelBottomControls.ResumeLayout(false);
                gradientPanel.ResumeLayout(false);
                panelTopBar.ResumeLayout(false);
                panelPlayerCard.ResumeLayout(false);
                this.ResumeLayout(false);
            }

            panelPlayerCard.Invalidate();
        }

        private void ApplyMaximizedTemplate(int clientW, int clientH)
        {
            _isMaximizedMode = true;

            // Auto-close playlist popup in maximized mode
            if (playlistPopup != null && playlistPopup.Visible)
            {
                playlistPopup.Hide();
                btnPlaylist.IsOpen = false;
            }

            // 1. panelPlayerCard fills 100% of the client area seamlessly (NO black gap at top, NO border around edges)
            int cardW = clientW;
            int cardH = clientH;
            panelPlayerCard.Location = new Point(0, 0);
            panelPlayerCard.Size = new Size(cardW, cardH);

            // 2. panelTopBar at top (height 50px)
            int topBarH = 50;
            panelTopBar.Location = new Point(0, 0);
            panelTopBar.Size = new Size(cardW, topBarH);
            panelTopBar.BackColor = Color.Transparent;

            // Reparent top controls to panelTopBar for seamless integrated header
            if (picLogo.Parent != panelTopBar) panelTopBar.Controls.Add(picLogo);
            if (lblLogo.Parent != panelTopBar) panelTopBar.Controls.Add(lblLogo);
            if (btnClose != null && btnClose.Parent != panelTopBar) panelTopBar.Controls.Add(btnClose);
            if (btnMaximize != null && btnMaximize.Parent != panelTopBar) panelTopBar.Controls.Add(btnMaximize);
            if (btnMinimize != null && btnMinimize.Parent != panelTopBar) panelTopBar.Controls.Add(btnMinimize);

            // Left: Logo & App Name (Enlarged)
            int logoSize = 30;
            picLogo.Size = new Size(logoSize, logoSize);
            picLogo.Location = new Point(14, (topBarH - logoSize) / 2);
            picLogo.BackColor = Color.Transparent;
            picLogo.Visible = true;
            picLogo.BringToFront();

            int lblH = 30;
            lblLogo.Size = new Size(180, lblH);
            lblLogo.Location = new Point(picLogo.Right + 10, (topBarH - lblH) / 2);
            lblLogo.BackColor = Color.Transparent;
            lblLogo.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            lblLogo.Visible = true;
            lblLogo.BringToFront();
            lblLogo.Invalidate();

            // Far Right: Stylized window controls (Close, Maximize, Minimize)
            int btnH = 30;
            int btnW = 34;
            int btnY = (topBarH - btnH) / 2;

            if (btnClose != null)
            {
                btnClose.Location = new Point(cardW - 42, btnY);
                btnClose.Size = new Size(36, btnH);
                btnClose.Text = string.Empty;
                btnClose.Visible = true;
                btnClose.BringToFront();
            }
            if (btnMaximize != null)
            {
                btnMaximize.Location = new Point(cardW - 42 - btnW - 4, btnY);
                btnMaximize.Size = new Size(btnW, btnH);
                btnMaximize.Text = string.Empty;
                btnMaximize.Visible = true;
                btnMaximize.BringToFront();
            }
            if (btnMinimize != null)
            {
                btnMinimize.Location = new Point(cardW - 42 - (btnW * 2) - 8, btnY);
                btnMinimize.Size = new Size(btnW, btnH);
                btnMinimize.Text = string.Empty;
                btnMinimize.Visible = true;
                btnMinimize.BringToFront();
            }

            // Right of search: User Avatar button (Larger: 36x36, placed with elegant breathing room)
            btnPlaylist.Visible = false;
            btnUser.Visible = true;
            btnUser.Size = new Size(38, 38);
            int userRightLimit = (btnMinimize != null) ? btnMinimize.Left - 16 : cardW - 16;
            btnUser.Location = new Point(userRightLimit - btnUser.Width, (topBarH - btnUser.Height) / 2);
            btnUser.BringToFront();

            // Center: Search box
            int searchLeftLimit = lblLogo.Right + 20;
            int searchRightLimit = btnUser.Left - 20;
            int availSearchW = Math.Max(180, searchRightLimit - searchLeftLimit);
            int searchW = Math.Min(560, availSearchW);
            int searchX = (cardW - searchW) / 2;
            if (searchX < searchLeftLimit) searchX = searchLeftLimit;
            if (searchX + searchW > searchRightLimit) searchW = searchRightLimit - searchX;
            searchPillBox.Location = new Point(searchX, (topBarH - 36) / 2);
            searchPillBox.Size = new Size(Math.Max(180, searchW), 36);
            searchPillBox.BringToFront();

            // 4. panelBottomControls hidden in Maximized mode
            panelBottomControls.Visible = false;

            // 5. gradientPanel fills rest of space below topBar seamlessly
            int midY = topBarH;
            int midH = cardH - midY;
            gradientPanel.Location = new Point(0, midY);
            gradientPanel.Size = new Size(cardW, midH);
            gradientPanel.BackColor = Color.Transparent;
            gradientPanel.IsMaximizedTemplate = true;
            gradientPanel.Invalidate();

            // Reparent controls for Maximized template
            if (_sideMenuPanel != null && _sideMenuPanel.Parent != gradientPanel) gradientPanel.Controls.Add(_sideMenuPanel);
            if (_topSectionCard != null && _topSectionCard.Parent != gradientPanel) gradientPanel.Controls.Add(_topSectionCard);
            if (_activeQueuePanel != null && _activeQueuePanel.Parent != gradientPanel) gradientPanel.Controls.Add(_activeQueuePanel);

            if (_topSectionCard != null)
            {
                if (artworkBox.Parent != _topSectionCard) _topSectionCard.Controls.Add(artworkBox);
                if (marqueeTitle.Parent != _topSectionCard) _topSectionCard.Controls.Add(marqueeTitle);
                if (marqueeAuthor.Parent != _topSectionCard) _topSectionCard.Controls.Add(marqueeAuthor);
                if (_lyricsCard != null && _lyricsCard.Parent != _topSectionCard) _topSectionCard.Controls.Add(_lyricsCard);
            }

            if (_lyricsCard != null && lyricsPanel.Parent != _lyricsCard)
            {
                _lyricsCard.Controls.Add(lyricsPanel);
            }

            // Controls on artworkBox
            if (btnThumbnailMenu != null && btnThumbnailMenu.Parent != artworkBox) artworkBox.Controls.Add(btnThumbnailMenu);
            if (thumbnailActionPopup != null && thumbnailActionPopup.Parent != artworkBox) artworkBox.Controls.Add(thumbnailActionPopup);
            if (_collectionPickerPopup != null && _collectionPickerPopup.Parent != artworkBox) artworkBox.Controls.Add(_collectionPickerPopup);
            if (btnFavorite.Parent != artworkBox) artworkBox.Controls.Add(btnFavorite);
            if (musicTrackBar.Parent != artworkBox) artworkBox.Controls.Add(musicTrackBar);
            if (btnLoop.Parent != artworkBox) artworkBox.Controls.Add(btnLoop);
            if (btnPrevious.Parent != artworkBox) artworkBox.Controls.Add(btnPrevious);
            if (btnPlayPause.Parent != artworkBox) artworkBox.Controls.Add(btnPlayPause);
            if (btnNext.Parent != artworkBox) artworkBox.Controls.Add(btnNext);
            if (btnTimer != null && btnTimer.Parent != artworkBox) artworkBox.Controls.Add(btnTimer);
            if (btnVolume.Parent != artworkBox) artworkBox.Controls.Add(btnVolume);

            // Artwork styling for Maximized template
            artworkBox.EnableModeToggleOnClick = false;
            artworkBox.ShowBottomTimeText = false;
            artworkBox.SetDisplayMode(ArtworkDisplayMode.RoundedCard);
            artworkBox.Cursor = Cursors.Default;
            if (btnLyrics != null) btnLyrics.Visible = false;

            // Gutters & Margins matching mockup
            int padX = 14;
            int padTop = 10;
            int padBottom = 14; // Visible strip of blazing neon hot pink gradient along bottom!
            int gutterX = 14;   // Visible vertical gradient strip between Left and Right sessions
            int gutterY = 12;   // Visible horizontal gradient strip between Top and Bottom sessions

            int totalW = gradientPanel.Width;
            int totalH = gradientPanel.Height;

            // Left Session: SideMenuPanel (~37% proportional width, matching Image 1!)
            int sideW = Math.Max(320, (int)((totalW - padX * 2 - gutterX) * 0.37f));
            int sideH = totalH - padTop - padBottom;
            if (_sideMenuPanel != null)
            {
                _sideMenuPanel.Location = new Point(padX, padTop);
                _sideMenuPanel.Size = new Size(sideW, sideH);
                _sideMenuPanel.Visible = true;
                _sideMenuPanel.BringToFront();
                _sideMenuPanel.RefreshData();
            }

            // Right Sessions
            int rightX = padX + sideW + gutterX;
            int rightW = totalW - rightX - padX;
            int rightAvailH = totalH - padTop - padBottom;

            // Top Card: _topSectionCard (~33% of vertical space, matching Image 1!)
            int topH = Math.Max(165, (int)(rightAvailH * 0.33f));
            if (_topSectionCard != null)
            {
                _topSectionCard.Location = new Point(rightX, padTop);
                _topSectionCard.Size = new Size(rightW, topH);
                _topSectionCard.CardBackColor = Color.FromArgb(150, 22, 10, 42); // Opacity < 100%
                _topSectionCard.CardBorderColor = Color.FromArgb(140, 180, 50, 220);
                _topSectionCard.CornerRadius = 14;
                _topSectionCard.Visible = true;
                _topSectionCard.BringToFront();

                // Artwork inside top card (Square 1:1, cleanly fitting inside _topSectionCard)
                int artH = topH - 24;
                int artW = artH;
                artworkBox.Location = new Point(12, 12);
                artworkBox.Size = new Size(artW, artH);
                artworkBox.Visible = true;
                artworkBox.BringToFront();

                // 1. Menu button & popup top-left inside thumbnail (+10% larger)
                if (btnThumbnailMenu != null)
                {
                    btnThumbnailMenu.Location = new Point(12, 12);
                    btnThumbnailMenu.Size = new Size(28, 28);
                    btnThumbnailMenu.Visible = true;
                    btnThumbnailMenu.BringToFront();
                }

                if (thumbnailActionPopup != null)
                {
                    thumbnailActionPopup.Location = new Point(44, 10);
                    if (thumbnailActionPopup.Visible) thumbnailActionPopup.BringToFront();
                }

                // 2. Favorite Heart button top-right inside thumbnail (neatly padded inside card)
                int favSize = 56;
                btnFavorite.Location = new Point(artW - favSize - 2, 2);
                btnFavorite.Size = new Size(favSize, favSize);
                btnFavorite.ForceFilledHeart = false;
                btnFavorite.ShowCheckmark = false;
                btnFavorite.Visible = true;
                btnFavorite.BringToFront();
                btnFavorite.RefreshState();

                // 3. Seekbar inside thumbnail (thin sleek line, no thumb knob, matching Figma ~70% Y)
                int trackY = (int)(artH * 0.70f);
                musicTrackBar.Location = new Point(14, trackY);
                musicTrackBar.Size = new Size(artW - 28, 12);
                musicTrackBar.ShowThumb = false;
                musicTrackBar.PlayedColor = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(245, 0, 25);
                musicTrackBar.UnplayedColor = Color.White;
                musicTrackBar.Visible = true;
                musicTrackBar.BringToFront();

                // 4. Volume button positioned on left directly above seekbar (larger + 70% opacity, generous bounds)
                int volW = 32;
                int volH = 28;
                btnVolume.Location = new Point(14, trackY - volH - 6);
                btnVolume.Size = new Size(volW, volH);
                btnVolume.IconColor = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(235, 0, 130);
                btnVolume.HoverColor = (_currentPalette != null) ? ColorExtractorService.Lerp(_currentPalette.AccentColor, Color.White, 0.4f) : Color.FromArgb(255, 75, 175);
                btnVolume.Visible = true;
                btnVolume.BringToFront();

                // 5. Playback control buttons under trackbar inside thumbnail (equal sized, 70% opacity)
                int playBtnSize = 36;
                int smallBtnW = 36;
                int smallBtnH = 36;
                int loopBtnSize = 32;
                int timerBtnSize = 32;
                int cx = artW / 2;

                int playY = trackY + 8 + (artH - (trackY + 8) - playBtnSize) / 2;
                int smallBtnY = playY + (playBtnSize - smallBtnH) / 2;

                // Center: Play/Pause circle button (36px, matched size with controls, 70% translucent theme gradient)
                btnPlayPause.Location = new Point(cx - playBtnSize / 2, playY);
                btnPlayPause.Size = new Size(playBtnSize, playBtnSize);
                btnPlayPause.UseThemeGradient = true;
                btnPlayPause.Visible = true;
                btnPlayPause.BringToFront();

                // Left of Play: Previous button (36x36, equilateral rounded triangle, 70% translucent theme gradient)
                btnPrevious.Location = new Point(btnPlayPause.Left - smallBtnW - 16, smallBtnY);
                btnPrevious.Size = new Size(smallBtnW, smallBtnH);
                btnPrevious.UseThemeGradient = true;
                btnPrevious.Visible = true;
                btnPrevious.BringToFront();

                // Right of Play: Next button (36x36, equilateral rounded triangle, 70% translucent theme gradient)
                btnNext.Location = new Point(btnPlayPause.Right + 16, smallBtnY);
                btnNext.Size = new Size(smallBtnW, smallBtnH);
                btnNext.UseThemeGradient = true;
                btnNext.Visible = true;
                btnNext.BringToFront();

                // Far Left: Loop / Repeat button (32x32)
                btnLoop.Location = new Point(14, playY + (playBtnSize - loopBtnSize) / 2);
                btnLoop.Size = new Size(loopBtnSize, loopBtnSize);
                btnLoop.InactiveColor = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(235, 0, 130);
                btnLoop.ActiveColor = (_currentPalette != null) ? ColorExtractorService.Lerp(_currentPalette.AccentColor, Color.White, 0.2f) : Color.FromArgb(255, 45, 140);
                btnLoop.Visible = true;
                btnLoop.BringToFront();

                // Far Right: Timer / Clock button (32x32)
                if (btnTimer != null)
                {
                    btnTimer.Location = new Point(artW - 14 - timerBtnSize, playY + (playBtnSize - timerBtnSize) / 2);
                    btnTimer.Size = new Size(timerBtnSize, timerBtnSize);
                    btnTimer.InactiveColor = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(235, 0, 130);
                    btnTimer.ActiveColor = (_currentPalette != null) ? ColorExtractorService.Lerp(_currentPalette.AccentColor, Color.White, 0.2f) : Color.FromArgb(255, 45, 140);
                    btnTimer.Visible = true;
                    btnTimer.BringToFront();
                }

                if (volumePopup != null && volumePopup.Visible)
                {
                    UpdateVolumePopupPosition();
                }

                // Typography beside artworkBox
                int midX = artworkBox.Right + 14;
                int midW = _topSectionCard.Width - midX - 12;

                if (string.IsNullOrEmpty(_audioPlayer?.CurrentTitle))
                {
                    marqueeTitle.Text = "Chưa phát hiện Audio";
                }
                marqueeTitle.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
                marqueeTitle.TextAlignment = StringAlignment.Near;
                marqueeTitle.Location = new Point(midX, 10);
                marqueeTitle.Size = new Size(midW, 26);
                marqueeTitle.Visible = true;
                marqueeTitle.BringToFront();

                marqueeAuthor.Font = new Font("Segoe UI", 11.5f, FontStyle.Regular);
                marqueeAuthor.TextAlignment = StringAlignment.Near;
                marqueeAuthor.Location = new Point(midX, marqueeTitle.Bottom + 2);
                marqueeAuthor.Size = new Size(midW, 22);
                marqueeAuthor.Visible = true;
                marqueeAuthor.BringToFront();

                // Lyrics box inside top card (distinct vibrant magenta/hot pink gradient border matching theme)
                if (_lyricsCard != null)
                {
                    int lyricsCardY = marqueeAuthor.Bottom + 6;
                    int lyricsCardH = topH - lyricsCardY - 10;
                    _lyricsCard.Location = new Point(midX, lyricsCardY);
                    _lyricsCard.Size = new Size(midW, lyricsCardH);
                    _lyricsCard.CardBackColor = Color.FromArgb(120, 18, 8, 36); // Opacity < 100%
                    _lyricsCard.BorderGradient = true;
                    _lyricsCard.BorderGradientColor1 = (_currentPalette != null) ? _currentPalette.SecondaryAccentColor : Color.FromArgb(140, 30, 180);
                    _lyricsCard.BorderGradientColor2 = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(255, 40, 140);
                    _lyricsCard.CornerRadius = 12;
                    _lyricsCard.Visible = true;
                    _lyricsCard.BringToFront();

                    lyricsPanel.Location = new Point(4, 4);
                    lyricsPanel.Size = new Size(_lyricsCard.Width - 8, _lyricsCard.Height - 8);
                    lyricsPanel.Visible = true;
                    lyricsPanel.BringToFront();
                    lyricsPanel.Invalidate();
                }
            }

            // Bottom Card: _activeQueuePanel (~65% of right session height, matching Image 1!)
            if (_activeQueuePanel != null)
            {
                int bottomY = padTop + topH + gutterY;
                int bottomH = rightAvailH - topH - gutterY;
                _activeQueuePanel.Location = new Point(rightX, bottomY);
                _activeQueuePanel.Size = new Size(rightW, bottomH);
                _activeQueuePanel.Visible = true;
                _activeQueuePanel.BringToFront();
                _activeQueuePanel.RefreshData();
            }

            if (_searchResultsView != null && _searchResultsView.Visible)
            {
                _searchResultsView.BringToFront();
            }
        }

        private void ApplyCompactTemplate(int clientW, int clientH)
        {
            _isMaximizedMode = false;
            gradientPanel.IsMaximizedTemplate = false;
            gradientPanel.Invalidate();

            btnFavorite.ForceFilledHeart = false;
            btnPrevious.FillShape = false;
            btnPrevious.UseThemeGradient = false;
            btnNext.FillShape = false;
            btnNext.UseThemeGradient = false;
            btnPlayPause.UseThemeGradient = false;
            btnPlayPause.CircleColor = Color.White;
            btnPlayPause.CircleHoverColor = Color.FromArgb(240, 240, 240);
            btnPlayPause.IconColor = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(168, 85, 247);
            if (_lyricsCard != null) _lyricsCard.BorderGradient = false;
            musicTrackBar.ShowThumb = true;
            musicTrackBar.PlayedColor = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(235, 15, 15);
            btnVolume.IconColor = Color.White;
            btnVolume.HoverColor = Color.FromArgb(220, 220, 220);
            btnLoop.InactiveColor = Color.FromArgb(220, 220, 220);
            btnLoop.ActiveColor = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(168, 85, 247);
            if (btnTimer != null)
            {
                btnTimer.InactiveColor = Color.FromArgb(220, 220, 220);
                btnTimer.ActiveColor = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(168, 85, 247);
            }
            if (btnPlaylist != null) btnPlaylist.ActiveColor = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(168, 85, 247);
            if (btnLyrics != null) btnLyrics.ActiveColor = (_currentPalette != null) ? _currentPalette.AccentColor : Color.FromArgb(168, 85, 247);

            // Reparent title bar controls back to this Form
            if (picLogo.Parent != this) this.Controls.Add(picLogo);
            if (lblLogo.Parent != this) this.Controls.Add(lblLogo);
            if (btnClose != null && btnClose.Parent != this) this.Controls.Add(btnClose);
            if (btnMaximize != null && btnMaximize.Parent != this) this.Controls.Add(btnMaximize);
            if (btnMinimize != null && btnMinimize.Parent != this) this.Controls.Add(btnMinimize);

            int normLogoSize = 20;
            picLogo.Size = new Size(normLogoSize, normLogoSize);
            picLogo.Location = new Point(6, (24 - normLogoSize) / 2);
            picLogo.Visible = true;
            picLogo.BringToFront();

            int normLblH = 20;
            lblLogo.Size = new Size(140, normLblH);
            lblLogo.Location = new Point(picLogo.Right + 6, (24 - normLblH) / 2);
            lblLogo.Font = new Font("Segoe UI", 9.75f, FontStyle.Bold);
            lblLogo.Visible = true;
            lblLogo.BringToFront();
            lblLogo.Invalidate();

            // 1. Title bar controls
            if (btnClose != null && btnMaximize != null && btnMinimize != null)
            {
                btnClose.Location = new Point(clientW - 28, 2);
                btnClose.Size = new Size(24, 20);
                btnClose.BringToFront();
                btnMaximize.Location = new Point(clientW - 54, 2);
                btnMaximize.Size = new Size(24, 20);
                btnMaximize.BringToFront();
                btnMinimize.Location = new Point(clientW - 80, 2);
                btnMinimize.Size = new Size(24, 20);
                btnMinimize.BringToFront();
            }

            // 2. panelPlayerCard fills the rest of the form below title bar (24px)
            int cardW = clientW;
            int cardH = clientH - 24;
            panelPlayerCard.Location = new Point(0, 24);
            panelPlayerCard.Size = new Size(cardW, cardH);

            // 3. panelTopBar at top (height 46px)
            int topBarH = 46;
            panelTopBar.Location = new Point(0, 0);
            panelTopBar.Size = new Size(cardW, topBarH);

            btnPlaylist.Visible = true;
            btnPlaylist.Location = new Point(12, (topBarH - btnPlaylist.Height) / 2);
            btnUser.Visible = true;
            btnUser.Size = new Size(32, 32);
            btnUser.Location = new Point(cardW - 12 - btnUser.Width, (topBarH - btnUser.Height) / 2);

            int searchLeft = btnPlaylist.Right + 12;
            int searchRight = btnUser.Left - 12;
            int maxSearchW = 560;
            int availSearchW = searchRight - searchLeft;
            int searchW = Math.Min(maxSearchW, availSearchW);
            int searchX = (cardW - searchW) / 2;
            if (searchX < searchLeft) searchX = searchLeft;
            if (searchX + searchW > searchRight) searchW = searchRight - searchX;

            searchPillBox.Location = new Point(searchX, (topBarH - 36) / 2);
            searchPillBox.Size = new Size(Math.Max(120, searchW), 36);

            // 4. panelBottomControls docked at bottom (height 104px)
            int bottomH = 104;
            panelBottomControls.Location = new Point(0, cardH - bottomH);
            panelBottomControls.Size = new Size(cardW, bottomH);
            panelBottomControls.Visible = true;

            // Reparent controls back to compact containers
            if (artworkBox.Parent != gradientPanel) gradientPanel.Controls.Add(artworkBox);
            if (marqueeTitle.Parent != gradientPanel) gradientPanel.Controls.Add(marqueeTitle);
            if (marqueeAuthor.Parent != gradientPanel) gradientPanel.Controls.Add(marqueeAuthor);
            if (lyricsPanel.Parent != gradientPanel) gradientPanel.Controls.Add(lyricsPanel);

            if (musicTrackBar.Parent != panelBottomControls) panelBottomControls.Controls.Add(musicTrackBar);
            if (btnFavorite.Parent != artworkBox) artworkBox.Controls.Add(btnFavorite);
            if (btnQuickDownload.Parent != panelBottomControls) panelBottomControls.Controls.Add(btnQuickDownload);
            if (btnPrevious.Parent != panelBottomControls) panelBottomControls.Controls.Add(btnPrevious);
            if (btnPlayPause.Parent != panelBottomControls) panelBottomControls.Controls.Add(btnPlayPause);
            if (btnNext.Parent != panelBottomControls) panelBottomControls.Controls.Add(btnNext);
            if (btnLoop.Parent != panelBottomControls) panelBottomControls.Controls.Add(btnLoop);
            if (btnTimer.Parent != panelBottomControls) panelBottomControls.Controls.Add(btnTimer);
            if (btnVolume.Parent != panelBottomControls) panelBottomControls.Controls.Add(btnVolume);

            // Hide Maximized-only controls
            if (_topSectionCard != null) _topSectionCard.Visible = false;
            if (_lyricsCard != null) _lyricsCard.Visible = false;
            if (_activeQueuePanel != null) _activeQueuePanel.Visible = false;
            if (_sideMenuPanel != null) _sideMenuPanel.Visible = false;
            if (btnThumbnailMenu != null) btnThumbnailMenu.Visible = false;
            if (thumbnailActionPopup != null) thumbnailActionPopup.Visible = false;
            if (_collectionPickerPopup != null) _collectionPickerPopup.Visible = false;

            // Restore Artwork Box properties
            artworkBox.EnableModeToggleOnClick = true;
            artworkBox.ShowBottomTimeText = true;
            artworkBox.Cursor = Cursors.Hand;
            if (btnLyrics != null) btnLyrics.Visible = true;

            // Bottom controls layout
            musicTrackBar.Location = new Point(24, 14);
            musicTrackBar.Size = new Size(Math.Max(100, cardW - 48), 20);
            musicTrackBar.Visible = true;

            // Bottom controls layout: 7 buttons perfectly centered and evenly spaced
            // Left to right: [Download] [Volume] [Previous] [Play/Pause] [Next] [Loop] [Timer]
            int subBtnSize = 24;
            int playBtnSize = 34;
            int btnPlayY = 44;
            int subBtnY = btnPlayY + (playBtnSize - subBtnSize) / 2; // = 49 (vertically centered with Play/Pause)

            // Set all sizes FIRST so Width/Height are guaranteed exact
            btnPlayPause.Size = new Size(playBtnSize, playBtnSize);
            btnPrevious.Size = new Size(subBtnSize, subBtnSize);
            btnVolume.Size = new Size(subBtnSize, subBtnSize);
            btnQuickDownload.Size = new Size(subBtnSize, subBtnSize);
            btnNext.Size = new Size(subBtnSize, subBtnSize);
            btnLoop.Size = new Size(subBtnSize, subBtnSize);
            btnTimer.Size = new Size(subBtnSize, subBtnSize);

            // 6 gaps between 7 buttons. Span of buttons = 34 + 6 * 24 = 178px.
            // On a 480px card, remaining space = 302px. Ideal gap = 14px to 18px.
            int gap = Math.Max(10, Math.Min(18, (cardW - 178) / 14));
            int playX = (cardW - playBtnSize) / 2;

            // Center Play/Pause button
            btnPlayPause.Location = new Point(playX, btnPlayY);
            btnPlayPause.Visible = true;

            // Left side (Previous -> Volume -> Download)
            btnPrevious.Location = new Point(playX - gap - subBtnSize, subBtnY);
            btnPrevious.Visible = true;

            btnVolume.Location = new Point(btnPrevious.Left - gap - subBtnSize, subBtnY);
            btnVolume.Visible = true;

            btnQuickDownload.Location = new Point(btnVolume.Left - gap - subBtnSize, subBtnY);
            btnQuickDownload.Visible = true;

            // Right side (Next -> Loop -> Timer)
            btnNext.Location = new Point(playX + playBtnSize + gap, subBtnY);
            btnNext.Visible = true;

            btnLoop.Location = new Point(btnNext.Right + gap, subBtnY);
            btnLoop.Visible = true;

            btnTimer.Location = new Point(btnLoop.Right + gap, subBtnY);
            btnTimer.Visible = true;

            // Favorite Button placed on top-right of thumbnail card inside artworkBox (+10% larger)
            if (btnFavorite.Parent != artworkBox) artworkBox.Controls.Add(btnFavorite);
            int favSize = 27;
            btnFavorite.Size = new Size(favSize, favSize);
            Rectangle cardBounds = artworkBox.GetThumbnailCardBounds();
            btnFavorite.Location = new Point(cardBounds.Right - favSize - 6, cardBounds.Top + 6);
            btnFavorite.Visible = true;
            btnFavorite.BringToFront();

            lblCopyright.Location = new Point((cardW - lblCopyright.Width) / 2, bottomH - lblCopyright.Height - 6);
            lblVersion.Location = new Point(cardW - lblVersion.Width - 12, bottomH - lblVersion.Height - 6);

            // 5. gradientPanel fills between panelTopBar and panelBottomControls
            int midY = topBarH;
            int midH = Math.Max(50, panelBottomControls.Top - midY);
            gradientPanel.Location = new Point(0, midY);
            gradientPanel.Size = new Size(cardW, midH);

            // Lyrics Button placed in the same column as User Avatar, directly above trackbar
            if (btnLyrics != null)
            {
                if (btnLyrics.Parent != gradientPanel) gradientPanel.Controls.Add(btnLyrics);
                int lyricsBtnSize = 24;
                int lyricsX = btnUser.Left + (btnUser.Width - lyricsBtnSize) / 2;
                int lyricsY = Math.Max(10, gradientPanel.Height - lyricsBtnSize - 6);
                btnLyrics.Size = new Size(lyricsBtnSize, lyricsBtnSize);
                btnLyrics.Location = new Point(lyricsX, lyricsY);
                btnLyrics.Visible = true;
                btnLyrics.BringToFront();
            }

            UpdateCenterAreaLayout(_isLyricsOpen ? 1f : 0f);

            // VolumePopup position above btnVolume
            UpdateVolumePopupPosition();
        }

        private void UpdateVolumePopupPosition()
        {
            if (volumePopup == null || btnVolume == null || gradientPanel == null) return;
            try
            {
                if (volumePopup.Parent != gradientPanel)
                {
                    gradientPanel.Controls.Add(volumePopup);
                }

                Point btnScreen = btnVolume.PointToScreen(Point.Empty);
                Point btnInGrad = gradientPanel.PointToClient(btnScreen);

                int popX = btnInGrad.X + (btnVolume.Width - volumePopup.Width) / 2;
                int popY = btnInGrad.Y - volumePopup.Height - 6;

                if (popX < 4) popX = 4;
                if (popX + volumePopup.Width > gradientPanel.Width - 4)
                    popX = gradientPanel.Width - volumePopup.Width - 4;

                if (popY < 4) popY = 4;
                if (popY + volumePopup.Height > gradientPanel.Height - 4)
                    popY = gradientPanel.Height - volumePopup.Height - 4;

                volumePopup.Location = new Point(popX, popY);
                volumePopup.BringToFront();
            }
            catch { }
        }

        private void UpdateCenterAreaLayout(float ease)
        {
            if (_isMaximizedMode || WindowState == FormWindowState.Maximized)
            {
                ApplyMaximizedTemplate(ClientSize.Width, ClientSize.Height);
                return;
            }

            if (gradientPanel == null || artworkBox == null) return;
            int cardW = gradientPanel.Width;
            int midH = gradientPanel.Height;
            if (cardW <= 0 || midH <= 0) return;

            int closedArtSize = Math.Max(120, Math.Min(160, midH - 65));
            int closedArtX = (cardW - closedArtSize) / 2;
            int closedArtY = Math.Max(6, (midH - closedArtSize - 50) / 2);
            int closedTitleW = Math.Min(cardW - 40, closedArtSize + 80);
            int closedTitleX = (cardW - closedTitleW) / 2;

            int openArtSize = Math.Max(110, Math.Min(140, midH - 75));
            int openArtX = 14;
            int openArtY = Math.Max(8, (midH - openArtSize - 50) / 2);
            int openTitleX = openArtX;
            int openTitleW = openArtSize;

            int currentArtSize = (int)Math.Round(closedArtSize - (closedArtSize - openArtSize) * ease);
            int currentArtX = (int)Math.Round(closedArtX - (closedArtX - openArtX) * ease);
            int currentArtY = (int)Math.Round(closedArtY - (closedArtY - openArtY) * ease);
            int currentTitleX = (int)Math.Round(closedTitleX - (closedTitleX - openTitleX) * ease);
            int currentTitleW = (int)Math.Round(closedTitleW - (closedTitleW - openTitleW) * ease);

            artworkBox.Size = new Size(currentArtSize, currentArtSize);
            artworkBox.Location = new Point(currentArtX, currentArtY);

            marqueeTitle.Font = _titleFontNormal;
            marqueeAuthor.Font = _authorFontNormal;
            marqueeTitle.TextAlignment = (ease > 0.5f) ? StringAlignment.Near : StringAlignment.Center;
            marqueeAuthor.TextAlignment = (ease > 0.5f) ? StringAlignment.Near : StringAlignment.Center;
            marqueeTitle.Location = new Point(currentTitleX, artworkBox.Bottom + 6);
            marqueeTitle.Size = new Size(currentTitleW, 22);

            marqueeAuthor.Location = new Point(currentTitleX, marqueeTitle.Bottom + 2);
            marqueeAuthor.Size = new Size(currentTitleW, 18);

            // Keep Favorite button at top-right of thumbnail card inside artworkBox
            Rectangle cardBounds = artworkBox.GetThumbnailCardBounds();
            btnFavorite.Location = new Point(cardBounds.Right - btnFavorite.Width - 6, cardBounds.Top + 6);
            btnFavorite.BringToFront();

            // Keep Lyrics button at fixed column aligned with avatar above trackbar (never moves when lyrics open!)
            if (btnLyrics != null)
            {
                int lyricsBtnSize = 24;
                int lyricsX = btnUser.Left + (btnUser.Width - lyricsBtnSize) / 2;
                int lyricsY = Math.Max(10, gradientPanel.Height - lyricsBtnSize - 6);
                btnLyrics.Location = new Point(lyricsX, lyricsY);
                btnLyrics.BringToFront();
            }

            if (lyricsPanel != null)
            {
                if (ease <= 0.01f)
                {
                    lyricsPanel.Visible = false;
                }
                else
                {
                    int lyricsLeft = artworkBox.Right + 14;
                    int lyricsRightBound = (btnLyrics != null) ? btnLyrics.Left - 8 : cardW - 12;
                    int lyricsWidth = lyricsRightBound - lyricsLeft;
                    lyricsPanel.Location = new Point(lyricsLeft, 8);
                    lyricsPanel.Size = new Size(Math.Max(80, lyricsWidth), Math.Max(40, midH - 16));
                    lyricsPanel.Visible = true;
                    if (_searchResultsView != null && _searchResultsView.Visible)
                    {
                        _searchResultsView.BringToFront();
                    }
                    else
                    {
                        lyricsPanel.BringToFront();
                    }
                    if (btnLyrics != null) btnLyrics.BringToFront();
                }
            }
        }

        private Color GetTitleBarBackgroundColor()
        {
            return _isMaximizedMode ? Color.FromArgb(32, 12, 75) : Color.FromArgb(20, 16, 38);
        }

        private static GraphicsPath FormRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0) return path;
            int maxRadius = Math.Min(bounds.Width, bounds.Height) / 2;
            radius = Math.Max(1, Math.Min(radius, maxRadius));
            int diameter = radius * 2;
            if (diameter > 0 && bounds.Width >= diameter && bounds.Height >= diameter)
            {
                path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
            }
            else
            {
                path.AddRectangle(bounds);
            }
            return path;
        }

        internal class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkMenuColorTable()) { }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Selected && e.Item.Enabled)
                {
                    Rectangle rc = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
                    using (GraphicsPath path = RoundedRect(rc, 4))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 40, 90)))
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.FillPath(b, path);
                    }
                }
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Color.FromArgb(235, 235, 235) : Color.FromArgb(120, 120, 125);
                base.OnRenderItemText(e);

                // Right-aligned bare checkmark tick (only when checked)
                if (e.Item is ToolStripMenuItem mi && mi.Checked)
                {
                    Graphics g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    float right = e.Item.Width - 14;
                    float centerY = e.Item.Height / 2f;

                    using (Pen pen = new Pen(Color.FromArgb(210, 150, 255), 2.2f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, right - 10, centerY, right - 6, centerY + 4.5f);
                        g.DrawLine(pen, right - 6, centerY + 4.5f, right, centerY - 4.5f);
                    }
                }
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                int y = e.Item.Height / 2;
                using (Pen pen = new Pen(Color.FromArgb(48, 48, 56)))
                {
                    e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
                }
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (Pen pen = new Pen(Color.FromArgb(52, 52, 60)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                }
            }

            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
            {
                // Blank - no margin drawn
            }

            private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
            {
                GraphicsPath path = new GraphicsPath();
                if (bounds.Width <= 0 || bounds.Height <= 0) return path;
                int maxRadius = Math.Min(bounds.Width, bounds.Height) / 2;
                radius = Math.Max(1, Math.Min(radius, maxRadius));
                int diameter = radius * 2;
                if (diameter > 0 && bounds.Width >= diameter && bounds.Height >= diameter)
                {
                    path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
                    path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
                    path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                    path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                    path.CloseFigure();
                }
                else
                {
                    path.AddRectangle(bounds);
                }
                return path;
            }
        }

        internal class DarkMenuColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.FromArgb(24, 24, 28);
            public override Color MenuBorder => Color.FromArgb(52, 52, 60);
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuItemSelected => Color.FromArgb(60, 40, 90);
            public override Color MenuStripGradientBegin => Color.FromArgb(24, 24, 28);
            public override Color MenuStripGradientEnd => Color.FromArgb(24, 24, 28);
            public override Color SeparatorDark => Color.FromArgb(48, 48, 56);
            public override Color SeparatorLight => Color.Transparent;
        }
    
        private void StopAudioForTimer()
        {
            if (_audioPlayer != null && _audioPlayer.IsPlaying)
            {
                _audioPlayer.Pause();
            }
        }


        private ToolTip _userToolTip;

        private void btnUser_Click(object sender, EventArgs e)
        {
            if (accountPopup != null && (accountPopup.Visible || accountPopup.JustDeactivated))
            {
                accountPopup.Hide();
                this.Activate();
            }
            else
            {
                if (playlistPopup != null && playlistPopup.Visible)
                {
                    playlistPopup.Hide();
                    btnPlaylist.IsOpen = false;
                }
                ShowAccountPopup();
                this.Activate();
            }
        }

        private void ShowAccountPopup()
        {
            if (accountPopup == null || accountPopup.IsDisposed)
            {
                accountPopup = new AccountForm();
                accountPopup.Owner = this;
            }
            Screen scr = Screen.FromControl(this);
            Rectangle bounds = scr.WorkingArea;
            int x = this.Location.X + (this.Width - accountPopup.Width) / 2;
            int y = this.Location.Y + (this.Height - accountPopup.Height) / 2;
            x = Math.Max(bounds.Left, Math.Min(x, bounds.Right - accountPopup.Width));
            y = Math.Max(bounds.Top, Math.Min(y, bounds.Bottom - accountPopup.Height));
            accountPopup.Location = new Point(x, y);
            accountPopup.Show(this);
        }

        private ToolTip _loopToolTip;

        private void UpdateLoopTooltip()
        {
            // Hover tooltips disabled as requested
            if (_loopToolTip != null) _loopToolTip.RemoveAll();
        }

        private string GetLoopMenuText(LoopMode mode)
        {
            switch (mode)
            {
                case LoopMode.All: return "Lặp: Toàn bộ danh sách";
                case LoopMode.One: return "Lặp: 1 bài hát";
                case LoopMode.Shuffle: return "Lặp: Phát ngẫu nhiên";
                case LoopMode.Off:
                default: return "Lặp: Tắt (Tuần tự)";
            }
        }

        private void UpdateUserButtonAppearance()
        {
            if (_userToolTip != null) _userToolTip.RemoveAll();
            btnUser.Invalidate();
        }

    }}
