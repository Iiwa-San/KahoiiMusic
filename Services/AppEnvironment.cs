using System;
using System.IO;

namespace KahoiiMusic
{
    public static class AppEnvironment
    {
        private static bool? _isDev;

        public static bool IsDev
        {
            get
            {
                if (!_isDev.HasValue)
                {
#if DEBUG
                    _isDev = true;
#else
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        _isDev = true;
                    }
                    else
                    {
                        try
                        {
                            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            if (baseDir.IndexOf(@"\bin\Debug", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                baseDir.IndexOf(@"\bin\Release", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                _isDev = true;
                            }
                            else
                            {
                                // Check if running inside source development tree
                                string current = baseDir;
                                bool foundDevMarker = false;
                                for (int i = 0; i < 4; i++)
                                {
                                    if (string.IsNullOrEmpty(current)) break;
                                    if (File.Exists(Path.Combine(current, "KahoiiMusic.csproj")) ||
                                        File.Exists(Path.Combine(current, "KahoiiMusic.sln")) ||
                                        Directory.Exists(Path.Combine(current, ".git")))
                                    {
                                        foundDevMarker = true;
                                        break;
                                    }
                                    current = Path.GetDirectoryName(current);
                                }
                                _isDev = foundDevMarker;
                            }
                        }
                        catch
                        {
                            _isDev = false;
                        }
                    }
#endif
                }
                return _isDev.Value;
            }
        }

        public static string AppDisplayName => IsDev ? "Kahoii Music [Dev]" : "Kahoii Music";

        public static string MutexId => IsDev
            ? @"Local\KahoiiMusic_Dev_SingleInstance_" + Environment.UserName
            : @"Local\KahoiiMusic_SingleInstance_" + Environment.UserName;

        public static string PipeName => IsDev
            ? "KahoiiMusic_Dev_Pipe_" + Environment.UserName
            : "KahoiiMusic_Pipe_" + Environment.UserName;
    }
}

