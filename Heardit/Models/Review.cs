using Heardit.Areas.Identity.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Heardit.Models
{
    public class Review
    {
        [System.ComponentModel.DataAnnotations.Key]
        public string ReviewId { get; set; } = null!;

        public string SongId { get; set; } = null!;

        public string SongName { get; set; } = null!;

        public string WrittenReview { get; set; } = null!;

        public HearditUser User { get; set; } = null!;

        [Column(TypeName = "decimal(3, 1)")]
        public decimal Rating { get; set; }

        /// <summary>When the review was first created (UTC). Used to order and sort reviews.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When the review was last edited (UTC), or null if never edited.</summary>
        public DateTime? UpdatedAt { get; set; }

        public Review() { }

        public Review(string _writtenReview, HearditUser _user, decimal _rating, string _songId, string _songName)
        {
            ReviewId = Guid.NewGuid().ToString();
            SongId = _songId;
            WrittenReview = _writtenReview;
            Rating = _rating;
            User = _user;
            SongName = _songName;
            CreatedAt = DateTime.UtcNow;
        }

    }
}
