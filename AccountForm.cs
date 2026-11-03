using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic
{
    public class AccountForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        private Panel pnlHeader;
        private Label lblTitle;
        private Label btnClose;
        private Panel pnlContent;

        // Custom Tab Control
        private CustomTabPill tabSelector;
        private bool _isRegisterMode = false;

        // Input Panels
        private Panel pnlEmailBox;
        private TextBox txtEmail;
        private Panel pnlPasswordBox;
        private TextBox txtPassword;
        private Panel pnlConfirmPasswordBox;
        private TextBox txtConfirmPassword;

        private RoundedButton btnSubmit;
        private Label lblError;
        private Label lblHint;

        // Profile View (Logged in)
        private Panel pnlProfile;
        private Label lblUserAvatar;
        private Label lblUserEmail;
        private Label lblUserId;
        private Label lblStats;
        private Label lblSyncStatus;
        private RoundedButton btnUploadCloud;
        private RoundedButton btnFetchCloud;
        private RoundedButton btnSignOut;

        private readonly Color BgDark = Color.FromArgb(18, 16, 25);
        private readonly Color HeaderBg = Color.FromArgb(24, 21, 32);
        private readonly Color CardBg = Color.FromArgb(28, 25, 38);
        private readonly Color InputBg = Color.FromArgb(30, 26, 42);
        private readonly Color BorderMuted = Color.FromArgb(56, 48, 72);
        private readonly Color AccentPink = Color.FromArgb(255, 45, 120);

        public AccountForm()
        {
            InitializeComponent();
            RefreshViewState();

            FirebaseAuthService.Instance.AuthStateChanged += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    try { Invoke(new Action(RefreshViewState)); } catch { }
                }
            };

            FirebaseSyncService.Instance.SyncStatusChanged += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    try { Invoke(new Action(UpdateSyncStatus)); } catch { }
                }
            };
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(340, 320);
            this.BackColor = BgDark;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;

            // Form Border
            this.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                using (var pen = new Pen(BorderMuted, 1.5f))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };

            // 1. Header Bar
            pnlHeader = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(this.Width, 40),
                BackColor = HeaderBg
            };
            pnlHeader.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    this.Capture = false;
                    Message msg = Message.Create(this.Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
                    this.DefWndProc(ref msg);
                }
            };

            lblTitle = new Label
            {
                Text = "Tài Khoản Kahoii",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(14, 10),
                AutoSize = true
            };

            btnClose = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 160, 175),
                Location = new Point(306, 8),
                Size = new Size(24, 24),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnClose.Click += (s, e) => this.Close();
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(160, 160, 175);

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(btnClose);
            this.Controls.Add(pnlHeader);

            // 2. Content Area
            pnlContent = new Panel
            {
                Location = new Point(0, 40),
                Size = new Size(this.Width, this.Height - 40),
                Padding = new Padding(0)
            };
            this.Controls.Add(pnlContent);

            SetupAuthUI();
            SetupProfileUI();
        }

        private void SetupAuthUI()
        {
            // Segmented Tab Pill
            tabSelector = new CustomTabPill
            {
                Location = new Point(20, 12),
                Size = new Size(300, 34),
                ActiveColor = AccentPink,
                BackColor = CardBg
            };
            tabSelector.TabChanged += (s, isRegister) => SwitchTab(isRegister);
            pnlContent.Controls.Add(tabSelector);

            // Email Box
            pnlEmailBox = CreateInputBox(20, 58, "✉", "Email của bạn...", false, out txtEmail);
            pnlContent.Controls.Add(pnlEmailBox);

            // Password Box
            pnlPasswordBox = CreateInputBox(20, 106, "🔒", "Mật khẩu (tối thiểu 6 ký tự)...", true, out txtPassword);
            pnlContent.Controls.Add(pnlPasswordBox);

            // Confirm Password Box (for Register)
            pnlConfirmPasswordBox = CreateInputBox(20, 154, "🔒", "Nhập lại mật khẩu...", true, out txtConfirmPassword);
            pnlConfirmPasswordBox.Visible = false;
            pnlContent.Controls.Add(pnlConfirmPasswordBox);

            // Error Label
            lblError = new Label
            {
                Location = new Point(20, 148),
                Size = new Size(300, 20),
                ForeColor = Color.FromArgb(255, 100, 125),
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };
            pnlContent.Controls.Add(lblError);

            // Submit Button
            btnSubmit = new RoundedButton
            {
                Text = "ĐĂNG NHẬP",
                Location = new Point(20, 172),
                Size = new Size(300, 38),
                ButtonColor = AccentPink,
                TextColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSubmit.Click += async (s, e) =>
            {
                lblError.Visible = false;
                lblError.Text = "";
                btnSubmit.Enabled = false;

                string email = txtEmail.Text.Trim();
                string pass = txtPassword.Text;

                if (_isRegisterMode)
                {
                    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
                    {
                        ShowError("Vui lòng nhập đầy đủ thông tin.");
                        btnSubmit.Enabled = true;
                        return;
                    }
                    if (pass.Length < 6)
                    {
                        ShowError("Mật khẩu phải từ 6 ký tự trở lên.");
                        btnSubmit.Enabled = true;
                        return;
                    }
                    if (pass != txtConfirmPassword.Text)
                    {
                        ShowError("Mật khẩu xác nhận không khớp.");
                        btnSubmit.Enabled = true;
                        return;
                    }

                    var res = await FirebaseAuthService.Instance.SignUpAsync(email, pass);
                    if (!res.Item1)
                    {
                        ShowError(res.Item2);
                    }
                    else
                    {
                        var payload = await FirebaseSyncService.Instance.SyncFetchAsync();
                        FirebaseSyncService.Instance.ApplyCloudPayload(payload);
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
                    {
                        ShowError("Vui lòng nhập email và mật khẩu.");
                        btnSubmit.Enabled = true;
                        return;
                    }

                    var res = await FirebaseAuthService.Instance.SignInAsync(email, pass);
                    if (!res.Item1)
                    {
                        ShowError(res.Item2);
                    }
                    else
                    {
                        var payload = await FirebaseSyncService.Instance.SyncFetchAsync();
                        FirebaseSyncService.Instance.ApplyCloudPayload(payload);
                    }
                }
                btnSubmit.Enabled = true;
            };
            pnlContent.Controls.Add(btnSubmit);

            // Hint Info
            lblHint = new Label
            {
                Text = "• Tự động đồng bộ bài hát, yêu thích và đã tải với Firebase",
                Location = new Point(14, 222),
                Size = new Size(312, 34),
                ForeColor = Color.FromArgb(135, 130, 155),
                Font = new Font("Segoe UI", 8F),
                TextAlign = ContentAlignment.TopCenter
            };
            pnlContent.Controls.Add(lblHint);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (pnlHeader != null)
            {
                pnlHeader.Width = this.Width;
                if (btnClose != null) btnClose.Location = new Point(this.Width - 34, 8);
            }
            if (pnlContent != null)
            {
                pnlContent.Width = this.Width;
                pnlContent.Height = Math.Max(0, this.Height - 40);
            }
        }

        private void ShowError(string msg)
        {
            lblError.Text = msg;
            lblError.Visible = true;
        }

        private Panel CreateInputBox(int x, int y, string iconText, string placeholder, bool isPassword, out TextBox txtOut)
        {
            var pnl = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(300, 36),
                BackColor = InputBg
            };

            bool isFocused = false;
            pnl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                using (var pen = new Pen(isFocused ? AccentPink : BorderMuted, isFocused ? 1.5f : 1f))
                using (var path = GetRoundedRect(new Rectangle(0, 0, pnl.Width - 1, pnl.Height - 1), 6))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };

            var lblIcon = new Label
            {
                Text = iconText,
                Font = new Font("Segoe UI Symbol", 10F),
                ForeColor = Color.FromArgb(150, 145, 170),
                Location = new Point(8, 7),
                Size = new Size(20, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnl.Controls.Add(lblIcon);

            var txt = new TextBox
            {
                Location = new Point(32, 9),
                Width = 258,
                BackColor = InputBg,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5F)
            };
            if (isPassword) txt.PasswordChar = '•';

            txt.HandleCreated += (s, e) =>
            {
                SendMessage(txt.Handle, EM_SETCUEBANNER, 0, placeholder);
            };

            txt.Enter += (s, e) => { isFocused = true; pnl.Invalidate(); };
            txt.Leave += (s, e) => { isFocused = false; pnl.Invalidate(); };

            pnl.Controls.Add(txt);
            txtOut = txt;
            return pnl;
        }

        private void SwitchTab(bool register)
        {
            _isRegisterMode = register;
            lblError.Visible = false;
            lblError.Text = "";

            if (register)
            {
                pnlConfirmPasswordBox.Visible = true;
                lblError.Location = new Point(20, 196);
                btnSubmit.Location = new Point(20, 220);
                btnSubmit.Text = "TẠO TÀI KHOẢN";
                lblHint.Location = new Point(14, 270);
                this.Height = 360;
                pnlContent.Height = this.Height - 40;
            }
            else
            {
                pnlConfirmPasswordBox.Visible = false;
                lblError.Location = new Point(20, 148);
                btnSubmit.Location = new Point(20, 172);
                btnSubmit.Text = "ĐĂNG NHẬP";
                lblHint.Location = new Point(14, 222);
                this.Height = 310;
                pnlContent.Height = this.Height - 40;
            }
        }

        private void SetupProfileUI()
        {
            pnlProfile = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            lblUserAvatar = new Label
            {
                Text = "👤",
                Font = new Font("Segoe UI", 30F),
                ForeColor = AccentPink,
                Location = new Point(0, 10),
                Size = new Size(340, 50),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblUserEmail = new Label
            {
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 64),
                Size = new Size(340, 24),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblUserId = new Label
            {
                Font = new Font("Consolas", 8F),
                ForeColor = Color.FromArgb(135, 130, 155),
                Location = new Point(0, 88),
                Size = new Size(340, 18),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblStats = new Label
            {
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(200, 195, 220),
                Location = new Point(20, 114),
                Size = new Size(300, 44),
                BackColor = CardBg,
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblStats.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                using (var pen = new Pen(BorderMuted, 1))
                using (var path = GetRoundedRect(new Rectangle(0, 0, lblStats.Width - 1, lblStats.Height - 1), 6))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };

            lblSyncStatus = new Label
            {
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = AccentPink,
                Location = new Point(20, 164),
                Size = new Size(300, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            btnUploadCloud = new RoundedButton
            {
                Text = "☁  Lưu Dữ Liệu Lên Cloud",
                Location = new Point(20, 190),
                Size = new Size(300, 36),
                ButtonColor = AccentPink,
                TextColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnUploadCloud.Click += async (s, e) =>
            {
                btnUploadCloud.Enabled = false;
                var payload = new CloudMusicPayload
                {
                    savedTracks = SavedTracksService.Instance.GetSavedTracks(),
                    history = HistoryService.GetHistory(),
                    collections = PlaylistCollectionService.Instance.GetPlaylists()
                };
                await FirebaseSyncService.Instance.SyncUploadAsync(payload);
                btnUploadCloud.Enabled = true;
            };

            btnFetchCloud = new RoundedButton
            {
                Text = "⟳  Tải Dữ Liệu Từ Cloud",
                Location = new Point(20, 234),
                Size = new Size(300, 36),
                ButtonColor = CardBg,
                TextColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnFetchCloud.Click += async (s, e) =>
            {
                btnFetchCloud.Enabled = false;
                var payload = await FirebaseSyncService.Instance.SyncFetchAsync();
                if (payload != null)
                {
                    FirebaseSyncService.Instance.ApplyCloudPayload(payload, missingCount =>
                    {
                        MessageBox.Show("Đang tự động tải lại " + missingCount + " bài hát đã lưu từ tài khoản của bạn.",
                            "Tự động tải lại bài đã lưu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });
                }
                btnFetchCloud.Enabled = true;
                UpdateStats();
            };

            btnSignOut = new RoundedButton
            {
                Text = "Đăng Xuất",
                Location = new Point(20, 282),
                Size = new Size(300, 32),
                ButtonColor = Color.FromArgb(38, 20, 28),
                TextColor = Color.FromArgb(255, 120, 130),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSignOut.Click += (s, e) =>
            {
                FirebaseAuthService.Instance.SignOut();
            };

            pnlProfile.Controls.Add(lblUserAvatar);
            pnlProfile.Controls.Add(lblUserEmail);
            pnlProfile.Controls.Add(lblUserId);
            pnlProfile.Controls.Add(lblStats);
            pnlProfile.Controls.Add(lblSyncStatus);
            pnlProfile.Controls.Add(btnUploadCloud);
            pnlProfile.Controls.Add(btnFetchCloud);
            pnlProfile.Controls.Add(btnSignOut);

            pnlContent.Controls.Add(pnlProfile);
        }

        private void RefreshViewState()
        {
            if (FirebaseAuthService.Instance.IsLoggedIn)
            {
                tabSelector.Visible = false;
                pnlEmailBox.Visible = false;
                pnlPasswordBox.Visible = false;
                pnlConfirmPasswordBox.Visible = false;
                btnSubmit.Visible = false;
                lblError.Visible = false;
                lblHint.Visible = false;

                pnlProfile.Visible = true;
                this.Height = 380;
                pnlContent.Height = this.Height - 40;

                var user = FirebaseAuthService.Instance.CurrentUser;
                lblUserEmail.Text = user?.email ?? "User";
                lblUserId.Text = "UID: " + (user?.localId ?? "");
                UpdateStats();
                UpdateSyncStatus();
            }
            else
            {
                pnlProfile.Visible = false;
                tabSelector.Visible = true;
                pnlEmailBox.Visible = true;
                pnlPasswordBox.Visible = true;
                btnSubmit.Visible = true;
                lblHint.Visible = true;
                SwitchTab(_isRegisterMode);
            }
        }

        private void UpdateStats()
        {
            int savedCount = SavedTracksService.Instance.GetSavedTracks().Count;
            int historyCount = HistoryService.GetHistory().Count;
            int collectionCount = PlaylistCollectionService.Instance.GetPlaylists().Count;

            lblStats.Text = string.Format("Đã tải: {0} bài  •  Bộ sưu tập: {1}\r\nLịch sử nghe: {2} bài",
                savedCount, collectionCount, historyCount);
        }

        private void UpdateSyncStatus()
        {
            lblSyncStatus.Text = FirebaseSyncService.Instance.SyncStatusMessage;
        }

        private static GraphicsPath GetRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ================= HELPER CONTROLS =================

        // 1. Custom Rounded Button
        private class RoundedButton : Control
        {
            public Color ButtonColor { get; set; } = Color.FromArgb(255, 45, 120);
            public Color TextColor { get; set; } = Color.White;
            private bool _isHovered = false;

            public RoundedButton()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.HighQuality;

                Color c = _isHovered ? ControlPaint.Light(ButtonColor, 0.15f) : ButtonColor;
                using (var brush = new SolidBrush(c))
                using (var path = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                {
                    g.FillPath(brush, path);
                }

                TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), TextColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        // 2. Custom Pill Segmented Tab Control
        private class CustomTabPill : Control
        {
            public Color ActiveColor { get; set; } = Color.FromArgb(255, 45, 120);
            private bool _isRegister = false;
            public event EventHandler<bool> TabChanged;

            public CustomTabPill()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                bool newRegister = e.X >= Width / 2;
                if (newRegister != _isRegister)
                {
                    _isRegister = newRegister;
                    Invalidate();
                    TabChanged?.Invoke(this, _isRegister);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.HighQuality;

                // Base container
                using (var bgBrush = new SolidBrush(BackColor))
                using (var path = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                {
                    g.FillPath(bgBrush, path);
                }

                // Active tab pill
                int tabW = (Width - 6) / 2;
                int tabH = Height - 6;
                int activeX = _isRegister ? Width / 2 : 3;
                int activeY = 3;

                using (var activeBrush = new SolidBrush(ActiveColor))
                using (var activePath = GetRoundedRect(new Rectangle(activeX, activeY, tabW, tabH), 6))
                {
                    g.FillPath(activeBrush, activePath);
                }

                // Tab 1 text: "Đăng Nhập"
                Color c1 = _isRegister ? Color.FromArgb(180, 175, 195) : Color.White;
                using (var f1 = new Font("Segoe UI", 9.5F, _isRegister ? FontStyle.Regular : FontStyle.Bold))
                {
                    TextRenderer.DrawText(g, "Đăng Nhập", f1, new Rectangle(3, 3, tabW, tabH), c1,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                // Tab 2 text: "Đăng Ký"
                Color c2 = _isRegister ? Color.White : Color.FromArgb(180, 175, 195);
                using (var f2 = new Font("Segoe UI", 9.5F, _isRegister ? FontStyle.Bold : FontStyle.Regular))
                {
                    TextRenderer.DrawText(g, "Đăng Ký", f2, new Rectangle(Width / 2, 3, tabW, tabH), c2,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }
}
