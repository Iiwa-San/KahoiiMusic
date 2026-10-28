using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

class Program
{
    static void Main()
    {
        int W = 558;
        int H = 388;
        using (Bitmap bmp = new Bitmap(W, H))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(30, 30, 30));

            // Draw Title "Main • Moon Over a Thousand Peaks"
            using (Font f = new Font("Segoe UI", 9.75f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(142, 142, 147)))
            {
                g.DrawString("Main • Moon Over a Thousand Peaks", f, b, 37, 18);
            }

            int cardX = 37, cardY = 42, cardW = 476, cardH = 318;
            Color topColor = Color.FromArgb(43, 16, 127);
            Color bottomColor = Color.FromArgb(255, 38, 128);

            // 1. Full continuous gradient across the entire card
            using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(cardX, cardY, cardW, cardH), topColor, bottomColor, LinearGradientMode.Vertical))
            {
                g.FillRectangle(lgb, cardX, cardY, cardW, cardH);
            }

            // 2. Top Bar dark glass tint (làm mờ thanh tìm kiếm)
            using (SolidBrush topTint = new SolidBrush(Color.FromArgb(85, 0, 0, 0)))
            {
                g.FillRectangle(topTint, cardX, cardY, cardW, 30);
            }
            using (Pen sep = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
            {
                g.DrawLine(sep, cardX, cardY + 30, cardX + cardW, cardY + 30);
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

            // 3. Bottom Controls dark glass tint (làm mờ cụm nút điều khiển)
            using (LinearGradientBrush botTint = new LinearGradientBrush(
                new Rectangle(cardX, cardY + 222, cardW, 96),
                Color.FromArgb(50, 0, 0, 0),
                Color.FromArgb(120, 0, 0, 0),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(botTint, cardX, cardY + 222, cardW, 96);
            }
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

            // Draw Vinyl Disc in center
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

            bmp.Save(@"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\preview_seamless_tinted.png", ImageFormat.Png);
        }
        Console.WriteLine("Saved preview_seamless_tinted.png");
    }
}
