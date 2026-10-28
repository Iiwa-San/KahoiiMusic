using System;
using System.Drawing;
using System.Windows.Forms;
using KahoiiMusic;

class Program
{
    [STAThread]
    static void Main()
    {
        Form1 form = new Form1();
        // Use reflection to find gradientPanel
        var field = typeof(Form1).GetField("gradientPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var gp = field.GetValue(form) as KahoiiMusic.Controls.GradientPanel;
        Console.WriteLine("TopColor: " + gp.TopColor);
        Console.WriteLine("BottomColor: " + gp.BottomColor);
        
        var fieldTop = typeof(Form1).GetField("panelTopBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pt = fieldTop.GetValue(form) as Panel;
        Console.WriteLine("panelTopBar.BackColor: " + pt.BackColor);

        var fieldBottom = typeof(Form1).GetField("panelBottomControls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pb = fieldBottom.GetValue(form) as Panel;
        Console.WriteLine("panelBottomControls.BackColor: " + pb.BackColor);
    }
}
