using Heardit.Areas.Identity.Data;
using MessagePack;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Heardit.Models
{
    public class Review
    {
        [System.ComponentModel.DataAnnotations.Key]
        public string ReviewId { get; set; }

        public string SongId { get; set; }

        public string SongName { get; set; }

        public string WrittenReview { get; set; }

        public HearditUser User { get; set; }

        [Column(TypeName = "decimal(3, 1)")]
        public decimal Rating { get; set; }

        public Review() { }

        public Review(string writtenReview, decimal rating, string songId, string songName)
        {
            SongId = songId;
            WrittenReview = writtenReview;
            Rating = rating;
            SongName = songName;
        }

        public Review(string _writtenReview, HearditUser _user, decimal _rating, string _songId, string _songName)
        {
            ReviewId = Guid.NewGuid().ToString();
            SongId = _songId;
            WrittenReview = _writtenReview;
            Rating = _rating;
            User = _user;
            SongName = _songName;
        }

    }
}
