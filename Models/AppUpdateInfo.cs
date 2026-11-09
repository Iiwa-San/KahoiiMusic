using System;

namespace KahoiiMusic.Models
{
    public class AppUpdateInfo
    {
        public int VersionCode { get; set; }
        public string VersionName { get; set; }
        public string Changelog { get; set; }
        public string InstallerUrl { get; set; }
        public string ReleasePageUrl { get; set; }
        public bool ForceUpdate { get; set; }
    }

    public class DownloadProgressReport
    {
        public long BytesReceived { get; set; }
        public long TotalBytesToReceive { get; set; }
        public int ProgressPercentage { get; set; }
        public double DownloadSpeedBytesPerSec { get; set; }
    }
}
