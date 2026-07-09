using System.ComponentModel.DataAnnotations;

namespace Heardit.Options
{
    public class SpotifyOptions
    {
        public const string SectionName = "Spotify";

        [Required]
        public string ClientId { get; set; } = string.Empty;

        [Required]
        public string ClientSecret { get; set; } = string.Empty;
    }
}
