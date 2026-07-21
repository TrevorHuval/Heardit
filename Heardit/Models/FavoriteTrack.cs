using Heardit.Areas.Identity.Data;

namespace Heardit.Models
{
    /// <summary>
    /// One of the four tracks a listener pins to their profile. Position is 1–4 and unique per user,
    /// so the row order on the profile is the database's business rather than the view's.
    /// </summary>
    public class FavoriteTrack
    {
        public const int MaxPerUser = 4;

        public string UserId { get; set; } = null!;

        public HearditUser User { get; set; } = null!;

        public string SongId { get; set; } = null!;

        public Song Song { get; set; } = null!;

        /// <summary>1-based slot, at most <see cref="MaxPerUser"/>.</summary>
        public int Position { get; set; }
    }
}
