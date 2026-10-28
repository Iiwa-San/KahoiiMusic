using System;

namespace KahoiiMusic.Models
{
    public class PlaylistItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public int DurationSeconds { get; set; }

        public string Url
        {
            get { return "https://www.youtube.com/watch?v=" + Id; }
        }

        public string FormattedDuration
        {
            get
            {
                if (DurationSeconds <= 0) return "--:--";
                TimeSpan t = TimeSpan.FromSeconds(DurationSeconds);
                return t.TotalHours >= 1
                    ? string.Format("{0:h\\:mm\\:ss}", t)
                    : string.Format("{0:mm\\:ss}", t);
            }
        }
    }
}
