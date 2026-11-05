using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class ImageCropDialog : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private readonly Image _sourceImage;
        public Bitmap CroppedImage { get; private set; }

        private float _zoom = 1.0f;
        private float _baseFitZoom = 1.0f;
        private int _offsetX = 0;
        private int _offsetY = 0;
        private bool _isDragging = false;
        private Point _dragStart;

        private Panel _pnlHeader;
        private Label _lblTitle;
        private Label _btnClose;
        private CropViewportPanel _pnlViewport;
        private TrackBar _trackZoom;
        private Label _lblZoomIcon;
        private Label _lblHint;
        private Button _btnCancel;
        private Button _btnSave;

        private const int CropDiameter = 220;
        private readonly Color BgDark = Color.FromArgb(18, 15, 24);
        private readonly Color CardBg = Color.FromArgb(24, 20, 32);
        private readonly Color AccentPink = Color.FromArgb(255, 45, 120);

        public ImageCropDialog(Image sourceImage, string title = "Cắt Ảnh Đại Diện")
        {
            _sourceImage = sourceImage ?? throw new ArgumentNullException(nameof(sourceImage));

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Size = new Size(380, 480);
            BackColor = BgDark;

            InitializeComponents(title);
            CalculateInitialZoom();
        }

        private void InitializeComponents(string title)
        {
            // Header Panel
            _pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = CardBg
            };
            _pnlHeader.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };

            _lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(16, 8),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _pnlHeader.Controls.Add(_lblTitle);

            _btnClose = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(160, 155, 175),
                Location = new Point(Width - 32, 6),
                Size = new Size(24, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            _btnClose.MouseEnter += (s, e) => _btnClose.ForeColor = AccentPink;
            _btnClose.MouseLeave += (s, e) => _btnClose.ForeColor = Color.FromArgb(160, 155, 175);
            _btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _pnlHeader.Controls.Add(_btnClose);

            Controls.Add(_pnlHeader);

            // Viewport Panel
            _pnlViewport = new CropViewportPanel(this)
            {
                Location = new Point(15, 46),
                Size = new Size(350, 300),
                BackColor = Color.FromArgb(12, 10, 16),
                Cursor = Cursors.SizeAll
            };
            _pnlViewport.MouseDown += Viewport_MouseDown;
            _pnlViewport.MouseMove += Viewport_MouseMove;
            _pnlViewport.MouseUp += Viewport_MouseUp;
            _pnlViewport.MouseWheel += Viewport_MouseWheel;
            Controls.Add(_pnlViewport);

            // Hint label
            _lblHint = new Label
            {
                Text = "Kéo để di chuyển • Lăn chuột hoặc trượt để phóng to / thu nhỏ",
                Font = new Font("Segoe UI", 8.2F),
                ForeColor = Color.FromArgb(150, 145, 165),
                Location = new Point(15, 350),
                Size = new Size(350, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            Controls.Add(_lblHint);

            // Zoom Slider Panel
            Panel pnlZoom = new Panel
            {
                Location = new Point(30, 372),
                Size = new Size(320, 30),
                BackColor = Color.Transparent
            };

            _lblZoomIcon = new Label
            {
                Text = "🔍",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(180, 175, 195),
                Location = new Point(0, 4),
                Size = new Size(24, 20),
                BackColor = Color.Transparent
            };
            pnlZoom.Controls.Add(_lblZoomIcon);

            _trackZoom = new TrackBar
            {
                Location = new Point(28, 2),
                Size = new Size(290, 30),
                Minimum = 50,
                Maximum = 350,
                Value = 100,
                TickStyle = TickStyle.None,
                BackColor = BgDark,
                Cursor = Cursors.Hand
            };
            _trackZoom.ValueChanged += (s, e) =>
            {
                float factor = _trackZoom.Value / 100f;
                _zoom = _baseFitZoom * factor;
                _pnlViewport.Invalidate();
            };
            pnlZoom.Controls.Add(_trackZoom);
            Controls.Add(pnlZoom);

            // Action Buttons
            _btnCancel = new Button
            {
                Text = "Hủy",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 195, 215),
                BackColor = Color.FromArgb(32, 26, 42),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 32),
                Location = new Point(165, 424),
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnCancel);

            _btnSave = new Button
            {
                Text = "Cắt & Lưu",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = AccentPink,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(110, 32),
                Location = new Point(255, 424),
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += (s, e) =>
            {
                CroppedImage = GenerateCroppedImage();
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(_btnSave);
        }

        private void CalculateInitialZoom()
        {
            if (_sourceImage == null) return;
            float fitW = (float)CropDiameter / _sourceImage.Width;
            float fitH = (float)CropDiameter / _sourceImage.Height;
            _baseFitZoom = Math.Max(fitW, fitH);
            _zoom = _baseFitZoom;
            _trackZoom.Value = 100;
        }

        private void Viewport_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _dragStart = e.Location;
            }
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                int dx = e.X - _dragStart.X;
                int dy = e.Y - _dragStart.Y;
                _dragStart = e.Location;
                _offsetX += dx;
                _offsetY += dy;
                _pnlViewport.Invalidate();
            }
        }

        private void Viewport_MouseUp(object sender, MouseEventArgs e)
        {
            _isDragging = false;
        }

        private void Viewport_MouseWheel(object sender, MouseEventArgs e)
        {
            int step = e.Delta > 0 ? 10 : -10;
            int newVal = Math.Max(_trackZoom.Minimum, Math.Min(_trackZoom.Maximum, _trackZoom.Value + step));
            if (newVal != _trackZoom.Value)
            {
                _trackZoom.Value = newVal;
            }
        }

        internal void DrawViewport(Graphics g, Rectangle bounds)
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int cx = bounds.Width / 2;
            int cy = bounds.Height / 2;
            int r = CropDiameter / 2;

            // 1. Draw source image
            if (_sourceImage != null)
            {
                float drawW = _sourceImage.Width * _zoom;
                float drawH = _sourceImage.Height * _zoom;
                float drawX = cx + _offsetX - drawW / 2f;
                float drawY = cy + _offsetY - drawH / 2f;
                g.DrawImage(_sourceImage, drawX, drawY, drawW, drawH);
            }

            // 2. Dark mask overlay outside the circle
            Rectangle cropRect = new Rectangle(cx - r, cy - r, CropDiameter, CropDiameter);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.FillMode = FillMode.Alternate;
                path.AddRectangle(bounds);
                path.AddEllipse(cropRect);
                using (SolidBrush maskBrush = new SolidBrush(Color.FromArgb(185, 12, 10, 16)))
                {
                    g.FillPath(maskBrush, path);
                }
            }

            // 3. Glowing gradient border around crop circle
            using (LinearGradientBrush borderBrush = new LinearGradientBrush(
                new Point(cropRect.Left, cropRect.Top),
                new Point(cropRect.Right, cropRect.Bottom),
                AccentPink,
                Color.FromArgb(168, 85, 247)))
            using (Pen pen = new Pen(borderBrush, 2.5f))
            {
                g.DrawEllipse(pen, cropRect);
            }
        }

        public Bitmap GenerateCroppedImage()
        {
            int outputSize = 160;
            Bitmap bmp = new Bitmap(outputSize, outputSize, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                using (GraphicsPath clipPath = new GraphicsPath())
                {
                    clipPath.AddEllipse(0, 0, outputSize, outputSize);
                    g.SetClip(clipPath);
                }

                float scaleRatio = (float)outputSize / CropDiameter;
                float drawW = _sourceImage.Width * _zoom * scaleRatio;
                float drawH = _sourceImage.Height * _zoom * scaleRatio;
                float drawX = (outputSize / 2f) + (_offsetX * scaleRatio) - (drawW / 2f);
                float drawY = (outputSize / 2f) + (_offsetY * scaleRatio) - (drawH / 2f);

                g.DrawImage(_sourceImage, drawX, drawY, drawW, drawH);
            }
            return bmp;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw subtle border around the whole dialog
            using (Pen pen = new Pen(Color.FromArgb(56, 44, 76), 1.5f))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private class CropViewportPanel : Panel
        {
            private readonly ImageCropDialog _dialog;

            public CropViewportPanel(ImageCropDialog dialog)
            {
                _dialog = dialog;
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                _dialog.DrawViewport(e.Graphics, ClientRectangle);
            }
        }
    }
}
