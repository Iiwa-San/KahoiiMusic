using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class CyberConfirmDialog : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private bool _result = false;
        private readonly bool _isConfirm;

        public CyberConfirmDialog(string title, string message, string yesText = "Đồng ý", string noText = "Để sau", bool isConfirm = true)
        {
            _isConfirm = isConfirm;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(20, 16, 30);
            Size = new Size(470, 260);

            // True rounded window shape
            Region = new Region(GetRoundedRect(new Rectangle(0, 0, Width, Height), 16));

            // Allow dragging
            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };

            // Close '✕' Button
            Label btnClose = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 140, 165),
                Location = new Point(Width - 36, 14),
                Size = new Size(24, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.FromArgb(255, 45, 117);
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(140, 140, 165);
            btnClose.Click += (s, e) =>
            {
                _result = false;
                Close();
            };
            Controls.Add(btnClose);

            // Header Icon / Badge
            Label lblIcon = new Label
            {
                Text = "⚡",
                Font = new Font("Segoe UI Emoji", 14f),
                ForeColor = Color.FromArgb(255, 45, 117),
                Location = new Point(22, 16),
                Size = new Size(28, 28),
                BackColor = Color.Transparent
            };

            // Title Label
            Label lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(54, 18),
                Size = new Size(370, 26),
                BackColor = Color.Transparent
            };

            // Message Label
            Label lblMessage = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(220, 220, 235),
                Location = new Point(24, 56),
                Size = new Size(422, 126),
                BackColor = Color.Transparent
            };

            // Primary / Confirm Button
            Button btnYes = new Button
            {
                Text = yesText,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(168, 85, 247),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(136, 38),
                Location = isConfirm ? new Point(176, 202) : new Point(310, 202),
                Cursor = Cursors.Hand
            };
            btnYes.FlatAppearance.BorderSize = 0;
            btnYes.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush br = new LinearGradientBrush(
                    btnYes.ClientRectangle,
                    Color.FromArgb(255, 45, 117),
                    Color.FromArgb(124, 58, 237),
                    LinearGradientMode.Horizontal))
                {
                    using (GraphicsPath p = GetRoundedRect(new Rectangle(0, 0, btnYes.Width, btnYes.Height), 8))
                    {
                        g.FillPath(br, p);
                    }
                }
                TextRenderer.DrawText(g, btnYes.Text, btnYes.Font, btnYes.ClientRectangle, btnYes.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            btnYes.Click += (s, e) =>
            {
                _result = true;
                Close();
            };

            Controls.Add(lblIcon);
            Controls.Add(lblTitle);
            Controls.Add(lblMessage);
            Controls.Add(btnYes);

            // Secondary / Cancel Button (only for confirm)
            if (isConfirm)
            {
                Button btnNo = new Button
                {
                    Text = noText,
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = Color.FromArgb(170, 170, 195),
                    BackColor = Color.FromArgb(34, 28, 48),
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(116, 38),
                    Location = new Point(326, 202),
                    Cursor = Cursors.Hand
                };
                btnNo.FlatAppearance.BorderSize = 0;
                btnNo.Paint += (s, e) =>
                {
                    Graphics g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (SolidBrush br = new SolidBrush(Color.FromArgb(34, 28, 48)))
                    {
                        using (GraphicsPath p = GetRoundedRect(new Rectangle(0, 0, btnNo.Width, btnNo.Height), 8))
                        {
                            g.FillPath(br, p);
                            using (Pen borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f))
                            {
                                g.DrawPath(borderPen, p);
                            }
                        }
                    }
                    TextRenderer.DrawText(g, btnNo.Text, btnNo.Font, btnNo.ClientRectangle, btnNo.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                };
                btnNo.Click += (s, e) =>
                {
                    _result = false;
                    Close();
                };
                Controls.Add(btnNo);
            }

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    _result = true;
                    Close();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    _result = false;
                    Close();
                }
            };
            KeyPreview = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw rounded cyber border with subtle neon gradient
            using (GraphicsPath path = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 14))
            {
                using (LinearGradientBrush borderBrush = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, Height),
                    Color.FromArgb(200, 255, 45, 117),
                    Color.FromArgb(140, 124, 58, 237),
                    LinearGradientMode.ForwardDiagonal))
                {
                    using (Pen borderPen = new Pen(borderBrush, 1.5f))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }
            }
        }

        private static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
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

        public static bool ShowConfirm(string title, string message, string yesText = "Chuyển ngay", string noText = "Để sau", Form owner = null)
        {
            using (var dlg = new CyberConfirmDialog(title, message, yesText, noText, isConfirm: true))
            {
                if (owner != null && owner.Visible)
                {
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ShowDialog(owner);
                }
                else
                {
                    dlg.StartPosition = FormStartPosition.CenterScreen;
                    dlg.TopMost = true;
                    dlg.ShowDialog();
                }
                return dlg._result;
            }
        }

        public static void ShowAlert(string title, string message, string okText = "Đã hiểu", Form owner = null)
        {
            using (var dlg = new CyberConfirmDialog(title, message, okText, "", isConfirm: false))
            {
                if (owner != null && owner.Visible)
                {
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ShowDialog(owner);
                }
                else
                {
                    dlg.StartPosition = FormStartPosition.CenterScreen;
                    dlg.TopMost = true;
                    dlg.ShowDialog();
                }
            }
        }
    }
}
