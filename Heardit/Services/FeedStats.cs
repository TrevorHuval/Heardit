using Heardit.ViewModels;

namespace Heardit.Services
{
    /// <summary>Fills feed items with their aggregate review stats in a single batched query.</summary>
    public static class FeedStats
    {
        public static async Task ApplyAsync(IReviewService reviewService, IReadOnlyList<FeedItemViewModel> feed)
        {
            if (feed.Count == 0)
            {
                return;
            }

            var stats = await reviewService.GetSongStatsAsync(feed.Select(f => f.Id));
            foreach (var item in feed)
            {
                if (stats.TryGetValue(item.Id, out var s))
                {
                    item.AverageRating = s.Average;
                    item.ReviewCount = s.Count;
                }
            }
        }
    }
}
