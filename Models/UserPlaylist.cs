using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace KahoiiMusic.Models
{
    public class UserPlaylist
    {
        private DateTime _createdAt = DateTime.Now;

        public string Id { get; set; }
        public string Name { get; set; }

        public object CreatedAt
        {
            get
            {
                try
                {
                    DateTime utc = _createdAt.Kind == DateTimeKind.Utc ? _createdAt : _createdAt.ToUniversalTime();
                    return new DateTimeOffset(utc).ToUnixTimeMilliseconds();
                }
                catch
                {
                    return 0L;
                }
            }
            set => ParseCreatedAt(value);
        }

        [ScriptIgnore]
        public DateTime CreatedAtDateTime => _createdAt;

        public List<PlaylistItem> Items { get; set; }

        public UserPlaylist()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "Danh sách phát mới";
            _createdAt = DateTime.Now;
            Items = new List<PlaylistItem>();
        }

        public UserPlaylist(string name, List<PlaylistItem> items = null)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = string.IsNullOrWhiteSpace(name) ? "Danh sách phát mới" : name.Trim();
            _createdAt = DateTime.Now;
            Items = items != null ? new List<PlaylistItem>(items) : new List<PlaylistItem>();
        }

        private void ParseCreatedAt(object val)
        {
            if (val == null) return;
            if (val is DateTime dt)
            {
                _createdAt = dt;
                return;
            }
            if (val is long l)
            {
                try
                {
                    if (l > 100000000000L)
                        _createdAt = DateTimeOffset.FromUnixTimeMilliseconds(l).LocalDateTime;
                    else
                        _createdAt = DateTimeOffset.FromUnixTimeSeconds(l).LocalDateTime;
                    return;
                }
                catch { }
            }
            if (val is int i)
            {
                try
                {
                    _createdAt = DateTimeOffset.FromUnixTimeSeconds(i).LocalDateTime;
                    return;
                }
                catch { }
            }
            if (val is double d)
            {
                try
                {
                    long dl = (long)d;
                    if (dl > 100000000000L)
                        _createdAt = DateTimeOffset.FromUnixTimeMilliseconds(dl).LocalDateTime;
                    else
                        _createdAt = DateTimeOffset.FromUnixTimeSeconds(dl).LocalDateTime;
                    return;
                }
                catch { }
            }

            string s = val.ToString()?.Trim();
            if (string.IsNullOrEmpty(s)) return;

            if (s.StartsWith("/Date(") && s.EndsWith(")/"))
            {
                string numStr = s.Substring(6, s.Length - 8).Trim();
                int plusIdx = numStr.IndexOf('+');
                if (plusIdx > 0) numStr = numStr.Substring(0, plusIdx);
                int minusIdx = numStr.IndexOf('-');
                if (minusIdx > 0) numStr = numStr.Substring(0, minusIdx);
                if (long.TryParse(numStr, out long ms))
                {
                    try
                    {
                        _createdAt = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
                        return;
                    }
                    catch { }
                }
            }

            if (long.TryParse(s, out long parsedLong))
            {
                try
                {
                    if (parsedLong > 100000000000L)
                        _createdAt = DateTimeOffset.FromUnixTimeMilliseconds(parsedLong).LocalDateTime;
                    else
                        _createdAt = DateTimeOffset.FromUnixTimeSeconds(parsedLong).LocalDateTime;
                    return;
                }
                catch { }
            }

            if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsedDt))
            {
                _createdAt = parsedDt;
            }
        }
    }
}
