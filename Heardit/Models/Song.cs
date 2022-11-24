using SpotifyAPI.Web;
using System.ComponentModel.DataAnnotations;

namespace Heardit.Models
{
    public class Song
    {
        [Key]
        public string Id { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? AlbumArt { get; set; }

        public Song() { }

        public Song(string songId, string title, string artist, string album, string albumArt)
        {
            Id = songId;
            Title = title;
            Artist = artist;
            Album = album;
            AlbumArt = albumArt;
        }
    }
}
