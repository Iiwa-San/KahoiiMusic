using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using KahoiiMusic.Models;

namespace KahoiiMusic.Services
{
    public class SavedTracksService
    {
        private static readonly Lazy<SavedTracksService> _instance = new Lazy<SavedTracksService>(() => new SavedTracksService());
        public static SavedTracksService Instance => _instance.Value;

        private readonly object _lock = new object();
        private List<PlaylistItem> _savedTracks = new List<PlaylistItem>();
        private bool _isLoaded = false;

        public event EventHandler SavedTracksChanged;

        private SavedTracksService()
        {
            AppPaths.EnsureDirectoriesExist();
            LoadFromFile();
        }

        public List<PlaylistItem> GetSavedTracks()
        {
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                return new List<PlaylistItem>(_savedTracks);
            }
        }

        public bool IsTrackSaved(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return false;
            lock (_lock)
            {
                if (!_isLoaded) LoadFromFile();
                bool inList = _savedTracks.Exists(t => string.Equals(t.Id, videoId, StringComparison.OrdinalIgnoreCase));
                if (!inList) return false;

                // Verify file still exists on disk
                return FindSavedAudioFile(videoId) != null;
            }
        }

        public string FindSavedAudioFile(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return null;
            try
            {
                string[] exts = new string[] { ".webm", ".opus", ".m4a", ".mp3", ".aac" };
                foreach (string ext in exts)
                {
                    string path = Path.Combine(AppPaths.SavedAudioDir, videoId + ext);
                    if (File.Exists(path) && new FileInfo(path).Length > 50000)
                    {
                        return path;
                    }
                }
            }
            catch { }
            return null;
        }

        public string FindSavedThumbFile(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return null;
            try
            {
                string path = Path.Combine(AppPaths.SavedThumbDir, videoId + ".jpg");
                if (File.Exists(path) && new FileInfo(path).Length > 500)
                {
                    return path;
                }
            }
            catch { }
            return null;
        }

        public void SaveTrackAsync(PlaylistItem item, Action<bool, string> callback = null)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                callback?.Invoke(false, "Thông tin bài hát không hợp lệ.");
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    AppPaths.EnsureDirectoriesExist();
                    string videoId = item.Id;

                    // 1. Check if audio already exists in KahoiiTemp/audio/
                    string existingTempAudio = null;
                    string[] exts = new string[] { ".webm", ".opus", ".m4a", ".mp3", ".aac" };
                    foreach (string ext in exts)
                    {
                        string p = Path.Combine(AppPaths.TempAudioDir, videoId + ext);
                        if (File.Exists(p) && new FileInfo(p).Length > 50000)
                        {
                            existingTempAudio = p;
                            break;
                        }
                    }

                    string targetExt = existingTempAudio != null ? Path.GetExtension(existingTempAudio) : ".webm";
                    string destinationAudio = Path.Combine(AppPaths.SavedAudioDir, videoId + targetExt);

                    if (existingTempAudio != null && File.Exists(existingTempAudio))
                    {
                        // Copy from KahoiiTemp directly (instant 0ms)
                        File.Copy(existingTempAudio, destinationAudio, true);
                    }
                    else
                    {
                        // Download directly to Saved/audio via yt-dlp
                        string ytdlpPath = AudioPlayerService.FindYtDlp();
                        if (string.IsNullOrEmpty(ytdlpPath))
                        {
                            callback?.Invoke(false, "Không tìm thấy công cụ tải yt-dlp.");
                            return;
                        }

                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = ytdlpPath;
                        psi.Arguments = string.Format("-f \"ba[ext=webm]/ba[ext=m4a]/ba/b[height<=480]\" -N 4 --no-playlist --no-progress --no-part --no-mtime --no-warnings --no-check-certificates -o \"{0}\\%(id)s.%(ext)s\" \"https://www.youtube.com/watch?v={1}\"", AppPaths.SavedAudioDir, videoId);
                        psi.CreateNoWindow = true;
                        psi.UseShellExecute = false;

                        using (Process proc = Process.Start(psi))
                        {
                            proc?.WaitForExit(60000);
                        }
                    }

