using Heardit.Areas.Identity.Data;

namespace Heardit.Models
{
    /// <summary>One track a listener parked for later. The pair is the key, so saving twice is a no-op.</summary>
    public class ListenLater
    {
        public string UserId { get; set; } = null!;

        public HearditUser User { get; set; } = null!;

        public string SongId { get; set; } = null!;

        public Song Song { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
