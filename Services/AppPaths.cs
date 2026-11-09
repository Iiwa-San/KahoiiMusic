using System;
using System.IO;

namespace KahoiiMusic.Services
{
    public static class AppPaths
    {
        public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

        private static string _dataRoot;
        public static string DataRoot
        {
            get
            {
                if (_dataRoot == null)
                {
                    try
                    {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory).TrimEnd('\\', '/');
                        string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory).TrimEnd('\\', '/');

                        // If app is placed directly on Desktop, NEVER pollute Desktop with data/cache folders!
                        bool isDesktop = string.Equals(baseDir, desktop, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(baseDir, commonDesktop, StringComparison.OrdinalIgnoreCase);

                        // If installed into Program Files (read-only for standard non-elevated users)
                        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                        bool isProgramFiles = (!string.IsNullOrEmpty(pf) && baseDir.StartsWith(pf, StringComparison.OrdinalIgnoreCase)) ||
                                              (!string.IsNullOrEmpty(pf86) && baseDir.StartsWith(pf86, StringComparison.OrdinalIgnoreCase));

                        if (isDesktop || isProgramFiles)
                        {
                            _dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KahoiiMusic");
                        }
                        else
                        {
                            // Portable mode (inside a dedicated folder)
                            _dataRoot = baseDir;
                        }
                    }
                    catch
                    {
                        _dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KahoiiMusic");
                    }
                }
                return _dataRoot;
            }
        }

        // Permanent Saved Folder
        public static string SavedDir => Path.Combine(DataRoot, "Saved");
        public static string SavedAudioDir => Path.Combine(SavedDir, "audio");
        public static string SavedThumbDir => Path.Combine(SavedDir, "thumb");
        public static string SavedLyricsDir => Path.Combine(SavedDir, "lyrics");
        public static string SavedJsonFile => Path.Combine(SavedDir, "saved.json");
        public static string PlaylistsJsonFile => Path.Combine(SavedDir, "playlists.json");
        public static string FavoritesJsonFile => Path.Combine(SavedDir, "favorites.json");

        // Application Temporary Cache Folder
        public static string TempDir => Path.Combine(DataRoot, "KahoiiTemp");
        public static string TempAudioDir => Path.Combine(TempDir, "audio");
        public static string TempThumbDir => Path.Combine(TempDir, "thumb");
        public static string TempLyricsDir => Path.Combine(TempDir, "lyrics");

        public static void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(DataRoot)) Directory.CreateDirectory(DataRoot);
                if (!Directory.Exists(SavedDir)) Directory.CreateDirectory(SavedDir);
                if (!Directory.Exists(SavedAudioDir)) Directory.CreateDirectory(SavedAudioDir);
                if (!Directory.Exists(SavedThumbDir)) Directory.CreateDirectory(SavedThumbDir);
                if (!Directory.Exists(SavedLyricsDir)) Directory.CreateDirectory(SavedLyricsDir);

                if (!Directory.Exists(TempDir)) Directory.CreateDirectory(TempDir);
                if (!Directory.Exists(TempAudioDir)) Directory.CreateDirectory(TempAudioDir);
                if (!Directory.Exists(TempThumbDir)) Directory.CreateDirectory(TempThumbDir);
                if (!Directory.Exists(TempLyricsDir)) Directory.CreateDirectory(TempLyricsDir);
            }
            catch { }
        }
    }
}
