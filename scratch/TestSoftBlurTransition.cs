using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

class Program
{
    static Color Blend(Color c1, Color c2, float t)
    {
        t = Math.Max(0f, Math.Min(1f, t));
        int r = (int)(c1.R + (c2.R - c1.R) * t);
        int g = (int)(c1.G + (c2.G - c1.G) * t);
        int b = (int)(c1.B + (c2.B - c1.B) * t);
        return Color.FromArgb(r, g, b);
    }

    static void Main()
    {
        int formW = 558, formH = 388;
        int cardX = 37, cardY = 42, cardW = 476, totalH = 318;
        Color topColor = Color.FromArgb(43, 16, 127);
        Color bottomColor = Color.FromArgb(255, 38, 128);

        // Dark framing colors
        Color darkTop = Color.FromArgb(
            Math.Max(16, (int)(topColor.R * 0.32f)),
            Math.Max(14, (int)(topColor.G * 0.32f)),
            Math.Max(26, (int)(topColor.B * 0.32f)));

        Color darkBottom = Color.FromArgb(
            Math.Max(28, (int)(bottomColor.R * 0.30f)),
            Math.Max(14, (int)(bottomColor.G * 0.30f)),
            Math.Max(22, (int)(bottomColor.B * 0.30f)));

        // We want to test two approaches:
        // Approach 1: Smooth continuous blend across the entire 318px card
        // Approach 2: Soft blurred gradient transition at the junctions (feathers 25px across each seam)
        
        RenderApproach1(cardX, cardY, cardW, totalH, topColor, bottomColor, darkTop, darkBottom,
            @"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\soft_transition_app1.png");

        RenderApproach2(cardX, cardY, cardW, totalH, topColor, bottomColor, darkTop, darkBottom,
            @"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\soft_transition_app2.png");

        Console.WriteLine("Rendered both approaches!");
    }

    // Approach 1: The entire card background is ONE smooth gradient that starts dark, blooms vibrant in the middle, and softens dark at the bottom
    static void RenderApproach1(int cardX, int cardY, int cardW, int totalH, Color topColor, Color bottomColor, Color darkTop, Color darkBottom, string outPath)
    {
        using (Bitmap bmp = new Bitmap(558, 388))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.Clear(Color.FromArgb(30, 30, 30));

            // Title
            using (Font f = new Font("Segoe UI", 9.75f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(142, 142, 147)))
                g.DrawString("Main • Moon Over a Thousand Peaks", f, b, 37, 18);

            // Multi-stop gradient across the entire 318px card:
            // 0% -> darkTop (Y=0)
            // 15% -> topColor (Y=48)
            // 65% -> bottomColor (Y=206)
            // 100% -> darkBottom (Y=318)
            using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(cardX, cardY, cardW, totalH), Color.Black, Color.Black, LinearGradientMode.Vertical))
            {
                ColorBlend cb = new ColorBlend(4);
                cb.Colors = new Color[] { darkTop, topColor, bottomColor, darkBottom };
                cb.Positions = new float[] { 0.0f, 0.15f, 0.65f, 1.0f };
                lgb.InterpolationColors = cb;
                g.FillRectangle(lgb, cardX, cardY, cardW, totalH);
            }

            DrawCommonElements(g, cardX, cardY, cardW);
            bmp.Save(outPath, ImageFormat.Png);
        }
    }

    // Approach 2: Soft gradient feather at the seam
    static void RenderApproach2(int cardX, int cardY, int cardW, int totalH, Color topColor, Color bottomColor, Color darkTop, Color darkBottom, string outPath)
    {
        using (Bitmap bmp = new Bitmap(558, 388))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.Clear(Color.FromArgb(30, 30, 30));

            // Title
            using (Font f = new Font("Segoe UI", 9.75f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(142, 142, 147)))
                g.DrawString("Main • Moon Over a Thousand Peaks", f, b, 37, 18);

            // TopBar (0..30): darkTop
            using (SolidBrush b = new SolidBrush(darkTop))
                g.FillRectangle(b, cardX, cardY, cardW, 30);

            // GradientPanel (30..222): 
            // from topColor to bottomColor, but the first 15px blends from darkTop, and the last 30px blends to darkBottom!
            using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(cardX, cardY + 30, cardW, 192), Color.Black, Color.Black, LinearGradientMode.Vertical))
            {
                ColorBlend cb = new ColorBlend(4);
                cb.Colors = new Color[] { darkTop, topColor, bottomColor, darkBottom };
                cb.Positions = new float[] { 0.0f, 0.12f, 0.82f, 1.0f };
                lgb.InterpolationColors = cb;
                g.FillRectangle(lgb, cardX, cardY + 30, cardW, 192);
            }

            // BottomControls (222..318): darkBottom
            using (SolidBrush b = new SolidBrush(darkBottom))
                g.FillRectangle(b, cardX, cardY + 222, cardW, 96);

            DrawCommonElements(g, cardX, cardY, cardW);
            bmp.Save(outPath, ImageFormat.Png);
        }
    }

    static void DrawCommonElements(Graphics g, int cardX, int cardY, int cardW)
    {
        // Pill box
        int pillX = cardX + 58, pillY = cardY + 5, pillW = 360, pillH = 20;
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddArc(pillX, pillY, pillH, pillH, 90, 180);
            path.AddArc(pillX + pillW - pillH, pillY, pillH, pillH, 270, 180);
            path.CloseFigure();
            using (SolidBrush pillBrush = new SolidBrush(Color.Black))
                g.FillPath(pillBrush, path);
        }

        // Vinyl Disc
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

        // Buttons
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
    }
}
