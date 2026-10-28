using System;
using System.Drawing;

class Program
{
    static void Main()
    {
        string p = @"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\current_real_ui.png";
        using (Bitmap bmp = new Bitmap(p))
        {
            Console.WriteLine("Image size: " + bmp.Width + "x" + bmp.Height);
            // Form1 size is 550x380 (client size 550x380)
            // panelPlayerCard is at (37, 42), size (476, 318)
            // gradientPanel is at (0, 30), size (476, 192) relative to card -> (37, 72) to (513, 264)
            // panelBottomControls is at (0, 222), size (476, 96) relative to card -> (37, 264) to (513, 360)
            
            // Let's check pixels at X = 35, 36, 37, 38 for Y from 260 to 268
            for (int y = 260; y <= 268; y++)
            {
                Console.Write(string.Format("Y={0}: ", y));
                for (int x = 34; x <= 40; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.R == 30 && c.G == 30 && c.B == 30)
                        Console.Write("BG ");
                    else if (c.R == 255 && c.G == 38 && c.B == 128)
                        Console.Write("PINK ");
                    else
                        Console.Write(string.Format("({0},{1},{2}) ", c.R, c.G, c.B));
                }
                Console.WriteLine();
            }
        }
    }
}
