using System.ComponentModel.DataAnnotations;

namespace Heardit.Models
{
    public class Search
    {
        [RegularExpression(@"\S+")]
        public string SearchField { get; set; }
    }
}
