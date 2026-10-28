using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using KahoiiMusic;

class Program
{
    [STAThread]
    static void Main()
    {
        using (Form1 form = new Form1())
        {
            form.Show();
            Application.DoEvents();

            using (Bitmap bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                bmp.Save(@"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\current_real_ui.png", ImageFormat.Png);
            }
            form.Close();
        }
        Console.WriteLine("Captured current_real_ui.png");
    }
}
