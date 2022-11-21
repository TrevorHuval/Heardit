using Heardit.Areas.Identity.Data;

namespace Heardit.Models
{
    public class FollowModel
    {
        public List<HearditUser> FollowersList { get; set; }
        public List<HearditUser> FollowingList { get; set; }
        public HearditUser User { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IsFollowing { get; set; }
    }
}
