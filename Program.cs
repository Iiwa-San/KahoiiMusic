using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace KahoiiMusic
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
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
                    string[] possiblePaths = new string[]
                    {
                        Path.Combine(Path.GetTempPath(), "KahoiiMusic"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KahoiiMusic")
                    };

                    foreach (string cache in possiblePaths)
                    {
                        if (Directory.Exists(cache))
                        {
                            foreach (string file in Directory.GetFiles(cache))
                            {
                                if (!file.EndsWith("yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    for (int retry = 0; retry < 3; retry++)
                                    {
                                        try { File.Delete(file); break; } catch { System.Threading.Thread.Sleep(25); }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            };

            Application.ApplicationExit += (s, e) => cleanTempAudio();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => cleanTempAudio();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string startUrl = (args != null && args.Length > 0) ? args[0] : null;
            Application.Run(new Form1(startUrl));
        }
    }
}
