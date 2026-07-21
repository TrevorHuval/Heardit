using Heardit.Models;
using Heardit.Services;

namespace Heardit.ViewModels
{
    public class HomeIndexViewModel
    {
        public IReadOnlyList<FeedItemViewModel> Feed { get; set; } = Array.Empty<FeedItemViewModel>();

        /// <summary>True when the Spotify call failed, as opposed to genuinely returning nothing.</summary>
        public bool SpotifyUnavailable { get; set; }

        /// <summary>Which tab is open. Only that tab's data is loaded; the other is a link away.</summary>
        public bool ShowingFollowing { get; set; }

        /// <summary>Whether the reader follows anyone — separates a quiet feed from an empty one.</summary>
        public bool FollowsAnyone { get; set; }

        /// <summary>Reviews from followed listeners; empty unless the Following tab is open.</summary>
        public PagedList<Review> Reviews { get; set; } = PagedList<Review>.Empty();

        public IReadOnlyDictionary<string, ReviewLikeStats> LikeStats { get; set; } =
            new Dictionary<string, ReviewLikeStats>();
    }
}
