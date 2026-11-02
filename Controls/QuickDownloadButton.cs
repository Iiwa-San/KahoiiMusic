using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public class QuickDownloadButton : Control
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isSaved = false;
        private bool _isDownloading = false;
        private float _spinAngle = 0f;
        private Timer _animTimer;
        private readonly ToolTip _toolTip;

        private Color _inactiveColor = Color.FromArgb(220, 220, 220);
        private Color _inactiveHoverColor = Color.White;
        private Color _activeColor = Color.FromArgb(168, 85, 247);
        private Color _activeHoverColor = Color.FromArgb(192, 132, 252);

        private PlaylistItem _currentTrack = null;

        public QuickDownloadButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(28, 24);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            _toolTip = new ToolTip();
            _toolTip.ShowAlways = true;
            _toolTip.AutoPopDelay = 4000;
            _toolTip.InitialDelay = 350;
            _toolTip.ReshowDelay = 150;

            SavedTracksService.Instance.SavedTracksChanged += (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((Action)(() => RefreshState()));
                    }
                    catch { }
                }
            };

            UpdateToolTip();
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsSaved
        {
            get => _isSaved;
            set
            {
                if (_isSaved != value)
                {
                    _isSaved = value;
                    UpdateToolTip();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                if (_isDownloading != value)
                {
                    _isDownloading = value;
                    if (_isDownloading)
                    {
                        StartAnimation();
                    }
                    else
                    {
                        StopAnimation();
                    }
                    UpdateToolTip();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        public Color ActiveColor
        {
            get => _activeColor;
            set { _activeColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color InactiveColor
        {
            get => _inactiveColor;
            set { _inactiveColor = value; Invalidate(); }
        }

        public void SetCurrentTrack(PlaylistItem track)
        {
            _currentTrack = track;
            RefreshState();
        }

        public void RefreshState()
        {
            if (_currentTrack != null && !string.IsNullOrWhiteSpace(_currentTrack.Id))
            {
                _isSaved = SavedTracksService.Instance.IsTrackSaved(_currentTrack.Id);
            }
            else
            {
                _isSaved = false;
            }
            UpdateToolTip();
            Invalidate();
        }

        private void UpdateToolTip()
        {
            if (_toolTip == null) return;
            if (_isDownloading)
            {
                _toolTip.SetToolTip(this, "Đang tải bài hát về máy (Offline)...");
            }
            else if (_isSaved)
            {
                _toolTip.SetToolTip(this, "✓ Đã lưu bài hát này offline (Click để tùy chọn)");
            }
            else if (_currentTrack != null && !string.IsNullOrWhiteSpace(_currentTrack.Id))
            {
                _toolTip.SetToolTip(this, "Tải nhanh bài hát này về máy (Offline)");
            }
            else
            {
                _toolTip.SetToolTip(this, "Tải về nhanh (Chưa phát bài nào)");
            }
        }

        private void StartAnimation()
        {
            if (_animTimer == null)
            {
                _animTimer = new Timer();
                _animTimer.Interval = 40;
                _animTimer.Tick += (s, e) =>
                {
                    _spinAngle = (_spinAngle + 18f) % 360f;
                    Invalidate();
                };
            }
            _animTimer.Start();
        }

        private void StopAnimation()
        {
            if (_animTimer != null)
            {
                _animTimer.Stop();
            }
            _spinAngle = 0f;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPressed && e.Button == MouseButtons.Left)
            {
                _isPressed = false;
                Invalidate();
                HandleClickAction();
            }
        }

        private void HandleClickAction()
        {
            if (_isDownloading) return;

            if (_currentTrack == null || string.IsNullOrWhiteSpace(_currentTrack.Id))
            {
                _toolTip.Show("Hãy phát một bài hát để tải về máy.", this, 0, -30, 2000);
                return;
            }

            if (_isSaved)
            {
                ShowSavedOptionsMenu();
            }
            else
            {
                // Start instant save
                IsDownloading = true;
                SavedTracksService.Instance.SaveTrackAsync(_currentTrack, (success, msg) =>
                {
                    if (IsHandleCreated && !IsDisposed)
                    {
                        try
                        {
                            BeginInvoke((Action)(() =>
                            {
                                IsDownloading = false;
                                RefreshState();
                                if (success)
                                {
                                    _toolTip.Show("✓ Đã lưu thành công vào mục Đã tải!", this, 0, -30, 2500);
                                }
                                else if (!string.IsNullOrEmpty(msg))
                                {
                                    _toolTip.Show(msg, this, 0, -30, 3000);
                                }
                            }));
                        }
                        catch { }
                    }
                });
            }
        }

        private void ShowSavedOptionsMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(24, 24, 28);
            menu.ForeColor = Color.FromArgb(235, 235, 235);
            menu.Renderer = new Form1.DarkMenuRenderer();
            menu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            string title = _currentTrack?.Title;
            if (string.IsNullOrWhiteSpace(title) || title == "None") title = "Bài hát hiện tại";
            if (title.Length > 28) title = title.Substring(0, 25) + "...";

            var titleItem = new ToolStripMenuItem("✓ Đã lưu offline: " + title);
            titleItem.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            titleItem.Enabled = false;
            titleItem.ForeColor = Color.FromArgb(168, 85, 247);
            menu.Items.Add(titleItem);
            menu.Items.Add(new ToolStripSeparator());

            var itemDelete = new ToolStripMenuItem("Xóa bài này khỏi Đã tải");
            itemDelete.ForeColor = Color.FromArgb(239, 68, 68);
            itemDelete.Click += (s, e) =>
            {
                if (_currentTrack != null)
                {
                    SavedTracksService.Instance.DeleteSavedTrack(_currentTrack.Id);
                    RefreshState();
                    _toolTip.Show("Đã xóa khỏi danh sách đã lưu.", this, 0, -30, 2000);
                }
            };
            menu.Items.Add(itemDelete);

            var itemOpenFolder = new ToolStripMenuItem("Mở thư mục lưu nhạc");
            itemOpenFolder.Click += (s, e) =>
            {
                try
                {
                    if (Directory.Exists(AppPaths.SavedAudioDir))
                    {
                        Process.Start("explorer.exe", AppPaths.SavedAudioDir);
                    }
                }
                catch { }
            };
            menu.Items.Add(itemOpenFolder);

            Point location = new Point(0, -menu.PreferredSize.Height - 4);
            menu.Show(this, location);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Hover background pill
            if (_isHovered)
            {
                Color bg = _isPressed ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(30, 255, 255, 255);
                using (SolidBrush b = new SolidBrush(bg))
                using (GraphicsPath p = GetRoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 5))
                {
                    g.FillPath(b, p);
                }
            }

            Color drawColor;
            if (_isSaved)
            {
                drawColor = _isHovered ? _activeHoverColor : _activeColor;
            }
            else
            {
                drawColor = _isHovered ? _inactiveHoverColor : _inactiveColor;
            }

            if (_isPressed)
            {
                drawColor = Color.FromArgb(Math.Max(0, drawColor.R - 25),
                                           Math.Max(0, drawColor.G - 25),
                                           Math.Max(0, drawColor.B - 25));
            }

            float cx = Width / 2f;
            float cy = Height / 2f;

            if (_isDownloading)
            {
                // Animated spinner ring around center
                using (Pen pen = new Pen(Color.FromArgb(168, 85, 247), 1.8f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, cx - 6f, cy - 6f, 12f, 12f, _spinAngle, 270f);
                }
                // Small center arrow down
                using (Pen pen = new Pen(_inactiveHoverColor, 1.4f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, cx, cy - 3f, cx, cy + 2f);
                    g.DrawLine(pen, cx - 2f, cy, cx, cy + 2.2f);
                    g.DrawLine(pen, cx + 2f, cy, cx, cy + 2.2f);
                }
                return;
            }

            if (_isSaved)
            {
                // Base tray + Stylish Checkmark inside
                using (Pen pen = new Pen(drawColor, 1.8f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    // Tray
                    using (GraphicsPath tray = new GraphicsPath())
                    {
                        tray.AddLine(cx - 6.5f, cy + 3.0f, cx - 6.5f, cy + 6.2f);
                        tray.AddLine(cx - 6.5f, cy + 6.2f, cx + 6.5f, cy + 6.2f);
                        tray.AddLine(cx + 6.5f, cy + 6.2f, cx + 6.5f, cy + 3.0f);
                        g.DrawPath(pen, tray);
                    }

                    // Checkmark
                    g.DrawLine(pen, cx - 4.2f, cy - 0.2f, cx - 1.0f, cy + 3.0f);
                    g.DrawLine(pen, cx - 1.0f, cy + 3.0f, cx + 4.8f, cy - 4.0f);
                }
            }
            else
            {
                // Base tray + Download arrow
                using (Pen pen = new Pen(drawColor, 1.8f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    // Vertical arrow stem
                    g.DrawLine(pen, cx, cy - 6.5f, cx, cy + 2.0f);
                    // Chevron head
                    g.DrawLine(pen, cx - 3.8f, cy - 1.2f, cx, cy + 2.2f);
                    g.DrawLine(pen, cx + 3.8f, cy - 1.2f, cx, cy + 2.2f);

                    // Base tray
                    using (GraphicsPath tray = new GraphicsPath())
                    {
                        tray.AddLine(cx - 6.5f, cy + 2.5f, cx - 6.5f, cy + 6.0f);
                        tray.AddLine(cx - 6.5f, cy + 6.0f, cx + 6.5f, cy + 6.0f);
                        tray.AddLine(cx + 6.5f, cy + 6.0f, cx + 6.5f, cy + 2.5f);
                        g.DrawPath(pen, tray);
                    }
                }
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopAnimation();
                _animTimer?.Dispose();
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
