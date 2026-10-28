using System;
using System.Drawing;

class Program
{
    static void Main()
    {
        string p = @"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\.user_uploaded\media_1793169020210.png";
        using (Bitmap bmp = new Bitmap(p))
        {
            Console.WriteLine("Image size: " + bmp.Width + "x" + bmp.Height);
            // Scan left edge of the card (around x = 20..50, y = 200..300)
            for (int y = 200; y < 270; y += 5)
            {
                Color c = bmp.GetPixel(30, y);
                Console.WriteLine("y={0}: ({1},{2},{3})", y, c.R, c.G, c.B);
            }
        }
    }
}
