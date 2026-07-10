using System.ComponentModel.DataAnnotations;

namespace Heardit.ViewModels
{
    public class ReviewInputModel
    {
        [Required]
        [RegularExpression("^[A-Za-z0-9]{22}$", ErrorMessage = "Invalid song id.")]
        public string SongId { get; set; } = string.Empty;

        [Range(1, 10, ErrorMessage = "Rating must be between 1 and 10.")]
        public decimal Rating { get; set; }

        [StringLength(2000)]
        public string? WrittenReview { get; set; }
    }
}
