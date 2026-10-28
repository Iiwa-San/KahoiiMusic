using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

class Program
{
    static void Main()
    {
        int W = 476;
        int H = 318;
        Color topColor = Color.FromArgb(43, 16, 127);
        Color bottomColor = Color.FromArgb(255, 38, 128);

        // Option A: Continuous gradient across entire card (0..318), 
        // with top bar (0..30) having a dark translucent glass overlay (làm mờ)
        // and bottom bar (222..318) having a dark translucent glass overlay (làm mờ)
        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;

            // 1. Full height continuous gradient
            using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, 0, W, H), topColor, bottomColor, LinearGradientMode.Vertical))
            {
                g.FillRectangle(lgb, 0, 0, W, H);
            }

            // 2. Top bar translucent dark overlay (làm mờ thanh tìm kiếm)
            using (SolidBrush topOverlay = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillRectangle(topOverlay, 0, 0, W, 30);
            }
            // Thin separator line
            using (Pen sep = new Pen(Color.FromArgb(30, 255, 255, 255), 1))
            {
                g.DrawLine(sep, 0, 30, W, 30);
            }

            // 3. Bottom controls translucent dark overlay (làm mờ vùng điều khiển)
            using (LinearGradientBrush bottomOverlay = new LinearGradientBrush(
                new Rectangle(0, 222, W, 96),
                Color.FromArgb(60, 0, 0, 0),
                Color.FromArgb(140, 0, 0, 0),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bottomOverlay, 0, 222, W, 96);
            }
            using (Pen sep = new Pen(Color.FromArgb(30, 255, 255, 255), 1))
            {
                g.DrawLine(sep, 0, 222, W, 222);
            }

            bmp.Save(@"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\option_a_glass_blur.png", ImageFormat.Png);
        }

        // Option B: Darkened/dimmed colors (tính toán màu tối mờ từ TopColor và BottomColor)
        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            // Top bar: darkened topColor (35% brightness)
            Color darkTop = Color.FromArgb(
                (int)(topColor.R * 0.4f),
                (int)(topColor.G * 0.4f),
                (int)(topColor.B * 0.4f));

            // Bottom bar: darkened bottomColor (35% brightness)
            Color darkBottom = Color.FromArgb(
                (int)(bottomColor.R * 0.4f),
                (int)(bottomColor.G * 0.4f),
                (int)(bottomColor.B * 0.4f));

            using (SolidBrush bTop = new SolidBrush(darkTop))
                g.FillRectangle(bTop, 0, 0, W, 30);

            using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, 30, W, 192), topColor, bottomColor, LinearGradientMode.Vertical))
                g.FillRectangle(lgb, 0, 30, W, 192);

            using (SolidBrush bBottom = new SolidBrush(darkBottom))
                g.FillRectangle(bBottom, 0, 222, W, 96);

            bmp.Save(@"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\option_b_darkened.png", ImageFormat.Png);
        }

        Console.WriteLine("Rendered both options!");
    }
}
