using Heardit.Areas.Identity.Data;

namespace Heardit.ViewModels
{
    public class SearchResultsViewModel
    {
        /// <summary>Listeners matching the query — our own data, so unaffected by Spotify being down.</summary>
        public IReadOnlyList<HearditUser> Users { get; set; } = Array.Empty<HearditUser>();

        public IReadOnlyList<FeedItemViewModel> Tracks { get; set; } = Array.Empty<FeedItemViewModel>();

        /// <summary>True when the Spotify call failed, as opposed to genuinely matching nothing.</summary>
        public bool SpotifyUnavailable { get; set; }
    }
}
