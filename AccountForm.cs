using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using KahoiiMusic.Controls;
using KahoiiMusic.Models;
using KahoiiMusic.Services;

namespace KahoiiMusic
{
    public class AccountForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        private Label lblTitle;
        private GradientCloseButton btnClose;

        // Custom Tab Control
        private CustomTabPill tabSelector;
        private bool _isRegisterMode = false;

        private DateTime _lastDeactivatedTime = DateTime.MinValue;
        public bool JustDeactivated => (DateTime.UtcNow - _lastDeactivatedTime).TotalMilliseconds < 250;
        public bool IsShowingChildDialog => _isShowingChildDialog;
        private bool _isShowingChildDialog = false;

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
        private UserAvatarBox picUserAvatar;
        private Label lblUserEmail;
        private Label lblUserId;
        private Label lblStats;
        private Label lblSyncStatus;
        private RoundedButton btnSyncNow;
        private RoundedButton btnSignOut;
        private Label lblVersion;
        private Label lblCopyright;

        private Color _accentColor = Color.FromArgb(255, 45, 120);
        private Color _secondaryAccentColor = Color.FromArgb(168, 85, 247);
        private Color _bgGradTop = Color.FromArgb(26, 20, 36);
        private Color _bgGradBottom = Color.FromArgb(15, 13, 20);
        private Color _cardBg = Color.FromArgb(28, 25, 40);
        private Color _borderGradColor1 = Color.FromArgb(160, 255, 45, 120);
        private Color _borderGradColor2 = Color.FromArgb(120, 168, 85, 247);

        private readonly Color BorderMuted = Color.FromArgb(56, 48, 72);

        public void SetTheme(DynamicThemePalette theme)
        {
            if (theme == null) return;
            try
            {
                _accentColor = theme.AccentColor;
                _secondaryAccentColor = theme.SecondaryAccentColor;

                _bgGradTop = BlendColor(Color.FromArgb(22, 18, 30), theme.CompactTop, 0.25f);
                _bgGradBottom = BlendColor(Color.FromArgb(13, 11, 18), theme.CompactBottom, 0.15f);
                _cardBg = BlendColor(Color.FromArgb(28, 24, 38), theme.SecondaryAccentColor, 0.18f);

                _borderGradColor1 = Color.FromArgb(160, _accentColor);
                _borderGradColor2 = Color.FromArgb(120, _secondaryAccentColor);

                this.BackColor = _bgGradBottom;

                if (tabSelector != null)
                {
                    tabSelector.ActiveColor = _accentColor;
                    tabSelector.BackColor = _cardBg;
                    tabSelector.Invalidate();
                }

                if (btnSubmit != null)
                {
                    btnSubmit.ButtonColor = _accentColor;
                    btnSubmit.Invalidate();
                }

                if (btnSyncNow != null)
                {
                    btnSyncNow.ButtonColor = _accentColor;
                    btnSyncNow.Invalidate();
                }

                if (chkRememberMe != null)
                {
                    chkRememberMe.AccentColor = _accentColor;
                    chkRememberMe.Invalidate();
                }

                if (btnClose != null)
                {
                    btnClose.SetTheme(_accentColor, _secondaryAccentColor);
                    btnClose.Invalidate();
                }

                if (picUserAvatar != null)
                {
                    picUserAvatar.SetTheme(_accentColor, _secondaryAccentColor);
                    picUserAvatar.Invalidate();
                }

                if (lblSyncStatus != null)
                {
                    lblSyncStatus.ForeColor = _accentColor;
                }

                if (lblStats != null)
                {
                    lblStats.BackColor = _cardBg;
                    lblStats.Invalidate();
                }

                if (lblCopyright != null)
                {
                    lblCopyright.ForeColor = Color.FromArgb(120, 115, 140);
                }

                if (lblVersion != null)
                {
                    lblVersion.ForeColor = Color.FromArgb(170, 165, 195);
                }

                pnlEmailBox?.Invalidate();
                pnlPasswordBox?.Invalidate();
                pnlConfirmPasswordBox?.Invalidate();

                Invalidate();
            }
            catch { }
        }

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

