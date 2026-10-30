using System;
using System.IO;

namespace KahoiiMusic.Services
{
    public static class AppPaths
    {
        public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

        // Permanent Saved Folder
        public static string SavedDir => Path.Combine(BaseDirectory, "Saved");
        public static string SavedAudioDir => Path.Combine(SavedDir, "audio");
        public static string SavedThumbDir => Path.Combine(SavedDir, "thumb");
        public static string SavedJsonFile => Path.Combine(SavedDir, "saved.json");

        // Application Temporary Cache Folder
        public static string TempDir => Path.Combine(BaseDirectory, "KahoiiTemp");
        public static string TempAudioDir => Path.Combine(TempDir, "audio");
        public static string TempThumbDir => Path.Combine(TempDir, "thumb");

        public static void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(SavedDir)) Directory.CreateDirectory(SavedDir);
                if (!Directory.Exists(SavedAudioDir)) Directory.CreateDirectory(SavedAudioDir);
                if (!Directory.Exists(SavedThumbDir)) Directory.CreateDirectory(SavedThumbDir);

                if (!Directory.Exists(TempDir)) Directory.CreateDirectory(TempDir);
                if (!Directory.Exists(TempAudioDir)) Directory.CreateDirectory(TempAudioDir);
                if (!Directory.Exists(TempThumbDir)) Directory.CreateDirectory(TempThumbDir);
            }
            catch { }
        }
    }
}

