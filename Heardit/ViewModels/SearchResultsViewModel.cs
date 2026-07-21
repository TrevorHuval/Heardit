namespace Heardit.ViewModels
{
    public class SearchResultsViewModel
    {
        public IReadOnlyList<FeedItemViewModel> Tracks { get; set; } = Array.Empty<FeedItemViewModel>();

        /// <summary>True when the Spotify call failed, as opposed to genuinely matching nothing.</summary>
        public bool SpotifyUnavailable { get; set; }
    }
}
