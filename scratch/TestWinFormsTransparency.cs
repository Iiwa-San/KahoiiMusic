using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

class TestForm : Form
{
    public Panel panelCard;
    public Panel topPanel;
    public Panel midPanel;
    public Panel botPanel;

    public TestForm()
    {
        this.Size = new Size(558, 388);
        this.BackColor = Color.FromArgb(30, 30, 30);

        panelCard = new Panel() { Location = new Point(37, 42), Size = new Size(476, 318) };
        this.Controls.Add(panelCard);

        panelCard.Paint += (s, e) =>
        {
            Color topColor = Color.FromArgb(43, 16, 127);
            Color bottomColor = Color.FromArgb(255, 38, 128);
            Color darkTop = Color.FromArgb(Math.Max(16, (int)(topColor.R * 0.32f)), Math.Max(14, (int)(topColor.G * 0.32f)), Math.Max(26, (int)(topColor.B * 0.32f)));
            Color darkBottom = Color.FromArgb(Math.Max(28, (int)(bottomColor.R * 0.30f)), Math.Max(14, (int)(bottomColor.G * 0.30f)), Math.Max(22, (int)(bottomColor.B * 0.30f)));

            using (LinearGradientBrush lgb = new LinearGradientBrush(panelCard.ClientRectangle, Color.Black, Color.Black, LinearGradientMode.Vertical))
            {
                ColorBlend cb = new ColorBlend(4);
                cb.Colors = new Color[] { darkTop, topColor, bottomColor, darkBottom };
                cb.Positions = new float[] { 0.0f, 0.15f, 0.65f, 1.0f };
                lgb.InterpolationColors = cb;
                e.Graphics.FillRectangle(lgb, panelCard.ClientRectangle);
            }
        };

        topPanel = new Panel() { Location = new Point(0, 0), Size = new Size(476, 30), BackColor = Color.Transparent };
        midPanel = new Panel() { Location = new Point(0, 30), Size = new Size(476, 192), BackColor = Color.Transparent };
        botPanel = new Panel() { Location = new Point(0, 222), Size = new Size(476, 96), BackColor = Color.Transparent };

        panelCard.Controls.Add(topPanel);
        panelCard.Controls.Add(midPanel);
        panelCard.Controls.Add(botPanel);
    }
}

class Program
{
    [STAThread]
    static void Main()
    {
        using (TestForm f = new TestForm())
        {
            f.Show();
            Application.DoEvents();
            using (Bitmap bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(@"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\winforms_transparency_test.png", ImageFormat.Png);
            }
            f.Close();
        }
        Console.WriteLine("Done winforms transparency test!");
    }
}
