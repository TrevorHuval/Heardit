using Heardit.Areas.Identity.Data;

namespace Heardit.Models
{
    public class ProfileModel
    {
        public ProfileModel() { }

        public HearditUser User { get; set; }

        public List<Review> Reviews { get; set; }

        public bool IsFollowing { get; set; }

        public int FollowersCount { get; set; }

        public int FollowingCount { get; set; }
    }
}
