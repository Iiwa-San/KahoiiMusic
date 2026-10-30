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
        /// Delegate to check whether background downloading or preloading is actively in progress.
        /// Protects physical RAM pages from being purged during active disk-heavy downloads.
        /// </summary>
        public static Func<bool> IsDownloadActive { get; set; }

        /// <summary>
        /// Lightweight background memory trim that does NOT freeze UI threads or cause page faults.
        /// Runs on ThreadPool to avoid any impact on UI animations.
        /// </summary>
        public static void TrimMemoryLight()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    GC.Collect(1, GCCollectionMode.Optimized, false);
                }
                catch { }
            });
        }

        /// <summary>
        /// Aggressive working set flush - trims unreferenced managed objects and flushes unused working set pages.
        /// If downloads are actively running, defers to lightweight trim to prevent disk contention and audio stuttering.
        /// Once downloads finish, this is executed to achieve minimal RAM footprint.
        /// </summary>
        public static void TrimMemoryAggressive()
        {
            try
            {
                // CRITICAL PROTECTION: While background downloads are writing to disk,
                // do NOT purge working set to prevent hard page faults on audio stream.
                if (IsDownloadActive != null && IsDownloadActive())
                {
                    TrimMemoryLight();
                    return;
                }

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

        /// <summary>
        /// Trims memory asynchronously after a specified delay.
        /// </summary>
        public static void TrimMemoryAsync(int delayMs = 500, bool aggressive = false)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (delayMs > 0)
                {
                    Thread.Sleep(delayMs);
                }
                if (aggressive)
                {
                    TrimMemoryAggressive();
                }
                else
                {
                    TrimMemoryLight();
                }
            });
        }

        public static void TrimMemory(bool aggressive = false)
        {
            if (aggressive)
            {
                TrimMemoryAggressive();
            }
            else
            {
                TrimMemoryLight();
            }
        }
    }
}
