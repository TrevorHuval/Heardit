using Heardit.Models;
using Heardit.Services;

namespace Heardit.ViewModels
{
    /// <summary>The homepage dashboard: a short preview of each feed, with a "See all" page behind it.</summary>
    public class HomeIndexViewModel
    {
        /// <summary>Whether the reader follows anyone — separates a quiet friends section from an empty one.</summary>
        public bool FollowsAnyone { get; set; }

        /// <summary>The newest few reviews from followed listeners.</summary>
        public IReadOnlyList<Review> FriendReviews { get; set; } = Array.Empty<Review>();

        /// <summary>True when the friends feed has more than the preview shows.</summary>
        public bool MoreFriendReviews { get; set; }

        public IReadOnlyList<ShelfTileViewModel> Trending { get; set; } = Array.Empty<ShelfTileViewModel>();

        /// <summary>Trending fell back to all-time activity because the last week was too quiet.</summary>
        public bool TrendingIsAllTime { get; set; }

        public IReadOnlyList<ShelfTileViewModel> NewReleases { get; set; } = Array.Empty<ShelfTileViewModel>();

        /// <summary>True when the Spotify call failed, as opposed to genuinely returning nothing.</summary>
        public bool SpotifyUnavailable { get; set; }

        /// <summary>The newest few reviews from everyone.</summary>
        public IReadOnlyList<Review> LatestReviews { get; set; } = Array.Empty<Review>();

        /// <summary>Like stats for every review on the page (friends and latest), fetched in one batch.</summary>
        public IReadOnlyDictionary<string, ReviewLikeStats> LikeStats { get; set; } =
            new Dictionary<string, ReviewLikeStats>();
    }

    /// <summary>A full, paged list of reviews behind one of the homepage's "See all" links.</summary>
    public class ReviewFeedViewModel
    {
        public required string Title { get; init; }

        public required string Subtitle { get; init; }

        public required PagedList<Review> Reviews { get; init; }

        public IReadOnlyDictionary<string, ReviewLikeStats> LikeStats { get; init; } =
            new Dictionary<string, ReviewLikeStats>();

        /// <summary>Shown when the first page is empty.</summary>
        public required string EmptyMessage { get; init; }
    }

    /// <summary>A grid of playable track cards behind one of the homepage's "See all" links.</summary>
    public class TrackGridViewModel
    {
        public required string Title { get; init; }

        public required string Subtitle { get; init; }

        public IReadOnlyList<FeedItemViewModel> Tracks { get; init; } = Array.Empty<FeedItemViewModel>();

        public bool SpotifyUnavailable { get; init; }

        public required string EmptyMessage { get; init; }
    }
}
