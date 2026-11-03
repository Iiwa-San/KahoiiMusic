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
        private GradientCloseButton btnClose;
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
        private CustomCheckBox chkRememberMe;
        private Label btnForgotPassword;
        
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
            this.Size = new Size(340, 270);
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

            btnClose = new GradientCloseButton
            {
                Location = new Point(302, 7),
                Size = new Size(26, 26)
            };
            btnClose.Click += (s, e) => this.Close();

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

            // Remember Login Checkbox
            chkRememberMe = new CustomCheckBox
            {
                Location = new Point(20, 148),
                Size = new Size(160, 22),
                Checked = true
            };
            pnlContent.Controls.Add(chkRememberMe);

            // Forgot Password Button
            btnForgotPassword = new Label
            {
                Text = "Quên mật khẩu?",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Underline),
                ForeColor = Color.FromArgb(170, 165, 195),
                Location = new Point(190, 149),
                Size = new Size(130, 20),
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.Hand
            };
            btnForgotPassword.MouseEnter += (s, e) => btnForgotPassword.ForeColor = AccentPink;
            btnForgotPassword.MouseLeave += (s, e) => btnForgotPassword.ForeColor = Color.FromArgb(170, 165, 195);
            btnForgotPassword.Click += async (s, e) =>
            {
                string email = txtEmail.Text.Trim();
                if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                {
                    ShowError("Vui lòng nhập email hợp lệ để nhận link khôi phục.");
                    txtEmail.Focus();
                    return;
                }

                btnForgotPassword.Enabled = false;
                lblError.Visible = false;
                var res = await FirebaseAuthService.Instance.SendPasswordResetEmailAsync(email);
                btnForgotPassword.Enabled = true;

                if (res.Item1)
                {
                    MessageBox.Show(res.Item2, "Khôi phục mật khẩu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ShowError(res.Item2);
                }
            };
            pnlContent.Controls.Add(btnForgotPassword);

            // Error Label
            lblError = new Label
            {
                Location = new Point(20, 174),
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
                Location = new Point(20, 196),
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

                    var res = await FirebaseAuthService.Instance.SignUpAsync(email, pass, chkRememberMe.Checked);
                    if (!res.Item1)
                    {
                        ShowError(res.Item2);
                    }
                    else
                    {
                        string currentPlayingId = Form1.Instance?.AudioPlayer?.CurrentVideoId;
                        Form1.Instance?.AudioPlayer?.RetainOnlyCurrentTrack();
                        FirebaseAuthService.Instance.ClearUserDataExceptPlayingTrack(currentPlayingId);

                        var payload = await FirebaseSyncService.Instance.SyncFetchAsync();
                        FirebaseSyncService.Instance.ApplyCloudPayload(payload, missingCount =>
                        {
                            MessageBox.Show("Tài khoản của bạn có " + missingCount + " bài hát đã lưu. Ứng dụng đang tự động tải nhạc về máy...",
                                "Tự động tải bài hát", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        });
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

                    var res = await FirebaseAuthService.Instance.SignInAsync(email, pass, chkRememberMe.Checked);
                    if (!res.Item1)
                    {
                        ShowError(res.Item2);
                    }
                    else
                    {
                        string currentPlayingId = Form1.Instance?.AudioPlayer?.CurrentVideoId;
                        Form1.Instance?.AudioPlayer?.RetainOnlyCurrentTrack();
                        FirebaseAuthService.Instance.ClearUserDataExceptPlayingTrack(currentPlayingId);

                        var payload = await FirebaseSyncService.Instance.SyncFetchAsync();
                        FirebaseSyncService.Instance.ApplyCloudPayload(payload, missingCount =>
                        {
                            MessageBox.Show("Tài khoản của bạn có " + missingCount + " bài hát đã lưu. Ứng dụng đang tự động tải nhạc về máy...",
                                "Tự động tải bài hát", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        });
                    }
                }
                btnSubmit.Enabled = true;
            };
            pnlContent.Controls.Add(btnSubmit);


        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (pnlHeader != null)
            {
                pnlHeader.Width = this.Width;
                if (btnClose != null) btnClose.Location = new Point(this.Width - 34, 7);
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
                if (btnForgotPassword != null) btnForgotPassword.Visible = false;
                chkRememberMe.Location = new Point(20, 194);
                lblError.Location = new Point(20, 220);
                btnSubmit.Location = new Point(20, 242);
                btnSubmit.Text = "TẠO TÀI KHOẢN";
                this.Height = 344;
                pnlContent.Height = this.Height - 40;
            }
            else
            {
                pnlConfirmPasswordBox.Visible = false;
                if (btnForgotPassword != null) btnForgotPassword.Visible = true;
                chkRememberMe.Location = new Point(20, 148);
                if (btnForgotPassword != null) btnForgotPassword.Location = new Point(190, 149);
                lblError.Location = new Point(20, 174);
                btnSubmit.Location = new Point(20, 196);
                btnSubmit.Text = "ĐĂNG NHẬP";
                this.Height = 298;
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
                var ask = MessageBox.Show("Bạn có muốn đăng xuất tài khoản không?\n\n(Danh sách bài hát của bạn đã được lưu an toàn trên đám mây và sẽ tự động tải về khi bạn đăng nhập lại).",
                    "Đăng xuất", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ask == DialogResult.Yes)
                {
                    string currentPlayingId = Form1.Instance?.AudioPlayer?.CurrentVideoId;
                    Form1.Instance?.AudioPlayer?.RetainOnlyCurrentTrack();
                    FirebaseAuthService.Instance.SignOut(currentPlayingId);
                }
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
                chkRememberMe.Visible = false;
                if (btnForgotPassword != null) btnForgotPassword.Visible = false;
                btnSubmit.Visible = false;
                lblError.Visible = false;
                
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
                chkRememberMe.Visible = true;
                btnSubmit.Visible = true;
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

        // 3. Gradient Close Button with Smooth Pink-Purple Color Transition on Hover
        private class GradientCloseButton : Control
        {
            private float _hoverProgress = 0f;
            private float _targetProgress = 0f;
            private bool _isPressed = false;
            private Timer _animTimer;

            public GradientCloseButton()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Size = new Size(26, 26);
                Cursor = Cursors.Hand;

                _animTimer = new Timer();
                _animTimer.Interval = 15;
                _animTimer.Tick += (s, e) =>
                {
                    float diff = _targetProgress - _hoverProgress;
                    if (Math.Abs(diff) < 0.05f)
                    {
                        _hoverProgress = _targetProgress;
                        _animTimer.Stop();
                    }
                    else
                    {
                        _hoverProgress += diff * 0.35f;
                    }
                    Invalidate();
                };
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _animTimer != null)
                {
                    _animTimer.Stop();
                    _animTimer.Dispose();
                    _animTimer = null;
                }
                base.Dispose(disposing);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _targetProgress = 1.0f;
                _animTimer.Start();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _targetProgress = 0.0f;
                _isPressed = false;
                _animTimer.Start();
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
                _isPressed = false;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int cx = Width / 2;
                int cy = Height / 2;
                int r = _isPressed ? 4 : 5;

                // Subtle soft ambient glow background on hover (NOT an opaque solid circle)
                if (_hoverProgress > 0.02f)
                {
                    int bgAlpha = (int)(25 * _hoverProgress);
                    using (var bgBrush = new SolidBrush(Color.FromArgb(bgAlpha, 255, 45, 140)))
                    using (var path = GetRoundedRect(new Rectangle(2, 2, Width - 5, Height - 5), 6))
                    {
                        g.FillPath(bgBrush, path);
                    }

                    // Soft neon bloom behind the 'X'
                    int glowAlpha = (int)(65 * _hoverProgress);
                    using (var glowPen = new Pen(Color.FromArgb(glowAlpha, 255, 50, 150), 3.8f))
                    {
                        glowPen.StartCap = LineCap.Round;
                        glowPen.EndCap = LineCap.Round;
                        g.DrawLine(glowPen, cx - r, cy - r, cx + r, cy + r);
                        g.DrawLine(glowPen, cx + r, cy - r, cx - r, cy + r);
                    }
                }

                if (_hoverProgress < 0.02f)
                {
                    // Clean Muted '✕' in default state
                    using (var pen = new Pen(Color.FromArgb(160, 160, 175), 1.6f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                        g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
                    }
                }
                else
                {
                    // Gradient '✕' with Smooth Color Shift (Purple #7819BE to Neon Pink #FF2680)
                    var gradRect = new Rectangle(cx - r - 1, cy - r - 1, (r * 2) + 2, (r * 2) + 2);
                    Color purple = _isPressed ? Color.FromArgb(160, 40, 240) : Color.FromArgb(180, 50, 255);
                    Color pink = _isPressed ? Color.FromArgb(255, 90, 170) : Color.FromArgb(255, 38, 128);

                    // Interpolate starting colors with muted grey based on progress
                    Color startC = BlendColor(Color.FromArgb(160, 160, 175), purple, _hoverProgress);
                    Color endC = BlendColor(Color.FromArgb(160, 160, 175), pink, _hoverProgress);

                    using (var gradBrush = new LinearGradientBrush(gradRect, startC, endC, 45f))
                    using (var pen = new Pen(gradBrush, 2.0f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                        g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
                    }
                }
            }

            private Color BlendColor(Color from, Color to, float amount)
            {
                amount = Math.Max(0f, Math.Min(1f, amount));
                int r = (int)(from.R + (to.R - from.R) * amount);
                int g = (int)(from.G + (to.G - from.G) * amount);
                int b = (int)(from.B + (to.B - from.B) * amount);
                return Color.FromArgb(r, g, b);
            }
        }

        // 4. Custom Modern CheckBox Control for Remember Login
        private class CustomCheckBox : Control
        {
            private bool _checked = true;
            public bool Checked
            {
                get => _checked;
                set { _checked = value; Invalidate(); }
            }

            public string LabelText { get; set; } = "Ghi nhớ đăng nhập";

            public CustomCheckBox()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Size = new Size(180, 22);
                Cursor = Cursors.Hand;
            }

            protected override void OnClick(EventArgs e)
            {
                base.OnClick(e);
                Checked = !Checked;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int boxSize = 16;
                int boxY = (Height - boxSize) / 2;
                var boxRect = new Rectangle(0, boxY, boxSize, boxSize);

                if (_checked)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(255, 38, 128))) // Neon Pink
                    using (var path = GetRoundedRect(boxRect, 4))
                    {
                        g.FillPath(brush, path);
                    }

                    // Checkmark '✓'
                    using (var pen = new Pen(Color.White, 1.8f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, 3, boxY + 8, 7, boxY + 12);
                        g.DrawLine(pen, 7, boxY + 12, 13, boxY + 4);
                    }
                }
                else
                {
                    using (var brush = new SolidBrush(Color.FromArgb(28, 25, 42)))
                    using (var pen = new Pen(Color.FromArgb(90, 85, 115), 1.2f))
                    using (var path = GetRoundedRect(boxRect, 4))
                    {
                        g.FillPath(brush, path);
                        g.DrawPath(pen, path);
                    }
                }

                // Text
                var textRect = new Rectangle(boxSize + 8, 0, Width - boxSize - 8, Height);
                using (var font = new Font("Segoe UI", 9F, FontStyle.Regular))
                {
                    TextRenderer.DrawText(g, LabelText, font, textRect, Color.FromArgb(195, 190, 215),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
            }

            private GraphicsPath GetRoundedRect(Rectangle r, int radius)
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
        }
    }
}