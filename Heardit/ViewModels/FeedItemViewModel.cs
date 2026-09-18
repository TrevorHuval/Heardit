namespace Heardit.ViewModels
{
    /// <summary>A single track in the Home / Search feed, with its aggregate review stats.</summary>
    public class FeedItemViewModel
    {
        public required string Id { get; set; }

        public required string Name { get; set; }

        public string Artists { get; set; } = string.Empty;

        /// <summary>Album art for the card cover. The Spotify player only loads once the cover is clicked.</summary>
        public string? ImageUrl { get; set; }

        /// <summary>Average rating, or null when the track has no reviews yet.</summary>
        public decimal? AverageRating { get; set; }

        public int ReviewCount { get; set; }

        /// <summary>Whether this track is already in the reader's listen-later queue.</summary>
        public bool Saved { get; set; }
    }
}
