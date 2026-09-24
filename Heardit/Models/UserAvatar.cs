using Heardit.Areas.Identity.Data;

namespace Heardit.Models
{
    /// <summary>
    /// A listener's profile photo: a 256px square WebP, a few kilobytes. Kept apart from the user row
    /// so the many queries that load users never drag image bytes along, and deleted with the account.
    /// </summary>
    public class UserAvatar
    {
        public string UserId { get; set; } = null!;

        public HearditUser User { get; set; } = null!;

        public byte[] Data { get; set; } = Array.Empty<byte>();

        public string ContentType { get; set; } = "image/webp";

        public DateTime UpdatedAt { get; set; }
    }
}
