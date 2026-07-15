using Heardit.Models;

namespace Heardit.ViewModels
{
    public class SongViewModel
    {
        public required Song Song { get; set; }

        public required List<Review> Reviews { get; set; }
    }
}
