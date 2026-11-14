using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class InputDialog : Form
    {
        private TextBox _txtInput;
        private Label _lblPrompt;
        private Button _btnOk;
        private Button _btnCancel;
        private bool _confirmed = false;

        public string InputText => _txtInput.Text.Trim();

        public InputDialog(string prompt, string title, string defaultText = "")
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Size = new Size(360, 150);
            BackColor = Color.FromArgb(20, 16, 28);

            Text = title;

            // Prompt label
            _lblPrompt = new Label
            {
                Text = prompt,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 235, 245),
                Location = new Point(20, 18),
                Size = new Size(320, 22),
                BackColor = Color.Transparent
            };

            // Input TextBox
            _txtInput = new TextBox
            {
                Text = defaultText,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(35, 28, 48),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(20, 48),
                Size = new Size(320, 26)
            };
            _txtInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    _confirmed = true;
                    Close();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    Close();
                }
            };

            // OK Button
            _btnOk = new Button
            {
                Text = "Lưu",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(168, 85, 247),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 30),
                Location = new Point(170, 95),
                Cursor = Cursors.Hand
            };
            _btnOk.FlatAppearance.BorderSize = 0;
            _btnOk.Click += (s, e) =>
            {
                _confirmed = true;
                Close();
            };

            // Cancel Button
            _btnCancel = new Button
            {
                Text = "Hủy",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 195),
                BackColor = Color.FromArgb(40, 32, 54),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 30),
                Location = new Point(260, 95),
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => Close();

            Controls.Add(_lblPrompt);
            Controls.Add(_txtInput);
            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);

            Shown += (s, e) =>
            {
                _txtInput.Focus();
                _txtInput.SelectAll();
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw rounded card border
            using (GraphicsPath path = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            {
                using (Pen borderPen = new Pen(Color.FromArgb(90, 168, 85, 247), 1.5f))
                {
                    g.DrawPath(borderPen, path);
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

        public static string Show(Form owner, string title, string prompt, string defaultText = "")
        {
            return Show(prompt, title, defaultText, owner);
        }

        public static string Show(string prompt, string title, string defaultText = "", Form owner = null)
        {
            if (owner == null)
            {
                owner = Form.ActiveForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
            }

            using (var dlg = new InputDialog(prompt, title, defaultText))
            {
                if (owner != null && owner.Visible)
                {
                    dlg.StartPosition = FormStartPosition.CenterParent;
                }
                else
                {
                    dlg.StartPosition = FormStartPosition.CenterScreen;
                }
                dlg.ShowDialog(owner);

                if (owner != null && !owner.IsDisposed && owner.Visible)
                {
                    try { owner.Activate(); } catch { }
                }

                return dlg._confirmed ? dlg.InputText : null;
            }
        }
    }
}
