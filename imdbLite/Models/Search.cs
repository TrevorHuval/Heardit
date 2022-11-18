using System.ComponentModel.DataAnnotations;

namespace imdbLite.Models
{
    public class Search
    {
        [RegularExpression(@"\S+")]
        public string SearchField { get; set; }
    }
}
