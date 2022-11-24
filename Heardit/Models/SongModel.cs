using Heardit.Areas.Identity.Data;

namespace Heardit.Models
{
    public class SongModel
    {
        public Song Song { get; set; }

        public List<Review> Reviews { get; set; }

        public bool IsEmpty
        {
            get
            {
                return (Reviews == null);
            }
        }
    }
}
