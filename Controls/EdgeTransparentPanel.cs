using System;
using System.Drawing;
using System.Windows.Forms;

namespace KahoiiMusic.Controls
{
    public class EdgeTransparentPanel : Panel
    {
        private const int WM_NCHITTEST = 0x84;
        private const int HTTRANSPARENT = -1;
        private const int ResizeBorder = 8;

        public EdgeTransparentPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                Form parent = FindForm();
                if (parent != null && parent.WindowState == FormWindowState.Normal)
                {
                    int x = (short)(m.LParam.ToInt32() & 0xFFFF);
                    int y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
                    Point pt = parent.PointToClient(new Point(x, y));

                    int w = parent.ClientSize.Width;
                    int h = parent.ClientSize.Height;

                    if (pt.X <= ResizeBorder || pt.X >= w - ResizeBorder ||
                        pt.Y <= ResizeBorder || pt.Y >= h - ResizeBorder)
                    {
                        m.Result = (IntPtr)HTTRANSPARENT;
                        return;
                    }
                }
            }
            base.WndProc(ref m);
        }
    }
}
