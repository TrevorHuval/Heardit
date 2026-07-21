using Heardit.Areas.Identity.Data;

namespace Heardit.Models
{
    /// <summary>One listener liking one review. The pair is the key, so a like can't be doubled.</summary>
    public class ReviewLike
    {
        public string ReviewId { get; set; } = null!;

        public Review Review { get; set; } = null!;

        public string UserId { get; set; } = null!;

        public HearditUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
