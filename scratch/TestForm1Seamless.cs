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

            // Reflection to access controls
            var fCard = typeof(Form1).GetField("panelPlayerCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fTop = typeof(Form1).GetField("panelTopBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fGrad = typeof(Form1).GetField("gradientPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fBot = typeof(Form1).GetField("panelBottomControls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Panel card = (Panel)fCard.GetValue(form);
            Panel topBar = (Panel)fTop.GetValue(form);
            KahoiiMusic.Controls.GradientPanel grad = (KahoiiMusic.Controls.GradientPanel)fGrad.GetValue(form);
            Panel botBar = (Panel)fBot.GetValue(form);

            // Set all 3 panels to Transparent
            topBar.BackColor = Color.Transparent;
            grad.BackColor = Color.Transparent;
            botBar.BackColor = Color.Transparent;

            // Paint card seamlessly
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                Color topColor = grad.TopColor;
                Color bottomColor = grad.BottomColor;

                Color darkTop = Color.FromArgb(
                    Math.Max(16, (int)(topColor.R * 0.32f)),
                    Math.Max(14, (int)(topColor.G * 0.32f)),
                    Math.Max(26, (int)(topColor.B * 0.32f)));

                Color darkBottom = Color.FromArgb(
                    Math.Max(28, (int)(bottomColor.R * 0.30f)),
                    Math.Max(14, (int)(bottomColor.G * 0.30f)),
                    Math.Max(22, (int)(bottomColor.B * 0.30f)));

                using (LinearGradientBrush lgb = new LinearGradientBrush(card.ClientRectangle, Color.Black, Color.Black, LinearGradientMode.Vertical))
                {
                    ColorBlend cb = new ColorBlend(4);
                    cb.Colors = new Color[] { darkTop, topColor, bottomColor, darkBottom };
                    cb.Positions = new float[] { 0.0f, 0.15f, 0.65f, 1.0f };
                    lgb.InterpolationColors = cb;
                    e.Graphics.FillRectangle(lgb, card.ClientRectangle);
                }
            };

            // Set test state
            var tbTitle = typeof(Form1).GetField("lblTitle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbTitle != null) ((Label)tbTitle.GetValue(form)).Text = "Main • Moon Over a Thousand Peaks";

            var tbSearch = typeof(Form1).GetField("searchPillBox", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbSearch != null) ((KahoiiMusic.Controls.SearchPillBox)tbSearch.GetValue(form)).Text = "https://www.youtube.com/watch?v=JKBHEKxndkE";

            var tbTrack = typeof(Form1).GetField("musicTrackBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbTrack != null) ((KahoiiMusic.Controls.MusicTrackBar)tbTrack.GetValue(form)).Value = 0.16f;

            var tbPlay = typeof(Form1).GetField("btnPlayPause", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbPlay != null) ((KahoiiMusic.Controls.PlayPauseButton)tbPlay.GetValue(form)).IsPlaying = true;

            var tbLoop = typeof(Form1).GetField("btnLoop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tbLoop != null) ((KahoiiMusic.Controls.LoopButton)tbLoop.GetValue(form)).IsLooping = true;

            form.Refresh();
            Application.DoEvents();

            using (Bitmap bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                string path = @"C:\Users\quocd\.gemini\antigravity-ide\brain\1ac08cac-a542-4fd5-9a87-7c71fe559213\scratch\test_form1_seamless.png";
                bmp.Save(path, ImageFormat.Png);
                Console.WriteLine("Saved: " + path);
            }

            form.Close();
        }
    }
}
