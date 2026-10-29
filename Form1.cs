using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using KahoiiMusic.Services;

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

        private AudioPlayerService _audioPlayer;
        private Controls.PlaylistPopup playlistPopup;
        private System.Windows.Forms.Timer _ramTimer;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayItemLoop;

        public Form1() : this(null)
        {
        }

        public Form1(string initialUrl)
        {
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

            SetupEvents();
            InitializeTrayIcon();

            if (!string.IsNullOrEmpty(initialUrl))
            {
                Shown += delegate
                {
                    searchPillBox.Text = initialUrl;
                    _audioPlayer.LoadAndPlay(initialUrl);
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

            // Periodic lightweight memory trimming timer (every 30s) running on threadpool to avoid any animation hitch
            _ramTimer = new Timer();
            _ramTimer.Interval = 30000;
            _ramTimer.Tick += (s, e) => MemoryOptimizer.TrimMemoryLight();
            _ramTimer.Start();

            // Audio Player Events
            _audioPlayer.StatusMessageChanged += AudioPlayer_StatusMessageChanged;
            _audioPlayer.TitleChanged += (s, title) =>
            {
                marqueeTitle.Text = title;
                UpdateTrayTooltip(title);
            };
            _audioPlayer.ThumbnailUrlReady += AudioPlayer_ThumbnailUrlReady;
            _audioPlayer.PlaybackStateChanged += AudioPlayer_PlaybackStateChanged;
            _audioPlayer.PlaybackEnded += AudioPlayer_PlaybackEnded;
            _audioPlayer.PlaybackLooped += AudioPlayer_PlaybackLooped;

            // Loop Button
            btnLoop.LoopChanged += (s, e) =>
            {
                _audioPlayer.IsLooping = btnLoop.IsLooping;
                if (_trayItemLoop != null)
                {
                    _trayItemLoop.Checked = btnLoop.IsLooping;
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
                if (playlistPopup.Visible || playlistPopup.JustDeactivated)
                {
                    playlistPopup.Hide();
                    btnPlaylist.IsOpen = false;
                    this.Activate();
                }
                else
                {
                    Point screenPt = panelTopBar.PointToScreen(new Point(btnPlaylist.Left, btnPlaylist.Bottom + 4));
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
                    playlistPopup.Location = panelTopBar.PointToScreen(new Point(btnPlaylist.Left, btnPlaylist.Bottom + 4));
                }
            };

            playlistPopup.TrackSelected += (s, item) =>
            {
                if (item != null)
                {
                    int idx = _audioPlayer.Playlist.FindIndex(x => x.Id == item.Id);
                    if (idx >= 0)
                    {
                        _audioPlayer.PlayTrackAtIndex(idx);
                    }
                    else
                    {
                        _audioPlayer.LoadAndPlay(item.Url);
                    }
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
            // Forward mouse wheel to playlistPopup if hovering over it
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
            if (string.IsNullOrEmpty(status))
            {
                if (!string.IsNullOrEmpty(_audioPlayer.CurrentTitle))
                {
                    marqueeTitle.Text = _audioPlayer.CurrentTitle;
                }
            }
            else
            {
                if (!_audioPlayer.HasTrack || string.IsNullOrEmpty(_audioPlayer.CurrentTitle))
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
            btnPlayPause.IsPlaying = _audioPlayer.IsPlaying;
            playbackTimer.Enabled = _audioPlayer.IsPlaying;
            artworkBox.IsSpinning = _audioPlayer.IsPlaying;
            if (_audioPlayer.HasTrack)
            {
                artworkBox.TimeText = FormatTimeSpan(_audioPlayer.Position, _audioPlayer.TotalDuration);
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
            if (_audioPlayer.HasPlaylist)
            {
                _audioPlayer.PlayPrevious();
            }
            else if (_audioPlayer.HasTrack)
            {
                _audioPlayer.Position = TimeSpan.Zero;
                musicTrackBar.Value = 0f;
                artworkBox.TimeText = FormatTimeSpan(TimeSpan.Zero, _audioPlayer.TotalDuration);
            }
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (_audioPlayer.HasPlaylist)
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
                btnPlaylist.HasPlaylist = false;
                btnPlaylist.IsOpen = false;
                playlistPopup.Visible = false;
                playlistPopup.Clear();
                return;
            }

            string videoId = AudioPlayerService.ExtractVideoId(url);
            string listId = AudioPlayerService.ExtractListId(url);

            // 1. If the video exists in the currently loaded playlist:
            if (!string.IsNullOrEmpty(videoId) && _audioPlayer.Playlist != null && _audioPlayer.Playlist.Count > 0)
            {
                int existingIdx = _audioPlayer.Playlist.FindIndex(x => x.Id == videoId);
                if (existingIdx >= 0)
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
            }
            else
            {
                // Standalone track not in current playlist -> Clear previous playlist
                _audioPlayer.ClearPlaylist();
                btnPlaylist.HasPlaylist = false;
                btnPlaylist.IsOpen = false;
                playlistPopup.Visible = false;
                playlistPopup.Clear();
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
            base.OnFormClosing(e);
            Application.RemoveMessageFilter(this);

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
            if (_audioPlayer != null)
            {
                _audioPlayer.Dispose();
                _audioPlayer = null;
            }
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
                    bool newLoop = _trayItemLoop.Checked;
                    if (btnLoop != null) btnLoop.IsLooping = newLoop;
                    if (_audioPlayer != null) _audioPlayer.IsLooping = newLoop;
                };

                var itemExit = new ToolStripMenuItem("Tho\u00e1t");
                itemExit.Click += (s, e) => Close();

                _trayMenu.Items.Add(itemOpen);
                _trayMenu.Items.Add(new ToolStripSeparator());
                _trayMenu.Items.Add(itemPlayPause);
                _trayMenu.Items.Add(itemNext);
                _trayMenu.Items.Add(itemPrevious);
                _trayMenu.Items.Add(_trayItemLoop);
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

        private void RestoreFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            BringToFront();
            Activate();
            TopMost = true;
            TopMost = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                MinimizeToTray();
            }
        }

        private class DarkMenuRenderer : ToolStripProfessionalRenderer
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
                int diameter = radius * 2;
                GraphicsPath path = new GraphicsPath();
                path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                return path;
            }
        }

        private class DarkMenuColorTable : ProfessionalColorTable
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
    }
}
