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

        public SearchController(ISpotifyService spotify, IReviewService reviewService)
        {
            _spotify = spotify;
            _reviewService = reviewService;
        }

        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> _Search(string SearchString)
        {
            if (string.IsNullOrWhiteSpace(SearchString))
            {
                return RedirectToAction("Index", "Home");
            }

            var results = await _spotify.SearchTracksAsync(SearchString);
            var feed = results.Select(t => new FeedItemViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Artists = string.Join(", ", t.Artists.Select(a => a.Name))
            }).ToList();

            await FeedStats.ApplyAsync(_reviewService, feed);
            return View(feed);
        }
    }
}