                    // 2. Ensure thumbnail is in Saved/thumb/
                    string destThumb = Path.Combine(AppPaths.SavedThumbDir, videoId + ".jpg");
                    string srcTempThumb = Path.Combine(AppPaths.TempThumbDir, videoId + ".jpg");
                    if (File.Exists(srcTempThumb))
                    {
                        try { File.Copy(srcTempThumb, destThumb, true); } catch { }
                    }
                    else
                    {
                        try
                        {
                            using (WebClient wc = new WebClient())
                            {
                                byte[] bytes = wc.DownloadData("https://img.youtube.com/vi/" + videoId + "/default.jpg");
                                File.WriteAllBytes(destThumb, bytes);
                            }
                        }
                        catch { }
                    }

                    // 3. Verify audio exists in Saved/audio/
                    if (FindSavedAudioFile(videoId) == null)
                    {
                        callback?.Invoke(false, "Không thể lưu tệp âm thanh.");
                        return;
                    }

                    // 4. Update saved metadata
                    lock (_lock)
                    {
                        _savedTracks.RemoveAll(t => string.Equals(t.Id, videoId, StringComparison.OrdinalIgnoreCase));
                        _savedTracks.Insert(0, item);
                        SaveToFile();
                    }

                    SavedTracksChanged?.Invoke(this, EventArgs.Empty);
                    callback?.Invoke(true, "Đã lưu bài hát vào mục Đã tải!");
                }
                catch (Exception ex)
                {
                    callback?.Invoke(false, "Lỗi khi lưu bài: " + ex.Message);
                }
            });
        }

        public bool DeleteSavedTrack(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return false;

            lock (_lock)
            {
                int removed = _savedTracks.RemoveAll(t => string.Equals(t.Id, videoId, StringComparison.OrdinalIgnoreCase));
                SaveToFile();

                // Delete audio file in Saved/audio/
                string audioFile = FindSavedAudioFile(videoId);
                if (audioFile != null && File.Exists(audioFile))
                {
                    try { File.Delete(audioFile); } catch { }
                }

                // Delete thumb file in Saved/thumb/
                string thumbFile = Path.Combine(AppPaths.SavedThumbDir, videoId + ".jpg");
                if (File.Exists(thumbFile))
                {
                    try { File.Delete(thumbFile); } catch { }
                }

                SavedTracksChanged?.Invoke(this, EventArgs.Empty);
                return removed > 0;
            }
        }

        private void LoadFromFile()
        {
            lock (_lock)
            {
                _savedTracks.Clear();
                _isLoaded = true;

                if (!File.Exists(AppPaths.SavedJsonFile)) return;

                try
                {
                    string json = File.ReadAllText(AppPaths.SavedJsonFile, Encoding.UTF8);
                    MatchCollection matches = Regex.Matches(json, "\\{\\s*\"id\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*\"title\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"\\s*,\\s*\"duration\"\\s*:\\s*(\\d+)\\s*\\}");
                    foreach (Match m in matches)
                    {
                        string id = m.Groups[1].Value;
                        string title = m.Groups[2].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
                        int duration = 0;
                        int.TryParse(m.Groups[3].Value, out duration);

                        // Only include if physical audio file exists in Saved/audio
                        if (FindSavedAudioFile(id) != null)
                        {
                            var item = new PlaylistItem
                            {
                                Id = id,
                                Title = title,
                                DurationSeconds = duration
                            };
                            _savedTracks.Add(item);
                        }
                    }
                }
                catch { }
            }
        }

        private void SaveToFile()
        {
            try
            {
                AppPaths.EnsureDirectoriesExist();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < _savedTracks.Count; i++)
                {
                    PlaylistItem item = _savedTracks[i];
                    string escapedTitle = (item.Title ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
                    string comma = (i < _savedTracks.Count - 1) ? "," : "";
                    sb.AppendLine(string.Format("  {{ \"id\": \"{0}\", \"title\": \"{1}\", \"duration\": {2} }}{3}",
                        item.Id, escapedTitle, item.DurationSeconds, comma));
                }
                sb.AppendLine("]");
                File.WriteAllText(AppPaths.SavedJsonFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
