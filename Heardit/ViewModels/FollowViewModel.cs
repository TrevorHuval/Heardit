using Heardit.Areas.Identity.Data;

namespace Heardit.ViewModels
{
    public class FollowViewModel
    {
        public required List<HearditUser> FollowersList { get; set; }

        public required List<HearditUser> FollowingList { get; set; }

        public required HearditUser User { get; set; }

        public int FollowersCount { get; set; }

        public int FollowingCount { get; set; }

        public bool IsFollowing { get; set; }
    }
}
