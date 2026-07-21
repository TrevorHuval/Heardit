using Heardit.Areas.Identity.Data;

namespace Heardit.ViewModels
{
    public class FollowViewModel
    {
        /// <summary>The list being shown — only the selected tab is loaded, one page at a time.</summary>
        public required PagedList<HearditUser> Listeners { get; set; }

        /// <summary>True when the Following tab is selected, false for Followers.</summary>
        public bool ShowingFollowing { get; set; }

        public required HearditUser User { get; set; }

        public int FollowersCount { get; set; }

        public int FollowingCount { get; set; }

        public bool IsFollowing { get; set; }
    }
}
