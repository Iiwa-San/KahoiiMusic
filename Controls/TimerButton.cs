using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KahoiiMusic.Services;

namespace KahoiiMusic.Controls
{
    public class TimerButton : Control
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        private readonly ToolTip _toolTip;

        private Color _inactiveColor = Color.FromArgb(220, 220, 220);
        private Color _inactiveHoverColor = Color.FromArgb(220, 220, 220);
        private Color _activeColor = Color.FromArgb(168, 85, 247);
        private Color _activeHoverColor = Color.FromArgb(192, 132, 252);

        public TimerButton()
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
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 400;
            _toolTip.ReshowDelay = 200;

            SleepTimerService.Instance.StateChanged += (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            Invalidate();
                            UpdateToolTip();
                        }));
                    }
                    catch { }
                }
            };

            UpdateToolTip();
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

        public bool IsTimerActive => SleepTimerService.Instance.IsActive;

        private void UpdateToolTip()
        {
            try
            {
                _toolTip.SetToolTip(this, SleepTimerService.Instance.GetStatusString());
            }
            catch { }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            UpdateToolTip();
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
                ShowTimerMenu();
            }
        }

        public void ShowTimerMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(24, 24, 28);
            menu.ForeColor = Color.FromArgb(235, 235, 235);
            menu.Renderer = new Form1.DarkMenuRenderer();
            menu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            var titleItem = new ToolStripMenuItem("⏱  Hẹn giờ tắt nhạc");
            titleItem.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            titleItem.Enabled = false;
            titleItem.ForeColor = Color.FromArgb(170, 170, 180);
            menu.Items.Add(titleItem);
            menu.Items.Add(new ToolStripSeparator());

            AddMenuItem(menu, "5 phút", SleepTimerOption.Minutes5);
            AddMenuItem(menu, "10 phút", SleepTimerOption.Minutes10);
            AddMenuItem(menu, "15 phút", SleepTimerOption.Minutes15);
            AddMenuItem(menu, "20 phút", SleepTimerOption.Minutes20);
            AddMenuItem(menu, "30 phút", SleepTimerOption.Minutes30);
            AddMenuItem(menu, "45 phút", SleepTimerOption.Minutes45);
            AddMenuItem(menu, "60 phút", SleepTimerOption.Minutes60);

            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "Hết bài hát hiện tại", SleepTimerOption.EndOfTrack);

            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "Tắt hẹn giờ", SleepTimerOption.Off);

            // Pop up upwards above the button so it is fully visible
            Point location = new Point(0, -menu.PreferredSize.Height - 4);
            menu.Show(this, location);
        }

        private void AddMenuItem(ContextMenuStrip menu, string text, SleepTimerOption option)
        {
            var item = new ToolStripMenuItem(text);
            item.Checked = (SleepTimerService.Instance.CurrentOption == option);
            item.Click += (s, e) =>
            {
                SleepTimerService.Instance.SetTimer(option);
                UpdateToolTip();
                Invalidate();
            };
            menu.Items.Add(item);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            bool active = IsTimerActive;
            Color drawColor;
            if (active)
            {
                drawColor = _isHovered ? _activeHoverColor : _activeColor;
            }
            else
            {
                drawColor = _isHovered ? _inactiveHoverColor : _inactiveColor;
            }

            if (_isPressed)
            {
                drawColor = Color.FromArgb(Math.Max(0, drawColor.R - 30),
                                           Math.Max(0, drawColor.G - 30),
                                           Math.Max(0, drawColor.B - 30));
            }

            float cx = Width / 2f;
            float cy = Height / 2f + 1f;
            float r = 6.2f;

            using (Pen pen = new Pen(drawColor, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                // 1. Clock circular dial
                g.DrawEllipse(pen, cx - r, cy - r, r * 2f, r * 2f);

                // 2. Stopwatch top knob / crown
                g.DrawLine(pen, cx, cy - r, cx, cy - r - 2.2f);
                g.DrawLine(pen, cx - 1.8f, cy - r - 2.2f, cx + 1.8f, cy - r - 2.2f);

                // 3. Hands inside clock:
                // Hour hand pointing to 12
                g.DrawLine(pen, cx, cy, cx, cy - 3.5f);
                // Minute hand pointing to 3 (or 2:15)
                g.DrawLine(pen, cx, cy, cx + 3.0f, cy + 0.5f);
            }

            // If active, draw small accent dot in center and small glowing status badge
            if (active)
            {
                using (SolidBrush dotBrush = new SolidBrush(drawColor))
                {
                    g.FillEllipse(dotBrush, cx - 1f, cy - 1f, 2f, 2f);
                    // Indicator pip at top right
                    g.FillEllipse(dotBrush, cx + r - 0.5f, cy - r - 1f, 3f, 3f);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

