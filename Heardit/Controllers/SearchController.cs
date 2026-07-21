using Heardit.Areas.Identity.Data;
using Heardit.Services;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Heardit.Controllers
{
    public class SearchController : Controller
    {
        private readonly ISpotifyService _spotify;
        private readonly IReviewService _reviewService;
        private readonly IProfileService _profileService;
        private readonly IListenLaterService _listenLater;

        public SearchController(
            ISpotifyService spotify,
            IReviewService reviewService,
            IProfileService profileService,
            IListenLaterService listenLater)
        {
            _spotify = spotify;
            _reviewService = reviewService;
            _profileService = profileService;
            _listenLater = listenLater;
        }

        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> _Search(string SearchString)
        {
            if (string.IsNullOrWhiteSpace(SearchString))
            {
                return RedirectToAction("Index", "Home");
            }

            // One query, two kinds of result. Listeners come from our own database, so they still
            // show up when Spotify doesn't answer.
            var model = new SearchResultsViewModel
            {
                Users = await _profileService.SearchUsersAsync(SearchString)
            };

            var results = await _spotify.SearchTracksAsync(SearchString);
            if (results == null)
            {
                model.SpotifyUnavailable = true;
                return View(model);
            }

            var feed = results.Select(t => new FeedItemViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Artists = string.Join(", ", t.Artists.Select(a => a.Name))
            }).ToList();

            await FeedStats.ApplyAsync(_reviewService, feed);
            await SavedState.ApplyAsync(_listenLater, feed, User.GetLoggedInUserId<string>());

            model.Tracks = feed;
            return View(model);
        }
    }
}
