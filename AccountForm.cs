using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic
{
    public class AccountForm : Form
    {
        private Panel pnlHeader;
        private Label lblTitle;
        private Label btnClose;
        private Panel pnlContent;

        // Logged Out Controls
        private Panel pnlAuth;
        private Button btnTabLogin;
        private Button btnTabRegister;
        private TextBox txtEmail;
        private TextBox txtPassword;
        private TextBox txtConfirmPassword;
        private Label lblConfirmPassword;
        private Button btnSubmit;
        private Label lblAuthError;
        private bool _isRegisterMode = false;

        // Logged In Controls
        private Panel pnlProfile;
        private Label lblUserAvatar;
        private Label lblUserEmail;
        private Label lblUserId;
        private Label lblSyncStatus;
        private Button btnUploadCloud;
        private Button btnFetchCloud;
        private Button btnSignOut;
        private Label lblStats;

        private readonly Color BgDark = Color.FromArgb(18, 16, 24);
        private readonly Color AccentPink = Color.FromArgb(255, 45, 120);
        private readonly Color CardBg = Color.FromArgb(28, 25, 38);

        public AccountForm()
        {
            InitializeComponent();
            RefreshViewState();
            FirebaseAuthService.Instance.AuthStateChanged += (s, e) =>
            {
                if (InvokeRequired) Invoke(new Action(RefreshViewState));
                else RefreshViewState();
            };
            FirebaseSyncService.Instance.SyncStatusChanged += (s, e) =>
            {
                if (InvokeRequired) Invoke(new Action(UpdateSyncStatus));
                else UpdateSyncStatus();
            };
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(380, 480);
            this.BackColor = BgDark;
            this.ShowInTaskbar = false;

            // Custom border
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(60, 50, 75), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };

            // Header
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.FromArgb(24, 21, 32)
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
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(16, 10),
                AutoSize = true
            };

            btnClose = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 160, 160),
                Location = new Point(345, 10),
                Size = new Size(24, 22),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnClose.Click += (s, e) => this.Close();
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(160, 160, 160);

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(btnClose);
            this.Controls.Add(pnlHeader);

            // Content Host
            pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };
            this.Controls.Add(pnlContent);

            SetupAuthPanel();
            SetupProfilePanel();
        }

        private void SetupAuthPanel()
        {
            pnlAuth = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            btnTabLogin = new Button
            {
                Text = "Đăng Nhập",
                Location = new Point(20, 10),
                Size = new Size(160, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentPink,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTabLogin.FlatAppearance.BorderSize = 0;
            btnTabLogin.Click += (s, e) => SwitchTab(false);

            btnTabRegister = new Button
            {
                Text = "Đăng Ký",
                Location = new Point(190, 10),
                Size = new Size(160, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = CardBg,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnTabRegister.FlatAppearance.BorderSize = 0;
            btnTabRegister.Click += (s, e) => SwitchTab(true);

            Label lblEmail = new Label
            {
                Text = "Email:",
                Location = new Point(20, 60),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F)
            };

            txtEmail = new TextBox
            {
                Location = new Point(20, 82),
                Width = 330,
                BackColor = CardBg,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5F)
            };

            Label lblPass = new Label
            {
                Text = "Mật khẩu:",
                Location = new Point(20, 125),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F)
            };

            txtPassword = new TextBox
            {
                Location = new Point(20, 147),
                Width = 330,
                BackColor = CardBg,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PasswordChar = '•',
                Font = new Font("Segoe UI", 10.5F)
            };

            lblConfirmPassword = new Label
            {
                Text = "Nhập lại mật khẩu:",
                Location = new Point(20, 190),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F),
                Visible = false
            };

            txtConfirmPassword = new TextBox
            {
                Location = new Point(20, 212),
                Width = 330,
                BackColor = CardBg,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PasswordChar = '•',
                Font = new Font("Segoe UI", 10.5F),
                Visible = false
            };

            lblAuthError = new Label
            {
                Location = new Point(20, 260),
                Size = new Size(330, 36),
                ForeColor = Color.FromArgb(255, 90, 90),
                Font = new Font("Segoe UI", 8.5F)
            };

            btnSubmit = new Button
            {
                Text = "ĐĂNG NHẬP",
                Location = new Point(20, 310),
                Size = new Size(330, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentPink,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSubmit.FlatAppearance.BorderSize = 0;
            btnSubmit.Click += async (s, e) =>
            {
                lblAuthError.Text = "";
                btnSubmit.Enabled = false;
                string email = txtEmail.Text.Trim();
                string pass = txtPassword.Text;

                if (_isRegisterMode)
                {
                    if (pass != txtConfirmPassword.Text)
                    {
                        lblAuthError.Text = "Mật khẩu xác nhận không khớp.";
                        btnSubmit.Enabled = true;
                        return;
                    }
                    var res = await FirebaseAuthService.Instance.SignUpAsync(email, pass);
                    if (!res.Item1) lblAuthError.Text = res.Item2;
                    else
                    {
                        // Auto fetch cloud
                        var payload = await FirebaseSyncService.Instance.SyncFetchAsync();
                        FirebaseSyncService.Instance.ApplyCloudPayload(payload);
                    }
                }
                else
                {
                    var res = await FirebaseAuthService.Instance.SignInAsync(email, pass);
                    if (!res.Item1) lblAuthError.Text = res.Item2;
                    else
                    {
                        // Auto fetch cloud
                        var payload = await FirebaseSyncService.Instance.SyncFetchAsync();
                        FirebaseSyncService.Instance.ApplyCloudPayload(payload);
                    }
                }
                btnSubmit.Enabled = true;
            };

            pnlAuth.Controls.Add(btnTabLogin);
            pnlAuth.Controls.Add(btnTabRegister);
            pnlAuth.Controls.Add(lblEmail);
            pnlAuth.Controls.Add(txtEmail);
            pnlAuth.Controls.Add(lblPass);
            pnlAuth.Controls.Add(txtPassword);
            pnlAuth.Controls.Add(lblConfirmPassword);
            pnlAuth.Controls.Add(txtConfirmPassword);
            pnlAuth.Controls.Add(lblAuthError);
            pnlAuth.Controls.Add(btnSubmit);

            pnlContent.Controls.Add(pnlAuth);
        }

        private void SwitchTab(bool register)
        {
            _isRegisterMode = register;
            lblAuthError.Text = "";
            if (register)
            {
                btnTabRegister.BackColor = AccentPink;
                btnTabRegister.ForeColor = Color.White;
                btnTabRegister.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);

                btnTabLogin.BackColor = CardBg;
                btnTabLogin.ForeColor = Color.LightGray;
                btnTabLogin.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

                lblConfirmPassword.Visible = true;
                txtConfirmPassword.Visible = true;
                btnSubmit.Text = "ĐĂNG KÝ TÀI KHOẢN";
            }
            else
            {
                btnTabLogin.BackColor = AccentPink;
                btnTabLogin.ForeColor = Color.White;
                btnTabLogin.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);

                btnTabRegister.BackColor = CardBg;
                btnTabRegister.ForeColor = Color.LightGray;
                btnTabRegister.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

                lblConfirmPassword.Visible = false;
                txtConfirmPassword.Visible = false;
                btnSubmit.Text = "ĐĂNG NHẬP";
            }
        }

        private void SetupProfilePanel()
        {
            pnlProfile = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            lblUserAvatar = new Label
            {
                Text = "👤",
                Font = new Font("Segoe UI", 36F),
                ForeColor = AccentPink,
                Location = new Point(0, 10),
                Size = new Size(370, 65),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblUserEmail = new Label
            {
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 80),
                Size = new Size(370, 26),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblUserId = new Label
            {
                Font = new Font("Consolas", 8.5F),
                ForeColor = Color.FromArgb(140, 140, 160),
                Location = new Point(0, 108),
                Size = new Size(370, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblStats = new Label
            {
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(200, 200, 210),
                Location = new Point(20, 140),
                Size = new Size(330, 48),
                BackColor = CardBg,
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblSyncStatus = new Label
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = AccentPink,
                Location = new Point(20, 198),
                Size = new Size(330, 24),
                TextAlign = ContentAlignment.MiddleCenter
            };

            btnUploadCloud = new Button
            {
                Text = "☁ Lưu Dữ Liệu Lên Cloud",
                Location = new Point(20, 235),
                Size = new Size(330, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentPink,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnUploadCloud.FlatAppearance.BorderSize = 0;
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

            btnFetchCloud = new Button
            {
                Text = "⟳ Tải Dữ Liệu Từ Cloud",
                Location = new Point(20, 282),
                Size = new Size(330, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = CardBg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnFetchCloud.FlatAppearance.BorderSize = 0;
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

            btnSignOut = new Button
            {
                Text = "Đăng Xuất",
                Location = new Point(20, 345),
                Size = new Size(330, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 20, 30),
                ForeColor = Color.FromArgb(255, 120, 120),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSignOut.FlatAppearance.BorderSize = 0;
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
                pnlAuth.Visible = false;
                pnlProfile.Visible = true;
                var user = FirebaseAuthService.Instance.CurrentUser;
                lblUserEmail.Text = user?.email ?? "User";
                lblUserId.Text = "UID: " + (user?.localId ?? "");
                UpdateStats();
                UpdateSyncStatus();
            }
            else
            {
                pnlProfile.Visible = false;
                pnlAuth.Visible = true;
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
    }
}
