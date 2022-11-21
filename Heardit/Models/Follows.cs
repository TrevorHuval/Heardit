using Heardit.Areas.Identity.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace Heardit.Models
{
    public class Follows
    {

        public string UserId { get; set; }
        public virtual HearditUser User { get; set; }


        public string FollowerId { get; set; }
        public virtual HearditUser Follower { get; set; }
    }
}
