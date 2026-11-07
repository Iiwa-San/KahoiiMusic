using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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
        private volatile bool _isFormClosed = false;
        public bool IsFormClosed => _isFormClosed;

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

            // Enable double buffering on the Form to prevent any flicker
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

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

            accountPopup = new AccountForm();
            accountPopup.Owner = this;

            ToolTip menuTip = new ToolTip();
            menuTip.SetToolTip(btnPlaylist, "Menu (Danh sách phát, Lịch sử, Đã tải)");

            // Window Dragging
            MouseDown += Window_MouseDown;
            picLogo.MouseDown += Window_MouseDown;
            lblLogo.MouseDown += Window_MouseDown;
            panelTopBar.MouseDown += Window_MouseDown;
            lblCopyright.MouseDown += Window_MouseDown;

            // Title logo gradient matching logo_icon (violet to magenta/pink)
            lblLogo.Paint += (s, e) =>
            {
                if (lblLogo.Width <= 0 || lblLogo.Height <= 0) return;
                SizeF sz = e.Graphics.MeasureString(lblLogo.Text, lblLogo.Font);
                float gradWidth = Math.Max(sz.Width, 10f);
                using (LinearGradientBrush lgb = new LinearGradientBrush(
                    new RectangleF(0, 0, gradWidth, lblLogo.Height),
                    Color.FromArgb(180, 110, 255),
                    Color.FromArgb(245, 90, 190),
                    LinearGradientMode.Horizontal))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    using (SolidBrush bg = new SolidBrush(BackColor))
                    {
                        e.Graphics.FillRectangle(bg, 0, 0, lblLogo.Width, lblLogo.Height);
                    }
                    e.Graphics.DrawString(lblLogo.Text, lblLogo.Font, lgb, 0, 0);
                }
            };

            // Window Controls (Close & Minimize)
            btnClose.Click += (s, e) => Close();
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.FromArgb(255, 100, 100);
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(140, 140, 140);

            btnMinimize.Click += (s, e) => MinimizeToTray();
            btnMinimize.MouseEnter += (s, e) => btnMinimize.ForeColor = Color.White;
            btnMinimize.MouseLeave += (s, e) => btnMinimize.ForeColor = Color.FromArgb(140, 140, 140);

            _userToolTip = new ToolTip();
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
                if (btnQuickDownload != null && !string.IsNullOrEmpty(_audioPlayer.CurrentVideoId))
                {
                    btnQuickDownload.SetCurrentTrack(new PlaylistItem
                    {
                        Id = _audioPlayer.CurrentVideoId,
                        Title = cleanTitle,
                        Author = marqueeAuthor.Text,
                        DurationSeconds = (int)_audioPlayer.TotalDuration.TotalSeconds
                    });
                }
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
                if (btnQuickDownload != null)
                {
                    btnQuickDownload.SetCurrentTrack(new PlaylistItem
                    {
                        Id = args.VideoId,
                        Title = args.Title,
                        Author = marqueeAuthor.Text,
                        DurationSeconds = (int)args.Duration.TotalSeconds
                    });
                }
            };

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
                    volumePopup.BringToFront();
                }
            };

            volumePopup.VolumeChanged += (s, v) =>
            {
                _audioPlayer.Volume = v;
                btnVolume.Volume = v;
            };

            // Playlist Button and Popup Events
            btnPlaylist.Click += (s, e) =>
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
                    int x = this.Location.X + (this.Width - accountPopup.Width) / 2;
                    int y = this.Location.Y + (this.Height - accountPopup.Height) / 2;
                    accountPopup.Location = new Point(Math.Max(0, x), Math.Max(0, y));
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
                playlistPopup.SetCurrentIndex(idx);
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
        }

        public bool PreFilterMessage(ref Message m)
        {
            // Forward mouse wheel to playlistPopup if hovering over it, or adjust volume
            if (m.Msg == 0x020A /* WM_MOUSEWHEEL */)
            {
                if (playlistPopup != null && playlistPopup.Visible && playlistPopup.Bounds.Contains(Cursor.Position))
                {
                    if (m.HWnd != playlistPopup.Handle)
                    {
                        SendMessage(playlistPopup.Handle, m.Msg, (int)m.WParam, (int)m.LParam);
                        return true;
                    }
                }
                else
                {
                    Point cur = Cursor.Position;
                    bool isOverVolume = (volumePopup != null && volumePopup.Visible && volumePopup.Bounds.Contains(cur)) ||
                                        (panelBottomControls != null && panelBottomControls.RectangleToScreen(btnVolume.Bounds).Contains(cur)) ||
                                        (panelBottomControls != null && panelBottomControls.RectangleToScreen(panelBottomControls.ClientRectangle).Contains(cur));
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
                    if (!searchScreenRect.Contains(cur))
                    {
                        searchPillBox.RemoveFocus();
                    }
                }
            }
            return false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (searchPillBox != null && searchPillBox.ContainsFocus)
            {
                if (keyData == Keys.Escape)
                {
                    searchPillBox.RemoveFocus();
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

        private void Window_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
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
                if (!string.IsNullOrEmpty(input) && input != "link youtube..." && input != "linkYT")
                {
                    // Automatically load and play the link in the search box!
                    SearchPillBox_UrlSubmitted(searchPillBox, input);
                }
                else
                {
                    btnPlayPause.IsPlaying = false;
                    marqueeTitle.Text = "Hãy dán link YouTube vào ô tìm kiếm rồi bấm Play!";
                }
            }
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (_audioPlayer == null) return;
            // Only update trackbar position when audio is actually loaded and playing
            if (_audioPlayer.HasTrack)
            {
                if (!musicTrackBar.IsDragging && _audioPlayer.IsPlaying && _audioPlayer.TotalDuration > TimeSpan.Zero)
                {
                    musicTrackBar.Value = _audioPlayer.Progress;
                }
                artworkBox.TimeText = FormatTimeSpan(_audioPlayer.Position, _audioPlayer.TotalDuration);
            }
        }

        private void MusicTrackBar_SeekCommitted(object sender, float val)
        {
            if (_audioPlayer.HasTrack)
            {
                _audioPlayer.SeekNormalized(val);
                artworkBox.TimeText = FormatTimeSpan(_audioPlayer.Position, _audioPlayer.TotalDuration);
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
            if (string.IsNullOrWhiteSpace(url) || url == "link youtube..." || url == "linkYT")
            {
                _audioPlayer.StopAndClear();
                _audioPlayer.ClearPlaylist();
                artworkBox.ClearImage();
                artworkBox.TimeText = string.Empty;
                musicTrackBar.Value = 0f;
                btnPlayPause.IsPlaying = false;
                playbackTimer.Stop();
                marqueeTitle.Text = string.Empty;
                marqueeAuthor.Text = string.Empty;
                btnPlaylist.HasPlaylist = false;
                btnPlaylist.IsOpen = false;
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

                // If currently playing audio, DO NOT interrupt the song being played!
                // Clear old playlist from queue and load new playlist in background.
                // The current song continues playing and is kept in history.
                if (_audioPlayer.IsPlaying || !string.IsNullOrEmpty(_audioPlayer.CurrentVideoId))
                {
                    if (!string.IsNullOrEmpty(_audioPlayer.CurrentVideoId))
                    {
                        HistoryService.AddTrack(new PlaylistItem
                        {
                            Id = _audioPlayer.CurrentVideoId,
                            Title = _audioPlayer.CurrentTitle,
                            Author = _audioPlayer.CurrentAuthor,
                            DurationSeconds = (int)_audioPlayer.TotalDuration.TotalSeconds
                        });
                    }

                    _audioPlayer.LoadPlaylistQueueOnly(url);
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(videoId))
            {
                if (_audioPlayer.IsPlaying || !string.IsNullOrEmpty(_audioPlayer.CurrentVideoId))
                {
                    btnPlaylist.HasPlaylist = true;
                    playlistPopup.StartLoading();

                    if (!string.IsNullOrEmpty(_audioPlayer.CurrentVideoId))
                    {
                        HistoryService.AddTrack(new PlaylistItem
                        {
                            Id = _audioPlayer.CurrentVideoId,
                            Title = _audioPlayer.CurrentTitle,
                            Author = _audioPlayer.CurrentAuthor,
                            DurationSeconds = (int)_audioPlayer.TotalDuration.TotalSeconds
                        });
                    }

                    _audioPlayer.LoadSingleTrackQueueOnly(videoId, url);
                    return;
                }
            }
            else
            {
                // Standalone track -> DO NOT wipe out user's session playlist in Tab 0!
                btnPlaylist.HasPlaylist = (_audioPlayer.HasSessionPlaylist || (playlistPopup.PlaylistItems != null && playlistPopup.PlaylistItems.Count > 0));
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Subtle border around the form for crisp definition on dark desktops
            using (Pen borderPen = new Pen(Color.FromArgb(38, 38, 38), 1))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000; // WS_MINIMIZEBOX (enables minimize from taskbar)
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

            Color topColor = gradientPanel.TopColor;
            Color bottomColor = gradientPanel.BottomColor;

            // Soft darkened tones for top and bottom ("làm mờ tự nhiên")
            Color darkTop = Color.FromArgb(
                Math.Max(16, (int)(topColor.R * 0.32f)),
                Math.Max(14, (int)(topColor.G * 0.32f)),
                Math.Max(26, (int)(topColor.B * 0.32f)));

            Color darkBottom = Color.FromArgb(
                Math.Max(28, (int)(bottomColor.R * 0.30f)),
                Math.Max(14, (int)(bottomColor.G * 0.30f)),
                Math.Max(22, (int)(bottomColor.B * 0.30f)));

            using (LinearGradientBrush lgb = new LinearGradientBrush(
                new Rectangle(0, 0, w, h),
                Color.Black, Color.Black,
                LinearGradientMode.Vertical))
            {
                ColorBlend cb = new ColorBlend(4);
                cb.Colors = new Color[] { darkTop, topColor, bottomColor, darkBottom };
                cb.Positions = new float[] { 0.0f, 0.15f, 0.65f, 1.0f };
                lgb.InterpolationColors = cb;
                e.Graphics.FillRectangle(lgb, 0, 0, w, h);
            }
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

                _trayMenu.Items.Add(_trayItemLoop);
                _trayMenu.Items.Add(trayItemTimer);
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
                Show();
                if (WindowState == FormWindowState.Minimized)
                {
                    WindowState = FormWindowState.Normal;
                }
                ShowWindow(this.Handle, SW_RESTORE);
                BringToFront();
                Activate();
                SetForegroundWindow(this.Handle);
                TopMost = true;
                TopMost = false;
            }
            catch { }
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

        private void HandleIncomingUrl(string url)
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
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        var res = await FirebaseAuthService.Instance.SignInWithRefreshTokenAsync(commonRt, commonUser);
                        if (res.Item1)
                        {
                            BeginInvoke(new Action(() =>
                            {
                                _audioPlayer?.RaiseStatus(string.Format("✓ Đã đăng nhập nhanh từ Extension: {0}", FirebaseAuthService.Instance.CurrentUser?.email));
                                _ = FirebaseSyncService.Instance.TriggerAutoFetchAsync();
                            }));
                        }
                    });
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
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            var res = await FirebaseAuthService.Instance.SignInWithRefreshTokenAsync(commonRt, commonUser);
                            if (res.Item1)
                            {
                                BeginInvoke(new Action(() =>
                                {
                                    _audioPlayer?.RaiseStatus(string.Format("✓ Đã chuyển sang tài khoản: {0}", FirebaseAuthService.Instance.CurrentUser?.email));
                                    _ = FirebaseSyncService.Instance.TriggerAutoFetchAsync();
                                }));
                            }
                        });
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
                System.Threading.Tasks.Task.Run(() => FirebaseSyncService.Instance.TriggerAutoFetchAsync());
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
                            }
                        }
                    }
                }
                catch { }

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

                if (!string.IsNullOrEmpty(targetUrl))
                {
                    SavePlaylistUrlToCollectionAsync(targetUrl, playlistName);
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
                                if (k.Equals("url", StringComparison.OrdinalIgnoreCase)) { targetUrlToPlay = v; break; }
                                if (k.Equals("v", StringComparison.OrdinalIgnoreCase)) { targetUrlToPlay = "https://www.youtube.com/watch?v=" + v; break; }
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

            searchPillBox.Text = targetUrlToPlay;
            SearchPillBox_UrlSubmitted(this, targetUrlToPlay);
        }

        private void SavePlaylistUrlToCollectionAsync(string playlistUrl, string customName)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
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
                    string detectedTitle = customName;

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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                MinimizeToTray();
            }
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
            int x = this.Location.X + (this.Width - accountPopup.Width) / 2;
            int y = this.Location.Y + (this.Height - accountPopup.Height) / 2;
            accountPopup.Location = new Point(Math.Max(0, x), Math.Max(0, y));
            accountPopup.Show(this);
        }

        private ToolTip _loopToolTip;

        private void UpdateLoopTooltip()
        {
            if (btnLoop == null) return;
            if (_loopToolTip == null) _loopToolTip = new ToolTip();
            string tip;
            switch (btnLoop.Mode)
            {
                case LoopMode.All:
                    tip = "Chế độ: Lặp lại danh sách (Bấm để đổi)";
                    break;
                case LoopMode.One:
                    tip = "Chế độ: Lặp lại 1 bài (Bấm để đổi)";
                    break;
                case LoopMode.Shuffle:
                    tip = "Chế độ: Phát ngẫu nhiên & Lặp lại khi hết (Bấm để đổi)";
                    break;
                case LoopMode.Off:
                default:
                    tip = "Chế độ: Phát tuần tự / Tắt lặp (Bấm để đổi)";
                    break;
            }
            _loopToolTip.SetToolTip(btnLoop, tip);
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
            if (FirebaseAuthService.Instance.IsLoggedIn)
            {
                _userToolTip?.SetToolTip(btnUser, "Tài khoản: " + FirebaseAuthService.Instance.CurrentUser.email);
            }
            else
            {
                _userToolTip?.SetToolTip(btnUser, "Đăng nhập / Đăng ký tài khoản Kahoii");
            }
            btnUser.Invalidate();
        }

    }}
