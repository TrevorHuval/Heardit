using Heardit.Areas.Identity.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace Heardit.Models
{
    public class Follows
    {

        public string UserId { get; set; } = null!;
        public virtual HearditUser User { get; set; } = null!;


        public string FollowerId { get; set; } = null!;
        public virtual HearditUser Follower { get; set; } = null!;
    }
}
