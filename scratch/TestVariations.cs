using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

class Program
{
    static void Main()
    {
        float[] factors = new float[] { 0.25f, 0.32f, 0.40f };
        for (int i = 0; i < factors.Length; i++)
        {
            float f = factors[i];
            RenderVariation(f, string.Format(@"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\variation_factor_{0}.png", (int)(f * 100)));
        }
        Console.WriteLine("Rendered variations!");
    }

    static void RenderVariation(float factor, string outPath)
    {
        int formW = 558, formH = 388;
        int cardX = 37, cardY = 42, cardW = 476, cardH = 318;
        Color topColor = Color.FromArgb(43, 16, 127);
        Color bottomColor = Color.FromArgb(255, 38, 128);

        Color darkTop = Color.FromArgb(
            Math.Max(12, (int)(topColor.R * factor)),
            Math.Max(10, (int)(topColor.G * factor)),
            Math.Max(25, (int)(topColor.B * factor)));

        Color darkBottom = Color.FromArgb(
            Math.Max(20, (int)(bottomColor.R * factor)),
            Math.Max(10, (int)(bottomColor.G * factor)),
            Math.Max(15, (int)(bottomColor.B * factor)));

        using (Bitmap bmp = new Bitmap(formW, formH))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.Clear(Color.FromArgb(30, 30, 30));

            // Window title
            using (Font font = new Font("Segoe UI", 9.75f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(142, 142, 147)))
            {
                g.DrawString("Main • Moon Over a Thousand Peaks", font, b, 37, 18);
            }

            // Top Bar (0..30)
            using (SolidBrush b = new SolidBrush(darkTop))
            {
                g.FillRectangle(b, cardX, cardY, cardW, 30);
            }
            using (Pen sep = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
            {
                g.DrawLine(sep, cardX, cardY + 29, cardX + cardW, cardY + 29);
            }

            // Search pill box
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

            // Middle GradientPanel (30..222)
            using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(cardX, cardY + 30, cardW, 192), topColor, bottomColor, LinearGradientMode.Vertical))
            {
                g.FillRectangle(lgb, cardX, cardY + 30, cardW, 192);
            }

            // Vinyl disc
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

            // Bottom Controls (222..318)
            Color darkBottom2 = Color.FromArgb(
                (int)(darkBottom.R * 0.75f),
                (int)(darkBottom.G * 0.75f),
                (int)(darkBottom.B * 0.75f));

            using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(cardX, cardY + 222, cardW, 96), darkBottom, darkBottom2, LinearGradientMode.Vertical))
            {
                g.FillRectangle(lgb, cardX, cardY + 222, cardW, 96);
            }
            using (Pen sep = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
            {
                g.DrawLine(sep, cardX, cardY + 222, cardX + cardW, cardY + 222);
            }

            // Trackbar
            using (Pen penRed = new Pen(Color.FromArgb(235, 15, 15), 3))
            using (Pen penWhite = new Pen(Color.White, 3))
            {
                int tbY = cardY + 222 + 25;
                g.DrawLine(penRed, cardX + 28, tbY, cardX + 150, tbY);
                g.DrawLine(penWhite, cardX + 150, tbY, cardX + cardW - 28, tbY);
                using (SolidBrush dot = new SolidBrush(Color.White))
                    g.FillEllipse(dot, cardX + 150 - 4, tbY - 4, 8, 8);
            }

            // Controls
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

            bmp.Save(outPath, ImageFormat.Png);
        }
    }
}
