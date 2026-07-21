using Heardit.Areas.Identity.Data;
using Heardit.Models;

namespace Heardit.ViewModels
{
    public class ProfileViewModel
    {
        public required HearditUser User { get; set; }

        public required PagedList<Review> Reviews { get; set; }

        public bool IsFollowing { get; set; }

        public int FollowersCount { get; set; }

        public int FollowingCount { get; set; }
    }
}
