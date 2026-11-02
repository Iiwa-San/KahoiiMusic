using System;
using System.Collections.Generic;

namespace KahoiiMusic.Models
{
    public class UserPlaylist
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PlaylistItem> Items { get; set; }

        public UserPlaylist()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "Danh sách phát mới";
            CreatedAt = DateTime.Now;
            Items = new List<PlaylistItem>();
        }

        public UserPlaylist(string name, List<PlaylistItem> items = null)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = string.IsNullOrWhiteSpace(name) ? "Danh sách phát mới" : name.Trim();
            CreatedAt = DateTime.Now;
            Items = items != null ? new List<PlaylistItem>(items) : new List<PlaylistItem>();
        }
    }
}

