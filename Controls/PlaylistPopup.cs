using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public enum MenuTab
    {
        Playlist = 0,
        Collection = 1,
        Favorite = 1,
        History = 2,
        Saved = 3
    }

    public class PlaylistPopup : Form
    {
        private SideMenuPanel _sideMenuPanel;
        private DateTime _deactivatedTime = DateTime.MinValue;
        private bool _isLoading = false;

        public bool JustDeactivated => (DateTime.UtcNow - _deactivatedTime).TotalMilliseconds < 250;
        public bool IsLoading => _isLoading;

        public event EventHandler<PlaylistItem> TrackSelected;
        public event EventHandler CloseRequested;

        public AudioPlayerService AudioPlayer
        {
            get => _sideMenuPanel?.AudioPlayer;
            set
            {
                if (_sideMenuPanel != null) _sideMenuPanel.AudioPlayer = value;
            }
        }

        public MenuTab ActiveTab
        {
            get => (MenuTab)(int)(_sideMenuPanel?.ActiveTab ?? SideTab.Queue);
            set
            {
                if (_sideMenuPanel != null)
                {
                    int tabIdx = (int)value;
                    if (tabIdx >= 0 && tabIdx <= 3)
                    {
                        _sideMenuPanel.ActiveTab = (SideTab)tabIdx;
                    }
                }
            }
        }

        public List<PlaylistItem> PlaylistItems => AudioPlayer?.SessionPlaylist ?? new List<PlaylistItem>();
        public int CurrentPlaylistIndex => AudioPlayer?.CurrentPlaylistIndex ?? -1;

        public PlaylistPopup()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(20, 16, 38);
            Size = new Size(560, 450);

            _sideMenuPanel = new SideMenuPanel
            {
                IsPopup = true,
                Dock = DockStyle.Fill
            };

            _sideMenuPanel.TrackSelected += (s, item) =>
            {
                TrackSelected?.Invoke(this, item);
            };

            _sideMenuPanel.CloseRequested += (s, e) =>
            {
                Hide();
            };

            Controls.Add(_sideMenuPanel);
            UpdateRegion();
        }

        private bool _isHiding = false;

        public new void Show(IWin32Window owner)
        {
            _deactivatedTime = DateTime.MinValue;
            UpdateRegion();
            base.Show(owner);
        }

        public new void Show()
        {
            _deactivatedTime = DateTime.MinValue;
            UpdateRegion();
            base.Show();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public new void Hide()
        {
            if (_isHiding || !Visible) return;
            _isHiding = true;
            try
            {
                base.Hide();

                if (Owner != null && !Owner.IsDisposed && Owner.Visible)
                {
                    try
                    {
                        IntPtr fg = GetForegroundWindow();
                        if (fg == IntPtr.Zero || fg == Handle || fg == Owner.Handle)
                        {
                            Owner.Activate();
                            SetForegroundWindow(Owner.Handle);
                        }
                    }
                    catch { }
                }

                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _isHiding = false;
            }
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                cp.ExStyle |= 0x00000080;    // WS_EX_TOOLWINDOW: belongs strictly to owner window stack, prevents jumping to other apps
                return cp;
            }
        }

        private bool _isUpdatingRegion = false;
        private Size _lastRegionSize = Size.Empty;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            if (_isUpdatingRegion) return;
            if (Width <= 0 || Height <= 0) return;
            if (Size == _lastRegionSize) return;

            _isUpdatingRegion = true;
            try
            {
                _lastRegionSize = Size;
                using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), 14))
                {
                    Region = new Region(path);
                }
            }
            catch { }
            finally
            {
                _isUpdatingRegion = false;
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rc, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rc.X, rc.Y, d, d, 180, 90);
            path.AddArc(rc.Right - d, rc.Y, d, d, 270, 90);
            path.AddArc(rc.Right - d, rc.Bottom - d, d, d, 0, 90);
            path.AddArc(rc.X, rc.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public void SetTheme(DynamicThemePalette theme)
        {
            _sideMenuPanel?.SetTheme(theme);
            Invalidate();
        }

        public void SetPlaylist(List<PlaylistItem> items, int currentIndex = 0)
        {
            _isLoading = false;
            _sideMenuPanel?.RefreshData();
        }

        public void SetCurrentIndex(int currentIndex)
        {
            _sideMenuPanel?.Invalidate();
        }

        public void Clear()
        {
            _isLoading = false;
            _sideMenuPanel?.RefreshData();
        }

        public void StartLoading()
        {
            _isLoading = true;
            _sideMenuPanel?.RefreshData();
        }

        public void RefreshPlaylists()
        {
            _sideMenuPanel?.RefreshData();
        }

        public void RefreshFavorites()
        {
            _sideMenuPanel?.RefreshData();
        }

        public void RefreshHistory()
        {
            _sideMenuPanel?.RefreshData();
        }

        public void RefreshSaved()
        {
            _sideMenuPanel?.RefreshData();
        }

        public void RefreshData()
        {
            _sideMenuPanel?.RefreshData();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Hide();
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _deactivatedTime = DateTime.UtcNow;

            if (!Visible || _isHiding) return;

            Point cur = Cursor.Position;
            if (this.Bounds.Contains(cur)) return;

            // Defer Hide() via BeginInvoke so Windows completes focus transition without activating another app
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (Visible && !_isHiding)
                    {
                        Hide();
                    }
                }));
            }
            catch
            {
                Hide();
            }
        }

        public SideMenuPanel SideMenu => _sideMenuPanel;
    }
}
