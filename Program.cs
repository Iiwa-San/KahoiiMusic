using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KahoiiMusic
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private const int SW_RESTORE = 9;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string startUrl = (args != null && args.Length > 0) ? args[0] : null;

            // Single instance lock per user session
            string mutexId = @"Local\KahoiiMusic_SingleInstance_" + Environment.UserName;
            bool createdNew = false;
            Mutex mutex = null;
            try
            {
                mutex = new Mutex(true, mutexId, out createdNew);
            }
            catch (AbandonedMutexException)
            {
                createdNew = true;
            }
            catch
            {
                createdNew = true;
            }

            if (!createdNew)
            {
                // Another instance is already running! Signal it to wake up and exit immediately.
                SignalRunningInstance(startUrl);
                return;
            }

            using (mutex)
            {
                // Auto-resolve dependent dlls from application directory or embedded resources
                AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
                {
                    try
                    {
                        string assemblyName = new AssemblyName(resolveArgs.Name).Name;

                        // 1. Try file on disk next to executable
                        string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        if (!string.IsNullOrEmpty(appDir))
                        {
                            string targetDll = Path.Combine(appDir, assemblyName + ".dll");
                            if (File.Exists(targetDll))
                            {
                                return Assembly.LoadFrom(targetDll);
                            }
                        }

                        // 2. Try embedded resource
                        Assembly executingAsm = Assembly.GetExecutingAssembly();
                        string suffix = "." + assemblyName + ".dll";
                        foreach (string resName in executingAsm.GetManifestResourceNames())
                        {
                            if (resName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                                resName.Equals("KahoiiMusic.Resources." + assemblyName + ".dll", StringComparison.OrdinalIgnoreCase))
                            {
                                using (Stream stream = executingAsm.GetManifestResourceStream(resName))
                                {
                                    if (stream != null)
                                    {
                                        byte[] bytes = new byte[stream.Length];
                                        stream.Read(bytes, 0, bytes.Length);
                                        return Assembly.Load(bytes);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    return null;
                };

                Action cleanTempAudio = () =>
                {
                    try
                    {
                        KahoiiMusic.Services.HistoryService.CleanNonHistoryTempFiles();
                    }
                    catch { }
                };

                Application.ApplicationExit += (s, e) => cleanTempAudio();
                AppDomain.CurrentDomain.ProcessExit += (s, e) => cleanTempAudio();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                KahoiiMusic.Services.AppPaths.EnsureDirectoriesExist();
                Application.Run(new Form1(startUrl));
            }
        }

        private static void SignalRunningInstance(string url)
        {
            try
            {
                string pipeName = "KahoiiMusic_Pipe_" + Environment.UserName;
                using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
                {
                    client.Connect(1000);
                    using (var writer = new StreamWriter(client, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine(string.IsNullOrEmpty(url) ? "__ACTIVATE__" : url);
                        writer.Flush();
                    }
                }
            }
            catch
            {
                TryActivateExistingWindow();
            }
        }

        private static void TryActivateExistingWindow()
        {
            try
            {
                IntPtr hwnd = FindWindow(null, "Kahoii Music");
                if (hwnd == IntPtr.Zero)
                {
                    var current = System.Diagnostics.Process.GetCurrentProcess();
                    foreach (var p in System.Diagnostics.Process.GetProcessesByName(current.ProcessName))
                    {
                        if (p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero)
                        {
                            hwnd = p.MainWindowHandle;
                            break;
                        }
                    }
                }

                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                }
            }
            catch { }
        }
    }
}
