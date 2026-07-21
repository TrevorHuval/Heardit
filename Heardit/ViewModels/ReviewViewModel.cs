using Heardit.Models;
using Heardit.Services;

namespace Heardit.ViewModels
{
    public class ReviewViewModel
    {
        public required Review Review { get; set; }

        public int LikeCount { get; set; }

        public bool LikedByMe { get; set; }

        /// <summary>Pairs a review with its row from a batched <see cref="IReviewService.GetLikeStatsAsync"/> lookup.</summary>
        public static ReviewViewModel For(Review review, IReadOnlyDictionary<string, ReviewLikeStats> likeStats)
        {
            likeStats.TryGetValue(review.ReviewId, out var stats);
            return new ReviewViewModel
            {
                Review = review,
                LikeCount = stats?.Count ?? 0,
                LikedByMe = stats?.LikedByMe ?? false
            };
        }
    }
}
