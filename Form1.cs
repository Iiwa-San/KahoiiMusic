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
            // Window Dragging
            MouseDown += Window_MouseDown;
            picLogo.MouseDown += Window_MouseDown;
            lblLogo.MouseDown += Window_MouseDown;
            panelTopBar.MouseDown += Window_MouseDown;

            // Window Controls (Close & Minimize)
            btnClose.Click += (s, e) => Close();
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.FromArgb(255, 100, 100);
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(140, 140, 140);

            btnMinimize.Click += (s, e) =>
            {
                WindowState = FormWindowState.Minimized;
                MemoryOptimizer.TrimMemoryAsync(50);
            };
            btnMinimize.MouseEnter += (s, e) => btnMinimize.ForeColor = Color.White;
            btnMinimize.MouseLeave += (s, e) => btnMinimize.ForeColor = Color.FromArgb(140, 140, 140);

            // Periodic memory trimming timer (every 30s) to keep RAM usage minimal
            Timer ramTimer = new Timer();
            ramTimer.Interval = 30000;
            ramTimer.Tick += (s, e) => MemoryOptimizer.TrimMemory();
            ramTimer.Start();

            // Audio Player Events
            _audioPlayer.StatusMessageChanged += AudioPlayer_StatusMessageChanged;
            _audioPlayer.TitleChanged += (s, title) =>
            {
                marqueeTitle.Text = title;
            };
            _audioPlayer.ThumbnailUrlReady += AudioPlayer_ThumbnailUrlReady;
            _audioPlayer.PlaybackStateChanged += AudioPlayer_PlaybackStateChanged;
            _audioPlayer.PlaybackEnded += AudioPlayer_PlaybackEnded;
            _audioPlayer.PlaybackLooped += AudioPlayer_PlaybackLooped;

            // Loop Button
            btnLoop.LoopChanged += (s, e) =>
            {
                _audioPlayer.IsLooping = btnLoop.IsLooping;
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
                if (playlistPopup.Visible)
                {
                    playlistPopup.Hide();
                    btnPlaylist.IsOpen = false;
                }
                else
                {
                    Point screenPt = panelTopBar.PointToScreen(new Point(btnPlaylist.Left, btnPlaylist.Bottom + 4));
                    playlistPopup.Location = screenPt;
                    playlistPopup.Show(this);
                    btnPlaylist.IsOpen = true;
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
            using (Pen borderPen = new Pen(Color.FromArgb(45, 45, 45), 1))
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

        private void gradientPanel_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panelBottomControls_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
