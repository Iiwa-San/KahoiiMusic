using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using KahoiiMusic.Models;

namespace KahoiiMusic.Services
{
    public class AppUpdateService
    {
        private static readonly Lazy<AppUpdateService> _instance = new Lazy<AppUpdateService>(() => new AppUpdateService());
        public static AppUpdateService Instance => _instance.Value;

        public const string PrimaryVersionUrl = "https://raw.githubusercontent.com/Iiwa-San/KahoiiMusic/main/version.json";
        public const string DefaultReleasePageUrl = "https://github.com/Iiwa-San/KahoiiMusic/releases";

        private readonly HttpClient _httpClient;
        private readonly JavaScriptSerializer _serializer;

        // Current Assembly version
        public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version;
        // Build number is the integer version code (e.g. 1.0.17.0 -> 17)
        public static int CurrentVersionCode => CurrentVersion.Build > 0 ? CurrentVersion.Build : 17;
        public static string CurrentVersionName => $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{Math.Max(0, CurrentVersion.Build)}";

        public static bool IsDevEnvironment
        {
            get
            {
#if DEBUG
                return true;
#else
                if (Debugger.IsAttached) return true;

                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    if (baseDir.IndexOf(@"\bin\Debug", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        baseDir.IndexOf(@"\bin\Release", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }

                    string current = baseDir;
                    for (int i = 0; i < 4; i++)
                    {
                        if (string.IsNullOrEmpty(current)) break;
                        if (File.Exists(Path.Combine(current, "KahoiiMusic.csproj")) ||
                            File.Exists(Path.Combine(current, "KahoiiMusic.sln")) ||
                            Directory.Exists(Path.Combine(current, ".git")))
                        {
                            return true;
                        }
                        current = Path.GetDirectoryName(current);
                    }
                }
                catch { }

                return false;
#endif
            }
        }

        private AppUpdateService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"KahoiiMusic-Desktop/{CurrentVersionName}");
            _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        }

        public async Task<AppUpdateInfo> CheckForUpdateAsync()
        {
            try
            {
                string urlWithBuster = $"{PrimaryVersionUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                using (var req = new HttpRequestMessage(HttpMethod.Get, urlWithBuster))
                {
                    req.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                    req.Headers.Add("Pragma", "no-cache");

                    using (var res = await _httpClient.SendAsync(req).ConfigureAwait(false))
                    {
                        if (!res.IsSuccessStatusCode) return null;
                        string json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(json)) return null;

                        var dict = _serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);
                        if (dict == null) return null;

                        var info = new AppUpdateInfo
                        {
                            VersionCode = dict.ContainsKey("versionCode") ? Convert.ToInt32(dict["versionCode"]) : 0,
                            VersionName = dict.ContainsKey("versionName") ? dict["versionName"]?.ToString() : string.Empty,
                            Changelog = dict.ContainsKey("changelog") ? dict["changelog"]?.ToString() : string.Empty,
                            InstallerUrl = dict.ContainsKey("installerUrl") ? dict["installerUrl"]?.ToString() : string.Empty,
                            ReleasePageUrl = dict.ContainsKey("releasePageUrl") ? dict["releasePageUrl"]?.ToString() : DefaultReleasePageUrl,
                            ForceUpdate = dict.ContainsKey("forceUpdate") && Convert.ToBoolean(dict["forceUpdate"])
                        };

                        if (IsUpdateAvailable(info))
                        {
                            return info;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppUpdateService] Check update error: {ex.Message}");
            }
            return null;
        }

        public bool IsUpdateAvailable(AppUpdateInfo remoteInfo)
        {
            if (remoteInfo == null) return false;

            // 1. Check by VersionCode if both are positive
            if (remoteInfo.VersionCode > 0 && CurrentVersionCode > 0)
            {
                return remoteInfo.VersionCode > CurrentVersionCode;
            }

            // 2. Check by VersionName
            if (!string.IsNullOrWhiteSpace(remoteInfo.VersionName) && Version.TryParse(remoteInfo.VersionName, out Version remoteVer))
            {
                Version currentNorm = new Version(CurrentVersion.Major, CurrentVersion.Minor, Math.Max(0, CurrentVersion.Build));
                Version remoteNorm = new Version(remoteVer.Major, remoteVer.Minor, Math.Max(0, remoteVer.Build));
                return remoteNorm > currentNorm;
            }

            return false;
        }

        public async Task<string> DownloadInstallerAsync(string downloadUrl, IProgress<DownloadProgressReport> progress, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
                throw new ArgumentException("Download URL is empty.");

            string tempFile = Path.Combine(Path.GetTempPath(), $"KahoiiMusic_Setup_{DateTime.UtcNow.Ticks}.exe");

            using (var downloadClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                downloadClient.DefaultRequestHeaders.Add("User-Agent", $"KahoiiMusic-Desktop/{CurrentVersionName}");

                using (var response = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    long bytesReadTotal = 0;

                    using (var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        byte[] buffer = new byte[81920];
                        int read;
                        var stopwatch = Stopwatch.StartNew();
                        long lastReportTicks = 0;

                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                            bytesReadTotal += read;

                            // Throttle progress updates to ~15-20 fps (every 50ms) to keep UI ultra responsive
                            if (stopwatch.ElapsedMilliseconds - lastReportTicks >= 50 || bytesReadTotal == totalBytes)
                            {
                                lastReportTicks = stopwatch.ElapsedMilliseconds;
                                int percent = totalBytes > 0 ? (int)((bytesReadTotal * 100) / totalBytes) : -1;
                                double speed = bytesReadTotal / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);

                                progress?.Report(new DownloadProgressReport
                                {
                                    BytesReceived = bytesReadTotal,
                                    TotalBytesToReceive = totalBytes,
                                    ProgressPercentage = percent,
                                    DownloadSpeedBytesPerSec = speed
                                });
                            }
                        }
                    }
                }
            }

            return tempFile;
        }

        public void LaunchInstallerAndExit(string installerPath)
        {
            if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath)) return;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppUpdateService] Failed to launch installer: {ex.Message}");
            }

            // Gracefully terminate current process so Inno Setup can overwrite files
            try
            {
                Application.Exit();
            }
            catch { }

            try
            {
                Environment.Exit(0);
            }
            catch { }
        }
    }
}

