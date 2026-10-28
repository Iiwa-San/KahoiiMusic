using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

class Program
{
    // Color interpolation helper
    static Color Lerp(Color c1, Color c2, float t)
    {
        t = Math.Max(0f, Math.Min(1f, t));
        int r = (int)(c1.R + (c2.R - c1.R) * t);
        int g = (int)(c1.G + (c2.G - c1.G) * t);
        int b = (int)(c1.B + (c2.B - c1.B) * t);
        return Color.FromArgb(r, g, b);
    }

    static void Main()
    {
        int cardW = 476;
        int totalH = 318;
        Color topColor = Color.FromArgb(43, 16, 127);
        Color bottomColor = Color.FromArgb(255, 38, 128);

        // Calculate colors at boundaries:
        // Y = 0: topColor
        // Y = 30: color at 30 / 318
        // Y = 222: color at 222 / 318
        // Y = 318: bottomColor
        Color c30 = Lerp(topColor, bottomColor, 30f / totalH);
        Color c222 = Lerp(topColor, bottomColor, 222f / totalH);

        Console.WriteLine("TopColor: " + topColor);
        Console.WriteLine("At Y=30: " + c30);
        Console.WriteLine("At Y=222: " + c222);
        Console.WriteLine("BottomColor: " + bottomColor);

        int formW = 558;
        int formH = 388;
        using (Bitmap bmp = new Bitmap(formW, formH))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.Clear(Color.FromArgb(30, 30, 30));

            // Title
            using (Font f = new Font("Segoe UI", 9.75f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(142, 142, 147)))
            {
                g.DrawString("Main • Moon Over a Thousand Peaks", f, b, 37, 18);
            }

            int cardX = 37, cardY = 42;

            // 1. PanelTopBar (0..30)
            // Gradient from topColor to c30
            using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(cardX, cardY, cardW, 30), topColor, c30, LinearGradientMode.Vertical))
            {
                g.FillRectangle(lgb, cardX, cardY, cardW, 30);
            }
            // Frosted dark glass overlay (làm mờ)
            using (SolidBrush darkOverlay = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillRectangle(darkOverlay, cardX, cardY, cardW, 30);
            }
            // Delicate divider line
            using (Pen sep = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
            {
                g.DrawLine(sep, cardX, cardY + 29, cardX + cardW, cardY + 29);
            }

            // Draw search pill box
            int pillX = cardX + 58, pillY = cardY + 5, pillW = 360, pillH = 20;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(pillX, pillY, pillH, pillH, 90, 180);
                path.AddArc(pillX + pillW - pillH, pillY, pillH, pillH, 270, 180);
                path.CloseFigure();
                using (SolidBrush pillBrush = new SolidBrush(Color.Black))
                {
                    g.FillPath(pillBrush, path);
                }
            }

            // 2. GradientPanel (30..222, height 192)
            using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(cardX, cardY + 30, cardW, 192), c30, c222, LinearGradientMode.Vertical))
            {
                g.FillRectangle(lgb, cardX, cardY + 30, cardW, 192);
            }

            // Draw Vinyl Disc
            int cx = cardX + cardW / 2;
            int cy = cardY + 30 + 88;
            int R = 72;
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                g.FillEllipse(shadow, cx - R + 2, cy - R + 5, 2 * R, 2 * R);
            using (SolidBrush vinyl = new SolidBrush(Color.FromArgb(18, 18, 18)))
                g.FillEllipse(vinyl, cx - R, cy - R, 2 * R, 2 * R);
            using (Pen rim = new Pen(Color.FromArgb(45, 45, 45), 1.5f))
                g.DrawEllipse(rim, cx - R, cy - R, 2 * R, 2 * R);
            using (Font tf = new Font("Segoe UI", 9f))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(240, 240, 240)))
            using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center })
            {
                g.DrawString("00:37 / 03:42", tf, tb, cx, cy + R + 6, sf);
            }

            // 3. PanelBottomControls (222..318, height 96)
            // Gradient from c222 to bottomColor
            using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(cardX, cardY + 222, cardW, 96), c222, bottomColor, LinearGradientMode.Vertical))
            {
                g.FillRectangle(lgb, cardX, cardY + 222, cardW, 96);
            }
            // Frosted dark glass overlay (làm mờ): soft gradient darkening from 55 to 130 alpha
            using (LinearGradientBrush darkOverlay = new LinearGradientBrush(
                new Rectangle(cardX, cardY + 222, cardW, 96),
                Color.FromArgb(60, 0, 0, 0),
                Color.FromArgb(135, 0, 0, 0),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(darkOverlay, cardX, cardY + 222, cardW, 96);
            }
            // Delicate divider line
            using (Pen sep = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
            {
                g.DrawLine(sep, cardX, cardY + 222, cardX + cardW, cardY + 222);
            }

            // Draw trackbar
            using (Pen penRed = new Pen(Color.FromArgb(235, 15, 15), 3))
            using (Pen penWhite = new Pen(Color.White, 3))
            {
                int tbY = cardY + 222 + 25;
                g.DrawLine(penRed, cardX + 28, tbY, cardX + 150, tbY);
                g.DrawLine(penWhite, cardX + 150, tbY, cardX + cardW - 28, tbY);
                using (SolidBrush dot = new SolidBrush(Color.White))
                    g.FillEllipse(dot, cardX + 150 - 4, tbY - 4, 8, 8);
            }

            // Draw buttons placeholders
            using (SolidBrush whiteB = new SolidBrush(Color.White))
            using (SolidBrush purpleB = new SolidBrush(Color.FromArgb(168, 85, 247)))
            {
                // Volume
                g.FillPolygon(whiteB, new Point[] {
                    new Point(cardX + 130, cardY + 222 + 55),
                    new Point(cardX + 138, cardY + 222 + 50),
                    new Point(cardX + 138, cardY + 222 + 65)
                });
                // Play/Pause circle
                using (SolidBrush circleB = new SolidBrush(Color.FromArgb(217, 217, 217)))
                    g.FillEllipse(circleB, cardX + 223, cardY + 222 + 44, 30, 30);
                // Loop
                g.DrawArc(new Pen(purpleB, 2), cardX + 324, cardY + 222 + 48, 20, 16, 0, 300);
            }

            string outPath = @"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\test_seamless_card.png";
            bmp.Save(outPath, ImageFormat.Png);
            Console.WriteLine("Saved: " + outPath);
        }
    }
}
