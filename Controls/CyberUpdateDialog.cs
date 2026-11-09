using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public class CyberUpdateDialog : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private readonly AppUpdateInfo _updateInfo;
        private CancellationTokenSource _cts;
        private string _downloadedFilePath = null;
        private bool _isDownloading = false;

        // UI Controls
        private Label _lblClose;
        private Label _lblBadgeIcon;
        private Label _lblTitle;
        private Label _lblVersionBadge;
        private Label _lblChangelogTitle;
        private Panel _pnlChangelog;
        private Label _lblChangelog;

        // Progress Controls
        private Panel _pnlProgressContainer;
        private Label _lblProgressStatus;
        private Panel _pnlProgressBar;
        private Label _lblProgressDetails;
        private int _currentProgressPercent = 0;

        // Action Buttons
        private Button _btnPrimary;
        private Button _btnSecondary;

        public static void ShowUpdate(IWin32Window owner, AppUpdateInfo info)
        {
            if (info == null) return;

            if (owner is Control ctrl && ctrl.InvokeRequired)
            {
                ctrl.BeginInvoke(new Action(() => ShowUpdate(owner, info)));
                return;
            }

            try
            {
                using (var dialog = new CyberUpdateDialog(info))
                {
                    if (owner is Form ownerForm && ownerForm.Visible)
                    {
                        dialog.StartPosition = FormStartPosition.Manual;
                        int x = ownerForm.Location.X + (ownerForm.Width - dialog.Width) / 2;
                        int y = ownerForm.Location.Y + (ownerForm.Height - dialog.Height) / 2;
                        Screen scr = Screen.FromControl(ownerForm);
                        Rectangle bounds = scr.WorkingArea;
                        x = Math.Max(bounds.Left, Math.Min(x, bounds.Right - dialog.Width));
                        y = Math.Max(bounds.Top, Math.Min(y, bounds.Bottom - dialog.Height));
                        dialog.Location = new Point(x, y);
                    }
                    dialog.ShowDialog(owner);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CyberUpdateDialog] ShowDialog error: {ex.Message}");
            }
        }

        public CyberUpdateDialog(AppUpdateInfo updateInfo)
        {
            _updateInfo = updateInfo ?? throw new ArgumentNullException(nameof(updateInfo));

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(20, 16, 31);
            Size = new Size(490, 470);

            // True rounded shape
            Region = new Region(GetRoundedRect(new Rectangle(0, 0, Width, Height), 18));

            // Drag window
            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };

            // Close '✕' Button
            _lblClose = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(145, 145, 175),
                Location = new Point(Width - 40, 14),
                Size = new Size(26, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent,
                Visible = !_updateInfo.ForceUpdate
            };
            _lblClose.MouseEnter += (s, e) => _lblClose.ForeColor = Color.FromArgb(255, 45, 117);
            _lblClose.MouseLeave += (s, e) => _lblClose.ForeColor = Color.FromArgb(145, 145, 175);
            _lblClose.Click += (s, e) => OnCancelOrDismiss();
            Controls.Add(_lblClose);

            // Icon Header (Rocket circular badge)
            _lblBadgeIcon = new Label
            {
                Text = "🚀",
                Font = new Font("Segoe UI Emoji", 20f),
                Location = new Point((Width - 58) / 2, 22),
                Size = new Size(58, 58),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            Controls.Add(_lblBadgeIcon);

            // Title Label
            _lblTitle = new Label
            {
                Text = "Đã Có Bản Cập Nhật Mới!",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 86),
                Size = new Size(Width - 40, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            Controls.Add(_lblTitle);

            // Version Badge Pill
            _lblVersionBadge = new Label
            {
                Text = $"Phiên bản v{_updateInfo.VersionName}",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 45, 117),
                BackColor = Color.FromArgb(45, 20, 40),
                AutoSize = false,
                Size = new Size(160, 28),
                Location = new Point((Width - 160) / 2, 118),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _lblVersionBadge.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = GetRoundedRect(_lblVersionBadge.ClientRectangle, 12))
                {
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(55, 20, 45)))
                    {
                        e.Graphics.FillPath(bg, path);
                    }
                    using (Pen borderPen = new Pen(Color.FromArgb(140, 255, 45, 117), 1f))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }
                TextRenderer.DrawText(e.Graphics, _lblVersionBadge.Text, _lblVersionBadge.Font, _lblVersionBadge.ClientRectangle, _lblVersionBadge.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            Controls.Add(_lblVersionBadge);

            // Changelog Section
            _lblChangelogTitle = new Label
            {
                Text = "Nội dung cập nhật:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 225, 240),
                Location = new Point(34, 156),
                Size = new Size(Width - 68, 20),
                BackColor = Color.Transparent
            };
            Controls.Add(_lblChangelogTitle);

            _pnlChangelog = new Panel
            {
                Location = new Point(34, 180),
                Size = new Size(Width - 68, 195),
                BackColor = Color.FromArgb(26, 20, 40)
            };
            _pnlChangelog.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = GetRoundedRect(new Rectangle(0, 0, _pnlChangelog.Width - 1, _pnlChangelog.Height - 1), 10))
                {
                    using (Pen pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f))
                    {
                        e.Graphics.DrawPath(pen, p);
                    }
                }
            };

            _pnlChangelog.AutoScroll = true;

            _lblChangelog = new Label
            {
                Location = new Point(14, 14),
                AutoSize = true,
                MaximumSize = new Size(_pnlChangelog.Width - 28, 0),
                ForeColor = Color.FromArgb(220, 225, 240),
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.Transparent,
                Text = string.IsNullOrWhiteSpace(_updateInfo.Changelog) ? "• Cải thiện hiệu năng và sửa các lỗi nhỏ." : _updateInfo.Changelog
            };
            _pnlChangelog.Controls.Add(_lblChangelog);
            Controls.Add(_pnlChangelog);

            // Progress Section (hidden initially)
            _pnlProgressContainer = new Panel
            {
                Location = new Point(34, 200),
                Size = new Size(Width - 68, 160),
                BackColor = Color.Transparent,
                Visible = false
            };

            _lblProgressStatus = new Label
            {
                Text = "Đang tải bản cập nhật...",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 20),
                Size = new Size(_pnlProgressContainer.Width, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            _pnlProgressContainer.Controls.Add(_lblProgressStatus);

            _pnlProgressBar = new Panel
            {
                Location = new Point(15, 60),
                Size = new Size(_pnlProgressContainer.Width - 30, 18),
                BackColor = Color.FromArgb(32, 26, 48)
            };
            _pnlProgressBar.Paint += OnPaintProgressBar;
            _pnlProgressContainer.Controls.Add(_pnlProgressBar);

            _lblProgressDetails = new Label
            {
                Text = "0%  •  0 MB",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(170, 175, 200),
                Location = new Point(0, 92),
                Size = new Size(_pnlProgressContainer.Width, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            _pnlProgressContainer.Controls.Add(_lblProgressDetails);
            Controls.Add(_pnlProgressContainer);

            // Primary Button ("Cập nhật ngay 🚀")
            _btnPrimary = new Button
            {
                Text = "Cập nhật ngay 🚀",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(160, 42),
                Location = new Point(Width - 34 - 160, Height - 64),
                Cursor = Cursors.Hand
            };
            _btnPrimary.FlatAppearance.BorderSize = 0;
            _btnPrimary.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush br = new LinearGradientBrush(
                    _btnPrimary.ClientRectangle,
                    Color.FromArgb(255, 45, 117),
                    Color.FromArgb(124, 58, 237),
                    LinearGradientMode.Horizontal))
                {
                    using (GraphicsPath p = GetRoundedRect(new Rectangle(0, 0, _btnPrimary.Width, _btnPrimary.Height), 8))
                    {
                        e.Graphics.FillPath(br, p);
                    }
                }
                TextRenderer.DrawText(e.Graphics, _btnPrimary.Text, _btnPrimary.Font, _btnPrimary.ClientRectangle, _btnPrimary.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            _btnPrimary.Click += async (s, e) => await OnPrimaryButtonClickAsync();
            Controls.Add(_btnPrimary);

            // Secondary Button ("Để sau")
            _btnSecondary = new Button
            {
                Text = "Để sau",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(180, 185, 210),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(116, 42),
                Location = new Point(Width - 34 - 160 - 12 - 116, Height - 64),
                Cursor = Cursors.Hand,
                Visible = !_updateInfo.ForceUpdate
            };
            _btnSecondary.FlatAppearance.BorderSize = 0;
            _btnSecondary.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush br = new SolidBrush(Color.FromArgb(34, 28, 50)))
                {
                    using (GraphicsPath p = GetRoundedRect(new Rectangle(0, 0, _btnSecondary.Width, _btnSecondary.Height), 8))
                    {
                        e.Graphics.FillPath(br, p);
                        using (Pen borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f))
                        {
                            e.Graphics.DrawPath(borderPen, p);
                        }
                    }
                }
                TextRenderer.DrawText(e.Graphics, _btnSecondary.Text, _btnSecondary.Font, _btnSecondary.ClientRectangle, _btnSecondary.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            _btnSecondary.Click += (s, e) => OnCancelOrDismiss();
            Controls.Add(_btnSecondary);

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && !_updateInfo.ForceUpdate && !_isDownloading)
                {
                    OnCancelOrDismiss();
                }
            };
            KeyPreview = true;
        }

        private void OnPaintProgressBar(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background
            using (GraphicsPath bgPath = GetRoundedRect(_pnlProgressBar.ClientRectangle, 6))
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(32, 26, 48)))
                {
                    g.FillPath(bgBrush, bgPath);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1f))
                {
                    g.DrawPath(borderPen, bgPath);
                }
            }

            // Fill
            if (_currentProgressPercent > 0)
            {
                int fillWidth = Math.Max(12, (_pnlProgressBar.Width * _currentProgressPercent) / 100);
                Rectangle fillRect = new Rectangle(0, 0, fillWidth, _pnlProgressBar.Height);
                using (GraphicsPath fillPath = GetRoundedRect(fillRect, 6))
                {
                    using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                        fillRect,
                        Color.FromArgb(255, 45, 117),
                        Color.FromArgb(124, 58, 237),
                        LinearGradientMode.Horizontal))
                    {
                        g.FillPath(fillBrush, fillPath);
                    }
                }
            }
        }

        private async Task OnPrimaryButtonClickAsync()
        {
            if (_downloadedFilePath != null && File.Exists(_downloadedFilePath))
            {
                // Install now
                AppUpdateService.Instance.LaunchInstallerAndExit(_downloadedFilePath);
                return;
            }

            if (_isDownloading)
            {
                return;
            }

            // Start downloading
            await StartDownloadAsync();
        }

        private async Task StartDownloadAsync()
        {
            string downloadUrl = _updateInfo.InstallerUrl;
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                // Open releases page in browser if direct url not provided
                OpenReleasePage();
                return;
            }

            _isDownloading = true;
            _lblTitle.Text = "Đang Cập Nhật KahoiiMusic";
            _lblClose.Visible = false;
            _lblChangelogTitle.Visible = false;
            _pnlChangelog.Visible = false;
            _pnlProgressContainer.Visible = true;

            _btnPrimary.Visible = false;
            _btnSecondary.Text = "Hủy bỏ";
            _btnSecondary.Location = new Point((Width - 120) / 2, Height - 64);
            _btnSecondary.Size = new Size(120, 42);
            _btnSecondary.Visible = true;

            _cts = new CancellationTokenSource();
            var progress = new Progress<DownloadProgressReport>(report =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(() =>
                {
                    _currentProgressPercent = Math.Max(0, Math.Min(100, report.ProgressPercentage));
                    _pnlProgressBar.Invalidate();

                    string downloadedMB = (report.BytesReceived / (1024f * 1024f)).ToString("0.1");
                    string totalMB = report.TotalBytesToReceive > 0 ? (report.TotalBytesToReceive / (1024f * 1024f)).ToString("0.1") : "??";
                    string speedMB = (report.DownloadSpeedBytesPerSec / (1024f * 1024f)).ToString("0.1");

                    _lblProgressDetails.Text = $"{_currentProgressPercent}%  •  {downloadedMB} MB / {totalMB} MB  •  {speedMB} MB/s";
                }));
            });

            try
            {
                _lblProgressStatus.Text = "Đang tải bản cập nhật...";
                string downloadedPath = await AppUpdateService.Instance.DownloadInstallerAsync(downloadUrl, progress, _cts.Token);
                _downloadedFilePath = downloadedPath;

                // Download completed!
                _lblBadgeIcon.Text = "✓";
                _lblTitle.Text = "Tải Cập Nhật Thành Công!";
                _lblProgressStatus.Text = "Đang chuẩn bị khởi chạy bộ cài đặt...";
                _currentProgressPercent = 100;
                _pnlProgressBar.Invalidate();

                await Task.Delay(1000);

                // Auto launch installer and terminate app
                AppUpdateService.Instance.LaunchInstallerAndExit(_downloadedFilePath);
            }
            catch (OperationCanceledException)
            {
                // Cancelled by user
                ResetToPromptView();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CyberUpdateDialog] Download error: {ex.Message}");
                ShowDownloadError(ex.Message);
            }
            finally
            {
                _isDownloading = false;
            }
        }

        private void ResetToPromptView()
        {
            _isDownloading = false;
            _lblBadgeIcon.Text = "🚀";
            _lblTitle.Text = "Đã Có Bản Cập Nhật Mới!";
            _lblClose.Visible = !_updateInfo.ForceUpdate;
            _lblChangelogTitle.Visible = true;
            _pnlChangelog.Visible = true;
            _pnlProgressContainer.Visible = false;

            _btnPrimary.Text = "Cập nhật ngay 🚀";
            _btnPrimary.Location = new Point(Width - 34 - 160, Height - 64);
            _btnPrimary.Size = new Size(160, 42);
            _btnPrimary.Visible = true;

            _btnSecondary.Text = "Để sau";
            _btnSecondary.Location = new Point(Width - 34 - 160 - 12 - 116, Height - 64);
            _btnSecondary.Size = new Size(116, 42);
            _btnSecondary.Visible = !_updateInfo.ForceUpdate;
        }

        private void ShowDownloadError(string errorMsg)
        {
            _lblProgressStatus.Text = "Không thể tải tự động. Vui lòng thử lại hoặc tải qua trình duyệt.";
            _lblProgressStatus.ForeColor = Color.FromArgb(255, 100, 100);

            _btnPrimary.Text = "Tải qua trình duyệt ↗";
            _btnPrimary.Size = new Size(180, 42);
            _btnPrimary.Location = new Point(Width - 34 - 180, Height - 64);
            _btnPrimary.Visible = true;

            _btnSecondary.Text = "Đóng";
            _btnSecondary.Location = new Point(Width - 34 - 180 - 12 - 100, Height - 64);
            _btnSecondary.Size = new Size(100, 42);
            _btnSecondary.Visible = true;
        }

        private void OpenReleasePage()
        {
            try
            {
                string target = !string.IsNullOrWhiteSpace(_updateInfo.ReleasePageUrl)
                    ? _updateInfo.ReleasePageUrl
                    : (!string.IsNullOrWhiteSpace(_updateInfo.InstallerUrl) ? _updateInfo.InstallerUrl : AppUpdateService.DefaultReleasePageUrl);
                Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            }
            catch { }
            Close();
        }

        private void OnCancelOrDismiss()
        {
            if (_isDownloading)
            {
                try
                {
                    _cts?.Cancel();
                }
                catch { }
                return;
            }

            if (!_updateInfo.ForceUpdate)
            {
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Header circular glow behind rocket badge
            Rectangle badgeCircle = new Rectangle((Width - 64) / 2, 19, 64, 64);
            using (GraphicsPath pBadge = new GraphicsPath())
            {
                pBadge.AddEllipse(badgeCircle);
                using (LinearGradientBrush brushBadge = new LinearGradientBrush(
                    badgeCircle,
                    Color.FromArgb(60, 255, 45, 117),
                    Color.FromArgb(40, 124, 58, 237),
                    LinearGradientMode.Vertical))
                {
                    g.FillPath(brushBadge, pBadge);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(140, 255, 45, 117), 1.5f))
                {
                    g.DrawPath(borderPen, pBadge);
                }
            }

            // Outer Neon Border
            using (GraphicsPath p = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 18))
            {
                using (LinearGradientBrush borderBrush = new LinearGradientBrush(
                    ClientRectangle,
                    Color.FromArgb(255, 45, 117),
                    Color.FromArgb(124, 58, 237),
                    LinearGradientMode.ForwardDiagonal))
                {
                    using (Pen borderPen = new Pen(borderBrush, 1.8f))
                    {
                        g.DrawPath(borderPen, p);
                    }
                }
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (Owner != null && Owner.Visible)
            {
                int x = Owner.Location.X + (Owner.Width - Width) / 2;
                int y = Owner.Location.Y + (Owner.Height - Height) / 2;
                Screen scr = Screen.FromControl(Owner);
                Rectangle bounds = scr.WorkingArea;
                x = Math.Max(bounds.Left, Math.Min(x, bounds.Right - Width));
                y = Math.Max(bounds.Top, Math.Min(y, bounds.Bottom - Height));
                Location = new Point(x, y);
            }
        }

        private static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
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
