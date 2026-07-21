namespace Heardit.ViewModels
{
    /// <summary>A single track in the Home / Search feed, with its aggregate review stats.</summary>
    public class FeedItemViewModel
    {
        public required string Id { get; set; }

        public required string Name { get; set; }

        public string Artists { get; set; } = string.Empty;

        /// <summary>Average rating, or null when the track has no reviews yet.</summary>
        public decimal? AverageRating { get; set; }

        public int ReviewCount { get; set; }

        /// <summary>Whether this track is already in the reader's listen-later queue.</summary>
        public bool Saved { get; set; }
    }
}
