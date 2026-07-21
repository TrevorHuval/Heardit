namespace Heardit.ViewModels
{
    public class HomeIndexViewModel
    {
        public IReadOnlyList<FeedItemViewModel> Feed { get; set; } = Array.Empty<FeedItemViewModel>();

        /// <summary>True when the Spotify call failed, as opposed to genuinely returning nothing.</summary>
        public bool SpotifyUnavailable { get; set; }
    }
}
