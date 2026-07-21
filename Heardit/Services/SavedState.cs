using Heardit.ViewModels;

namespace Heardit.Services
{
    /// <summary>Marks the feed items the reader already saved, in a single batched query.</summary>
    public static class SavedState
    {
        public static async Task ApplyAsync(
            IListenLaterService listenLater, IReadOnlyList<FeedItemViewModel> feed, string userId)
        {
            if (feed.Count == 0)
            {
                return;
            }

            var saved = await listenLater.GetSavedSongIdsAsync(userId, feed.Select(f => f.Id));
            foreach (var item in feed)
            {
                item.Saved = saved.Contains(item.Id);
            }
        }
    }
}
