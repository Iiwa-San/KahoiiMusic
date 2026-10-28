using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

// Let's create a Form identical to Form1's structure to test this with real controls
class TestPlayerForm : Form
{
    public Panel panelPlayerCard;
    public Panel panelTopBar;
    public Panel gradientPanel;
    public Panel panelBottomControls;

    public Color TopColor = Color.FromArgb(43, 16, 127);
    public Color BottomColor = Color.FromArgb(255, 38, 128);

    public TestPlayerForm()
    {
        this.Size = new Size(558, 388);
        this.BackColor = Color.FromArgb(30, 30, 30);
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.CenterScreen;

        // Double buffer
        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        // Player Card
        panelPlayerCard = new Panel()
        {
            Location = new Point(37, 42),
            Size = new Size(476, 318)
        };
        this.Controls.Add(panelPlayerCard);

        panelPlayerCard.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            Color darkTop = Color.FromArgb(
                Math.Max(16, (int)(TopColor.R * 0.32f)),
                Math.Max(14, (int)(TopColor.G * 0.32f)),
                Math.Max(26, (int)(TopColor.B * 0.32f)));

            Color darkBottom = Color.FromArgb(
                Math.Max(28, (int)(BottomColor.R * 0.30f)),
                Math.Max(14, (int)(BottomColor.G * 0.30f)),
                Math.Max(22, (int)(BottomColor.B * 0.30f)));

            using (LinearGradientBrush lgb = new LinearGradientBrush(
                panelPlayerCard.ClientRectangle,
                Color.Black, Color.Black,
                LinearGradientMode.Vertical))
            {
                ColorBlend cb = new ColorBlend(4);
                cb.Colors = new Color[] { darkTop, TopColor, BottomColor, darkBottom };
                cb.Positions = new float[] { 0.0f, 0.15f, 0.65f, 1.0f };
                lgb.InterpolationColors = cb;
                e.Graphics.FillRectangle(lgb, panelPlayerCard.ClientRectangle);
            }
        };

        // 3 child panels with Transparent BackColor
        panelTopBar = new Panel() { Location = new Point(0, 0), Size = new Size(476, 30), BackColor = Color.Transparent };
        gradientPanel = new Panel() { Location = new Point(0, 30), Size = new Size(476, 192), BackColor = Color.Transparent };
        panelBottomControls = new Panel() { Location = new Point(0, 222), Size = new Size(476, 96), BackColor = Color.Transparent };

        panelPlayerCard.Controls.Add(panelTopBar);
        panelPlayerCard.Controls.Add(gradientPanel);
        panelPlayerCard.Controls.Add(panelBottomControls);

        // Search pill box inside top bar
        var pill = new KahoiiMusic.Controls.SearchPillBox()
        {
            Location = new Point(58, 5),
            Size = new Size(360, 20),
            Text = "https://www.youtube.com/watch?v=JKBHEKxndkE"
        };
        panelTopBar.Controls.Add(pill);

        // ArtworkBox inside gradientPanel
        var artwork = new KahoiiMusic.Controls.ArtworkBox()
        {
            Location = new Point(156, 0),
            Size = new Size(164, 192),
            TimeText = "00:37 / 03:42",
            IsSpinning = true
        };
        gradientPanel.Controls.Add(artwork);

        // Trackbar inside panelBottomControls
        var track = new KahoiiMusic.Controls.MusicTrackBar()
        {
            Location = new Point(23, 16),
            Size = new Size(430, 20),
            Value = 0.16f
        };
        panelBottomControls.Controls.Add(track);

        // Buttons
        var vol = new KahoiiMusic.Controls.VolumeButton() { Location = new Point(124, 47), Size = new Size(28, 24) };
        var prev = new KahoiiMusic.Controls.DirectionalButton() { Location = new Point(176, 47), Size = new Size(24, 24) };
        var play = new KahoiiMusic.Controls.PlayPauseButton() { Location = new Point(223, 44), Size = new Size(30, 30), IsPlaying = true };
        var next = new KahoiiMusic.Controls.DirectionalButton() { Location = new Point(276, 47), Size = new Size(24, 24), Direction = KahoiiMusic.Controls.DirectionType.Next };
        var loop = new KahoiiMusic.Controls.LoopButton() { Location = new Point(324, 47), Size = new Size(28, 24), IsLooping = true };

        panelBottomControls.Controls.Add(vol);
        panelBottomControls.Controls.Add(prev);
        panelBottomControls.Controls.Add(play);
        panelBottomControls.Controls.Add(next);
        panelBottomControls.Controls.Add(loop);
    }
}

class Program
{
    [STAThread]
    static void Main()
    {
        using (TestPlayerForm form = new TestPlayerForm())
        {
            form.Show();
            Application.DoEvents();

            using (Bitmap bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                string path = @"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\real_controls_seamless.png";
                bmp.Save(path, ImageFormat.Png);
                Console.WriteLine("Saved: " + path);
            }

            form.Close();
        }
    }
}
