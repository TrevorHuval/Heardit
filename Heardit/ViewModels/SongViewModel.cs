using Heardit.Models;
using Heardit.Services;

namespace Heardit.ViewModels
{
    public class SongViewModel
    {
        public required Song Song { get; set; }

        /// <summary>Everyone else's reviews, one page at a time.</summary>
        public required PagedList<Review> Reviews { get; set; }

        /// <summary>Like counts for the reviews on this page, batched by the controller.</summary>
        public IReadOnlyDictionary<string, ReviewLikeStats> LikeStats { get; set; } =
            new Dictionary<string, ReviewLikeStats>();

        /// <summary>The signed-in user's own review, pulled out of the list so it can head the page.</summary>
        public Review? MyReview { get; set; }

        /// <summary>Average across every review of this song, not just the current page.</summary>
        public decimal AverageRating { get; set; }

        /// <summary>Total review count for this song, not just the current page.</summary>
        public int ReviewCount { get; set; }
    }
}
