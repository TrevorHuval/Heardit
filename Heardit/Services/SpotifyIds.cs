using System.Text.RegularExpressions;

namespace Heardit.Services
{
    /// <summary>The shape of a Spotify track id: 22 base62 characters. Checked before an id reaches the
    /// database, the Spotify API, a log line or a page.</summary>
    public static partial class SpotifyIds
    {
        public static bool IsTrackId(string? value) => value != null && TrackId().IsMatch(value);

        [GeneratedRegex("^[A-Za-z0-9]{22}$")]
        private static partial Regex TrackId();
    }
}