            EventHandler onSyncStatus = (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try { BeginInvoke(new Action(UpdateSyncStatus)); } catch { }
                }
            };
            EventHandler onDataChanged = (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try { BeginInvoke(new Action(UpdateStats)); } catch { }
                }
            };

            EventHandler onAvatarChanged = (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try { BeginInvoke(new Action(() => picUserAvatar?.Invalidate())); } catch { }
                }
            };
            FirebaseAuthService.Instance.AvatarChanged += onAvatarChanged;
            FirebaseSyncService.Instance.SyncStatusChanged += onSyncStatus;
            SavedTracksService.Instance.SavedTracksChanged += onDataChanged;
            HistoryService.HistoryChanged += onDataChanged;
            PlaylistCollectionService.Instance.PlaylistsChanged += onDataChanged;

            this.FormClosed += (s, e) =>
            {
                FirebaseSyncService.Instance.SyncStatusChanged -= onSyncStatus;
                SavedTracksService.Instance.SavedTracksChanged -= onDataChanged;
                HistoryService.HistoryChanged -= onDataChanged;
                PlaylistCollectionService.Instance.PlaylistsChanged -= onDataChanged;
            };
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;

            this.KeyPreview = true;
            this.Text = "Tài Khoản Kahoii";
            this.Size = new Size(340, 378);
            this.BackColor = _bgGradBottom;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;

            lblTitle = new Label
            {
                Text = "Tài Khoản Kahoii",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(14, 11),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            lblTitle.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    this.Capture = false;
                    Message msg = Message.Create(this.Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
                    this.DefWndProc(ref msg);
                }
            };

            btnClose = new GradientCloseButton
            {
                Location = new Point(this.Width - 34, 7),
                Size = new Size(26, 26)
            };
            btnClose.SetTheme(_accentColor, _secondaryAccentColor);
            btnClose.Click += (s, e) => this.Hide();

            lblVersion = new Label
            {
                Text = $"v{AppUpdateService.CurrentVersionName}  •  Kiểm tra cập nhật",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(170, 165, 195),
                Location = new Point(20, 330),
                Size = new Size(300, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            lblVersion.MouseEnter += (s, e) =>
            {
                lblVersion.ForeColor = _accentColor;
                lblVersion.Font = new Font("Segoe UI", 8.5F, FontStyle.Underline);
            };
            lblVersion.MouseLeave += (s, e) =>
            {
                lblVersion.ForeColor = Color.FromArgb(170, 165, 195);
                lblVersion.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            };
            lblVersion.Click += async (s, e) =>
            {
                if (Form1.Instance != null)
                {
                    lblVersion.Text = "Đang kiểm tra cập nhật...";
                    lblVersion.Enabled = false;
                    try
                    {
                        await Form1.Instance.CheckForAppUpdatesAsync(manual: true);
                    }
                    finally
                    {
                        lblVersion.Enabled = true;
                        lblVersion.Text = $"v{AppUpdateService.CurrentVersionName}  •  Kiểm tra cập nhật";
                    }
                }
            };

            lblCopyright = new Label
            {
                Text = "Copyright by Kahoii-san",
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 115, 140),
                Location = new Point(20, 350),
                Size = new Size(300, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            this.Controls.Add(lblTitle);
            this.Controls.Add(btnClose);
            this.Controls.Add(lblVersion);
            this.Controls.Add(lblCopyright);

            SetupAuthUI();
            SetupProfileUI();
            UpdateRegion();
        }

        private void SetupAuthUI()
        {
            // Segmented Tab Pill
            tabSelector = new CustomTabPill
            {
                Location = new Point(20, 50),
                Size = new Size(300, 34),
                ActiveColor = _accentColor,
                BackColor = _cardBg
            };
            tabSelector.TabChanged += (s, isRegister) => SwitchTab(isRegister);
            this.Controls.Add(tabSelector);

            // Email Box
            pnlEmailBox = CreateInputBox(20, 96, "✉", "Email của bạn...", false, out txtEmail);
            this.Controls.Add(pnlEmailBox);

            // Password Box
            pnlPasswordBox = CreateInputBox(20, 144, "🔒", "Mật khẩu (tối thiểu 6 ký tự)...", true, out txtPassword);
            this.Controls.Add(pnlPasswordBox);

            // Confirm Password Box (for Register)
            pnlConfirmPasswordBox = CreateInputBox(20, 192, "🔒", "Nhập lại mật khẩu...", true, out txtConfirmPassword);
            pnlConfirmPasswordBox.Visible = false;
            this.Controls.Add(pnlConfirmPasswordBox);

            // Remember Login Checkbox
            chkRememberMe = new CustomCheckBox
            {
                Location = new Point(20, 186),
                Size = new Size(160, 22),
                Checked = true,
                AccentColor = _accentColor
            };
            this.Controls.Add(chkRememberMe);

            // Forgot Password Button
            btnForgotPassword = new Label
            {
                Text = "Quên mật khẩu?",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Underline),
                ForeColor = Color.FromArgb(170, 165, 195),
                Location = new Point(190, 187),
                Size = new Size(130, 20),
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnForgotPassword.MouseEnter += (s, e) => btnForgotPassword.ForeColor = _accentColor;
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
                    ShowMessageBox(res.Item2, "Khôi phục mật khẩu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ShowError(res.Item2);
                }
            };
            this.Controls.Add(btnForgotPassword);

            // Error Label
            lblError = new Label
            {
                Location = new Point(20, 212),
                Size = new Size(300, 20),
                ForeColor = Color.FromArgb(255, 100, 125),
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false,
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblError);

            // Submit Button
            btnSubmit = new RoundedButton
            {
                Text = "ĐĂNG NHẬP",
                Location = new Point(20, 234),
                Size = new Size(300, 38),
                ButtonColor = _accentColor,
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

                        await FirebaseSyncService.Instance.TriggerAutoFetchAsync();
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

                        await FirebaseSyncService.Instance.TriggerAutoFetchAsync();
                    }
                }
                btnSubmit.Enabled = true;
            };
            this.Controls.Add(btnSubmit);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
            if (btnClose != null) btnClose.Location = new Point(this.Width - 34, 7);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y <= 42)
            {
                this.Capture = false;
                Message msg = Message.Create(this.Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
                this.DefWndProc(ref msg);
            }
        }

        private bool _isUpdatingRegion = false;
        private Size _lastRegionSize = Size.Empty;

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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int w = ClientRectangle.Width;
            int h = ClientRectangle.Height;
            if (w <= 0 || h <= 0) return;

            // 1. Frosted Liquid Glass Background Gradient
            using (var bgBrush = new LinearGradientBrush(new Rectangle(0, 0, w, h), _bgGradTop, _bgGradBottom, 90f))
            using (var path = RoundedRect(new Rectangle(0, 0, w, h), 14))
            {
                g.FillPath(bgBrush, path);
            }

            // 2. Liquid Glass Specular highlight sheen along the top edge
            using (var highlightPen = new Pen(Color.FromArgb(55, 255, 255, 255), 1.0f))
            {
                g.DrawLine(highlightPen, 18, 1, w - 19, 1);
            }

            // 3. Frosted header divider line at Y = 40
            using (var divPen = new Pen(Color.FromArgb(40, _secondaryAccentColor), 1f))
            {
                g.DrawLine(divPen, 14, 40, w - 14, 40);
            }

            // 4. Cyber neon gradient border (inset by 1px so it's 100% inside Region and never clipped)
            Rectangle borderRect = new Rectangle(1, 1, w - 2, h - 2);
            using (var borderBrush = new LinearGradientBrush(borderRect, _borderGradColor1, _borderGradColor2, 45f))
            using (var borderPen = new Pen(borderBrush, 1.5f))
            using (var borderPath = RoundedRect(borderRect, 13))
            {
                g.DrawPath(borderPen, borderPath);
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
                BackColor = Color.FromArgb(200, 24, 20, 36)
            };

            bool isFocused = false;
            pnl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                Color borderC = isFocused ? _accentColor : Color.FromArgb(60, _secondaryAccentColor);
                using (var pen = new Pen(borderC, isFocused ? 1.5f : 1f))
                using (var path = RoundedRect(new Rectangle(0, 0, pnl.Width - 1, pnl.Height - 1), 6))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };

            var lblIcon = new Label
            {
                Text = iconText,
                Font = new Font("Segoe UI Symbol", 10F),
                ForeColor = Color.FromArgb(160, 155, 180),
                Location = new Point(8, 7),
                Size = new Size(20, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            pnl.Controls.Add(lblIcon);

            var txt = new TextBox
            {
                Location = new Point(32, 9),
                Width = 258,
                BackColor = Color.FromArgb(24, 20, 36),
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
            if (lblError != null)
            {
                lblError.Visible = false;
                lblError.Text = "";
            }

            if (register)
            {
                if (pnlConfirmPasswordBox != null) pnlConfirmPasswordBox.Visible = true;
                if (btnForgotPassword != null) btnForgotPassword.Visible = false;
                if (chkRememberMe != null) chkRememberMe.Location = new Point(20, 234);
                if (lblError != null) lblError.Location = new Point(20, 260);
                if (btnSubmit != null)
                {
                    btnSubmit.Location = new Point(20, 282);
                    btnSubmit.Text = "TẠO TÀI KHOẢN";
                }
                this.Height = 376;
                if (lblVersion != null)
                {
                    lblVersion.Location = new Point(20, 326);
                    lblVersion.BringToFront();
                }
                if (lblCopyright != null)
                {
                    lblCopyright.Location = new Point(20, 348);
                    lblCopyright.BringToFront();
                }
            }
            else
            {
                if (pnlConfirmPasswordBox != null) pnlConfirmPasswordBox.Visible = false;
                if (btnForgotPassword != null)
                {
                    btnForgotPassword.Visible = true;
                    btnForgotPassword.Location = new Point(190, 187);
                }
                if (chkRememberMe != null) chkRememberMe.Location = new Point(20, 186);
                if (lblError != null) lblError.Location = new Point(20, 212);
                if (btnSubmit != null)
                {
                    btnSubmit.Location = new Point(20, 234);
                    btnSubmit.Text = "ĐĂNG NHẬP";
                }
                this.Height = 328;
                if (lblVersion != null)
                {
                    lblVersion.Location = new Point(20, 278);
                    lblVersion.BringToFront();
                }
                if (lblCopyright != null)
                {
                    lblCopyright.Location = new Point(20, 300);
                    lblCopyright.BringToFront();
                }
            }
            UpdateRegion();
            if (Owner is Form1 f1 && f1.Visible)
            {
                f1.UpdateAccountPopupLocation();
            }
            Invalidate();
        }

        private async void ChangeAvatar()
        {
            _isShowingChildDialog = true;
            try
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Chọn ảnh đại diện";
                    ofd.Filter = "Tệp hình ảnh (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";
                    ofd.Multiselect = false;
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        try
                        {
                            Image rawImg = null;
                            byte[] imgBytes = System.IO.File.ReadAllBytes(ofd.FileName);
                            using (var ms = new System.IO.MemoryStream(imgBytes))
                            {
                                rawImg = new Bitmap(Image.FromStream(ms));
                            }

                            using (rawImg)
                            using (var cropDialog = new Controls.ImageCropDialog(rawImg, "Cắt Ảnh Đại Diện"))
                            {
                                if (cropDialog.ShowDialog(this) == DialogResult.OK && cropDialog.CroppedImage != null)
                                {
                                    await FirebaseAuthService.Instance.SaveAndUploadAvatarAsync(cropDialog.CroppedImage);
                                    picUserAvatar?.Invalidate();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ShowMessageBox("Không thể tải ảnh: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            finally
            {
                _isShowingChildDialog = false;
            }
        }

        private void SetupProfileUI()
        {
            picUserAvatar = new UserAvatarBox
            {
                Location = new Point(132, 48),
                Size = new Size(76, 76)
            };
            picUserAvatar.SetTheme(_accentColor, _secondaryAccentColor);
            picUserAvatar.Click += (s, e) => ChangeAvatar();

            lblUserEmail = new Label
            {
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 128),
                Size = new Size(340, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            lblUserId = new Label
            {
                Font = new Font("Consolas", 8F),
                ForeColor = Color.FromArgb(135, 130, 155),
                Location = new Point(0, 150),
                Size = new Size(340, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            lblStats = new Label
            {
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(220, 215, 240),
                Location = new Point(20, 172),
                Size = new Size(300, 44),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblStats.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Rectangle r = new Rectangle(0, 0, lblStats.Width - 1, lblStats.Height - 1);
                using (var bgBrush = new SolidBrush(_cardBg))
                using (var path = RoundedRect(r, 8))
                {
                    e.Graphics.FillPath(bgBrush, path);
                }
                using (var pen = new Pen(Color.FromArgb(80, _secondaryAccentColor), 1f))
                using (var path = RoundedRect(r, 8))
                {
                    e.Graphics.DrawPath(pen, path);
                }

                if (!string.IsNullOrEmpty(lblStats.Text))
                {
                    TextRenderer.DrawText(e.Graphics, lblStats.Text, lblStats.Font, lblStats.ClientRectangle, lblStats.ForeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };

            lblSyncStatus = new Label
            {
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = _accentColor,
                Location = new Point(20, 222),
                Size = new Size(300, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            btnSyncNow = new RoundedButton
            {
                Text = "⟳  Đồng Bộ Ngay (Tự Động Bật)",
                Location = new Point(20, 246),
                Size = new Size(300, 36),
                ButtonColor = _accentColor,
                TextColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSyncNow.Click += async (s, e) =>
            {
                btnSyncNow.Enabled = false;
                btnSyncNow.Text = "⟳  Đang Đồng Bộ...";
                try
                {
                    await FirebaseSyncService.Instance.TriggerManualSyncAsync();
                }
                finally
                {
                    btnSyncNow.Enabled = true;
                    btnSyncNow.Text = "⟳  Đồng Bộ Ngay (Tự Động Bật)";
                    UpdateSyncStatus();
                    UpdateStats();
                }
            };

            btnSignOut = new RoundedButton
            {
                Text = "Đăng Xuất",
                Location = new Point(20, 288),
                Size = new Size(300, 34),
                ButtonColor = Color.FromArgb(36, 22, 32),
                TextColor = Color.FromArgb(255, 120, 130),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSignOut.Click += (s, e) =>
            {
                var ask = ShowMessageBox("Bạn có muốn đăng xuất tài khoản không?\n\n(Danh sách bài hát của bạn đã được lưu an toàn trên đám mây và sẽ tự động tải về khi bạn đăng nhập lại).",
                    "Đăng xuất", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ask == DialogResult.Yes)
                {
                    string currentPlayingId = Form1.Instance?.AudioPlayer?.CurrentVideoId;
                    Form1.Instance?.AudioPlayer?.RetainOnlyCurrentTrack();
                    FirebaseAuthService.Instance.SignOut(currentPlayingId);
                }
            };

            this.Controls.Add(picUserAvatar);
            this.Controls.Add(lblUserEmail);
            this.Controls.Add(lblUserId);
            this.Controls.Add(lblStats);
            this.Controls.Add(lblSyncStatus);
            this.Controls.Add(btnSyncNow);
            this.Controls.Add(btnSignOut);
        }

        private void RefreshViewState()
        {
            bool loggedIn = FirebaseAuthService.Instance.IsLoggedIn;

            // Auth controls
            if (tabSelector != null) tabSelector.Visible = !loggedIn;
            if (pnlEmailBox != null) pnlEmailBox.Visible = !loggedIn;
            if (pnlPasswordBox != null) pnlPasswordBox.Visible = !loggedIn;
            if (chkRememberMe != null) chkRememberMe.Visible = !loggedIn;
            if (btnSubmit != null) btnSubmit.Visible = !loggedIn;

            // Profile controls
            if (picUserAvatar != null) picUserAvatar.Visible = loggedIn;
            if (lblUserEmail != null) lblUserEmail.Visible = loggedIn;
            if (lblUserId != null) lblUserId.Visible = loggedIn;
            if (lblStats != null) lblStats.Visible = loggedIn;
            if (lblSyncStatus != null) lblSyncStatus.Visible = loggedIn;
            if (btnSyncNow != null) btnSyncNow.Visible = loggedIn;
            if (btnSignOut != null) btnSignOut.Visible = loggedIn;

            if (loggedIn)
            {
                if (pnlConfirmPasswordBox != null) pnlConfirmPasswordBox.Visible = false;
                if (btnForgotPassword != null) btnForgotPassword.Visible = false;
                if (lblError != null) lblError.Visible = false;

                this.Height = 378;
                if (lblVersion != null)
                {
                    lblVersion.Location = new Point(20, 330);
                    lblVersion.BringToFront();
                }
                if (lblCopyright != null)
                {
                    lblCopyright.Location = new Point(20, 350);
                    lblCopyright.BringToFront();
                }

                var user = FirebaseAuthService.Instance.CurrentUser;
                if (lblUserEmail != null) lblUserEmail.Text = user?.email ?? "User";
                if (lblUserId != null) lblUserId.Text = "UID: " + (user?.localId ?? "");
                UpdateStats();
                UpdateSyncStatus();
            }
            else
            {
                SwitchTab(_isRegisterMode);
            }
            UpdateRegion();
            if (Owner is Form1 f1 && f1.Visible)
            {
                f1.UpdateAccountPopupLocation();
            }
            Invalidate();
        }

        private void UpdateStats()
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(UpdateStats)); } catch { }
                return;
            }
            int savedCount = SavedTracksService.Instance.GetSavedTracks().Count;
            int historyCount = HistoryService.GetHistory().Count;
            int collectionCount = PlaylistCollectionService.Instance.GetPlaylists().Count;

            lblStats.Text = string.Format("Đã tải: {0} bài  •  Bộ sưu tập: {1}\r\nLịch sử nghe: {2} bài",
                savedCount, collectionCount, historyCount);
            lblStats.Invalidate();
        }

        private void UpdateSyncStatus()
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(UpdateSyncStatus)); } catch { }
                return;
            }
            lblSyncStatus.Text = FirebaseSyncService.Instance.SyncStatusMessage;
            if (btnSyncNow != null)
            {
                bool syncing = FirebaseSyncService.Instance.IsSyncing;
                btnSyncNow.Enabled = !syncing;
                btnSyncNow.Text = syncing ? "⟳  Đang Đồng Bộ..." : "⟳  Đồng Bộ Ngay (Tự Động Bật)";
            }
            UpdateStats();
        }

        private static GraphicsPath RoundedRect(Rectangle rc, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (rc.Width <= 0 || rc.Height <= 0) return path;
            int maxRadius = Math.Min(rc.Width, rc.Height) / 2;
            radius = Math.Max(1, Math.Min(radius, maxRadius));
            int d = radius * 2;
            if (d > 0 && rc.Width >= d && rc.Height >= d)
            {
                path.AddArc(rc.X, rc.Y, d, d, 180, 90);
                path.AddArc(rc.Right - d, rc.Y, d, d, 270, 90);
                path.AddArc(rc.Right - d, rc.Bottom - d, d, d, 0, 90);
                path.AddArc(rc.X, rc.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
            }
            else
            {
                path.AddRectangle(rc);
            }
            return path;
        }

        private static Color BlendColor(Color from, Color to, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            int r = (int)(from.R + (to.R - from.R) * amount);
            int g = (int)(from.G + (to.G - from.G) * amount);
            int b = (int)(from.B + (to.B - from.B) * amount);
            return Color.FromArgb(r, g, b);
        }

        private DialogResult ShowMessageBox(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            _isShowingChildDialog = true;
            try
            {
                if (buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel)
                {
                    bool ok = CyberConfirmDialog.ShowConfirm(caption, text, "Đồng ý", "Để sau", this);
                    return ok ? DialogResult.Yes : DialogResult.No;
                }
                else
                {
                    CyberConfirmDialog.ShowAlert(caption, text, "Đã hiểu", this);
                    return DialogResult.OK;
                }
            }
            finally
            {
                _isShowingChildDialog = false;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW: stays strictly within app window hierarchy
                return cp;
            }
        }

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_ACTIVATE = 1;
        private const int MA_NOACTIVATE = 3;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                if (FirebaseAuthService.Instance.IsLoggedIn)
                {
                    m.Result = (IntPtr)MA_NOACTIVATE;
                    return;
                }

                Point p = PointToClient(Cursor.Position);
                Control child = GetChildAtPoint(p);
                if (child == pnlEmailBox || child == pnlPasswordBox || child == pnlConfirmPasswordBox)
                {
                    m.Result = (IntPtr)MA_ACTIVATE;
                    base.WndProc(ref m);
                    return;
                }
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _lastDeactivatedTime = DateTime.UtcNow;
            if (_isShowingChildDialog) return;
            if (Owner is Form1 f1 && f1.IsFormClosed) { Hide(); return; }

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (Visible)
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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Hide();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (this.Visible)
            {
                RefreshViewState();
            }
            else
            {
                if (Owner != null && !Owner.IsDisposed)
                {
                    try { Owner.Activate(); } catch { }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                base.OnFormClosing(e);
            }
        }

        // ================= HELPER CONTROLS =================

        // 1b. Custom User Avatar Box with Pencil Edit Badge
        private class UserAvatarBox : Control
        {
            private bool _isHovered = false;
            private Color _accentColor = Color.FromArgb(255, 45, 120);
            private Color _secondaryAccentColor = Color.FromArgb(168, 85, 247);

            public void SetTheme(Color accent, Color secondary)
            {
                _accentColor = accent;
                _secondaryAccentColor = secondary;
                Invalidate();
            }

            public UserAvatarBox()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Size = new Size(76, 76);
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                try
                {
                    if (Width <= 4 || Height <= 4) return;

                    Graphics g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    int d = Math.Min(66, Math.Min(Width - 4, Height - 4));
                    if (d < 4) return;
                    Rectangle rCircle = new Rectangle(2, 2, d, d);

                    // 1. Tỏa sáng gradient viền ngoài
                    int glowAlpha = _isHovered ? 160 : 80;
                    using (Pen glowPen = new Pen(Color.FromArgb(glowAlpha, _accentColor), 2.5f))
                    {
                        g.DrawEllipse(glowPen, 1, 1, d + 2, d + 2);
                    }

                    // 2. Nội dung Avatar tròn
                    Rectangle rInner = new Rectangle(3, 3, d - 2, d - 2);
                    Image avatar = FirebaseAuthService.Instance.UserAvatar;
                    bool drewAvatar = false;
                    if (avatar != null)
                    {
                        try
                        {
                            using (GraphicsPath clip = new GraphicsPath())
                            {
                                clip.AddEllipse(rInner);
                                g.SetClip(clip);
                                g.DrawImage(avatar, rInner);
                                g.ResetClip();
                            }
                            drewAvatar = true;
                        }
                        catch { }
                    }

                    if (!drewAvatar)
                    {
                        Color topTint = BlendColor(Color.FromArgb(38, 20, 36), _accentColor, 0.25f);
                        Color botTint = BlendColor(Color.FromArgb(20, 14, 28), _secondaryAccentColor, 0.2f);
                        using (LinearGradientBrush bgBrush = new LinearGradientBrush(rInner, topTint, botTint, 45f))
                        {
                            g.FillEllipse(bgBrush, rInner);
                        }
                        int cx = rInner.X + rInner.Width / 2;
                        int cy = rInner.Y + rInner.Height / 2;
                        using (SolidBrush iconBrush = new SolidBrush(_accentColor))
                        using (Pen iconPen = new Pen(_accentColor, 2.2f))
                        {
                            iconPen.StartCap = LineCap.Round;
                            iconPen.EndCap = LineCap.Round;
                            g.FillEllipse(iconBrush, cx - 9, cy - 12, 18, 18);
                            g.DrawArc(iconPen, cx - 16, cy + 2, 32, 24, 195, 150);
                        }
                    }

                    // 3. Viền gradient sắc nét
                    using (LinearGradientBrush lgb = new LinearGradientBrush(rCircle, _accentColor, _secondaryAccentColor, 45f))
                    using (Pen gradPen = new Pen(lgb, 2.2f))
                    {
                        g.DrawEllipse(gradPen, rCircle);
                    }

                    // 4. Pencil Edit Badge ở góc dưới bên phải
                    int badgeX = Math.Max(2, d - 20);
                    int badgeY = Math.Max(2, d - 20);
                    int badgeD = 24;
                    Rectangle rBadge = new Rectangle(badgeX, badgeY, badgeD, badgeD);

                    // Viền tối ngăn cách badge với ảnh avatar
                    using (Pen sepPen = new Pen(Color.FromArgb(22, 18, 30), 2.5f))
                    {
                        g.DrawEllipse(sepPen, rBadge);
                    }

                    Color badgeBg = _isHovered ? ControlPaint.Light(_accentColor, 0.2f) : _accentColor;
                    using (SolidBrush badgeBrush = new SolidBrush(badgeBg))
                    {
                        g.FillEllipse(badgeBrush, rBadge);
                    }

                    // Vẽ icon bút chì sắc nét bên trong badge
                    using (Pen pPen = new Pen(Color.White, 2f))
                    {
                        pPen.StartCap = LineCap.Round;
                        pPen.EndCap = LineCap.Round;
                        g.DrawLine(pPen, badgeX + 7, badgeY + 16, badgeX + 15, badgeY + 8);
                        g.FillPolygon(Brushes.White, new PointF[] {
                            new PointF(badgeX + 6, badgeY + 17),
                            new PointF(badgeX + 9, badgeY + 17),
                            new PointF(badgeX + 7, badgeY + 14)
                        });
                    }
                }
                catch { }
            }
        }


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
            protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
            protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.HighQuality;

                Color c = _isHovered ? ControlPaint.Light(ButtonColor, 0.15f) : ButtonColor;
                using (var brush = new SolidBrush(c))
                using (var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
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
                using (var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                {
                    g.FillPath(bgBrush, path);
                }

                // Active tab pill
                int tabW = (Width - 6) / 2;
                int tabH = Height - 6;
                int activeX = _isRegister ? Width / 2 : 3;
                int activeY = 3;

                using (var activeBrush = new SolidBrush(ActiveColor))
                using (var activePath = RoundedRect(new Rectangle(activeX, activeY, tabW, tabH), 6))
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
            private Color _accentColor = Color.FromArgb(255, 38, 128);
            private Color _secondaryAccentColor = Color.FromArgb(180, 50, 255);

            public void SetTheme(Color accent, Color secondary)
            {
                _accentColor = accent;
                _secondaryAccentColor = secondary;
                Invalidate();
            }

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

                // Subtle soft ambient glow background on hover
                if (_hoverProgress > 0.02f)
                {
                    int bgAlpha = (int)(25 * _hoverProgress);
                    using (var bgBrush = new SolidBrush(Color.FromArgb(bgAlpha, _accentColor)))
                    using (var path = RoundedRect(new Rectangle(2, 2, Width - 5, Height - 5), 6))
                    {
                        g.FillPath(bgBrush, path);
                    }

                    // Soft neon bloom behind the 'X'
                    int glowAlpha = (int)(65 * _hoverProgress);
                    using (var glowPen = new Pen(Color.FromArgb(glowAlpha, _accentColor), 3.8f))
                    {
                        glowPen.StartCap = LineCap.Round;
                        glowPen.EndCap = LineCap.Round;
                        g.DrawLine(glowPen, cx - r, cy - r, cx + r, cy + r);
                        g.DrawLine(glowPen, cx + r, cy - r, cx - r, cy + r);
                    }
                }

                if (_hoverProgress < 0.02f)
                {
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
                    var gradRect = new Rectangle(cx - r - 1, cy - r - 1, (r * 2) + 2, (r * 2) + 2);
                    Color secColor = _isPressed ? ControlPaint.Dark(_secondaryAccentColor, 0.1f) : _secondaryAccentColor;
                    Color priColor = _isPressed ? ControlPaint.Dark(_accentColor, 0.1f) : _accentColor;

                    Color startC = BlendColor(Color.FromArgb(160, 160, 175), secColor, _hoverProgress);
                    Color endC = BlendColor(Color.FromArgb(160, 160, 175), priColor, _hoverProgress);

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

            public Color AccentColor { get; set; } = Color.FromArgb(255, 38, 128);
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
                    using (var brush = new SolidBrush(AccentColor))
                    using (var path = RoundedRect(boxRect, 4))
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
                    using (var path = RoundedRect(boxRect, 4))
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
                if (r.Width <= 0 || r.Height <= 0) return path;
                int maxRadius = Math.Min(r.Width, r.Height) / 2;
                radius = Math.Max(1, Math.Min(radius, maxRadius));
                int d = radius * 2;
                if (d > 0 && r.Width >= d && r.Height >= d)
                {
                    path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                }
                else
                {
                    path.AddRectangle(r);
                }
                return path;
            }
        }
    }
}