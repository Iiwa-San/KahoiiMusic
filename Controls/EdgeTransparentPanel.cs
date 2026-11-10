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
                    Point screenPt;
                    if (m.LParam != IntPtr.Zero)
                    {
                        int x = unchecked((short)(m.LParam.ToInt64() & 0xFFFF));
                        int y = unchecked((short)((m.LParam.ToInt64() >> 16) & 0xFFFF));
                        screenPt = new Point(x, y);
                    }
                    else
                    {
                        screenPt = Cursor.Position;
                    }
                    Point pt = parent.PointToClient(screenPt);

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

