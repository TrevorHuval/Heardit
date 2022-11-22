using System.ComponentModel.DataAnnotations;

namespace Heardit.Models
{
    public class Song
    {
        [Key]
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
    }
}
