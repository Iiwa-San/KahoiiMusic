using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace KahoiiMusic.Services
{
    public static class MemoryOptimizer
    {
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        /// <summary>
        /// Trims unreferenced managed objects and flushes unused working set pages to minimize RAM footprint.
        /// </summary>
        public static void TrimMemoryAsync(int delayMs = 500)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (delayMs > 0)
                {
                    Thread.Sleep(delayMs);
                }
                TrimMemory();
            });
        }

        public static void TrimMemory()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Forced, false);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, false);

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    IntPtr handle = Process.GetCurrentProcess().Handle;
                    EmptyWorkingSet(handle);
                    SetProcessWorkingSetSize(handle, -1, -1);
                }
            }
            catch { }
        }
    }
}
