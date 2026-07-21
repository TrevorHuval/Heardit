using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.Services;

namespace Heardit.ViewModels
{
    public class ProfileViewModel
    {
        public required HearditUser User { get; set; }

        public required PagedList<Review> Reviews { get; set; }

        /// <summary>Like counts for the reviews on this page, batched by the controller.</summary>
        public IReadOnlyDictionary<string, ReviewLikeStats> LikeStats { get; set; } =
            new Dictionary<string, ReviewLikeStats>();

        /// <summary>Up to four pinned tracks, in slot order.</summary>
        public IReadOnlyList<FavoriteTrack> Favorites { get; set; } = Array.Empty<FavoriteTrack>();

        public bool IsFollowing { get; set; }

        public int FollowersCount { get; set; }

        public int FollowingCount { get; set; }
    }
}
