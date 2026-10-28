using System;
using System.Drawing;
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

            // Set some dummy playing state for visual verification
            var tbTitle = typeof(Form1).GetField("lblTitle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbTitle != null)
            {
                Label lbl = (Label)tbTitle.GetValue(form);
                lbl.Text = "Main • Moon Over a Thousand Peaks";
            }

            var tbSearch = typeof(Form1).GetField("searchPillBox", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbSearch != null)
            {
                var pill = (KahoiiMusic.Controls.SearchPillBox)tbSearch.GetValue(form);
                pill.Text = "https://www.youtube.com/watch?v=JKBHEKxndkE";
            }

            var tbArtwork = typeof(Form1).GetField("artworkBox", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbArtwork != null)
            {
                var art = (KahoiiMusic.Controls.ArtworkBox)tbArtwork.GetValue(form);
                art.TimeText = "00:37 / 03:42";
                art.IsSpinning = true;
            }

            var tbTrack = typeof(Form1).GetField("musicTrackBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbTrack != null)
            {
                var mt = (KahoiiMusic.Controls.MusicTrackBar)tbTrack.GetValue(form);
                mt.Value = 0.16f;
            }

            var tbPlay = typeof(Form1).GetField("btnPlayPause", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbPlay != null)
            {
                var btn = (KahoiiMusic.Controls.PlayPauseButton)tbPlay.GetValue(form);
                btn.IsPlaying = true;
            }

            var tbLoop = typeof(Form1).GetField("btnLoop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbLoop != null)
            {
                var loop = (KahoiiMusic.Controls.LoopButton)tbLoop.GetValue(form);
                loop.IsLooping = true;
            }

            Application.DoEvents();

            using (Bitmap bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                string path = @"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\playing_ui_verified.png";
                bmp.Save(path, ImageFormat.Png);
                Console.WriteLine("Saved: " + path);
            }

            form.Close();
        }
    }
}
