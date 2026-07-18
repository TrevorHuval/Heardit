using System.Diagnostics;
using Heardit.Services;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Heardit.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISpotifyService _spotify;
        private readonly IReviewService _reviewService;

        public HomeController(ISpotifyService spotify, IReviewService reviewService)
        {
            _spotify = spotify;
            _reviewService = reviewService;
        }

        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> Index()
        {
            var tracks = await _spotify.GetNewReleaseTracksAsync();
            var feed = tracks.Select(t => new FeedItemViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Artists = string.Join(", ", t.Artists.Select(a => a.Name))
            }).ToList();

            await FeedStats.ApplyAsync(_reviewService, feed);
            return View(feed);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // When reached via UseStatusCodePagesWithReExecute the original status code is preserved
            // on the response, so the view can tailor its copy (404 vs 429 vs a generic failure).
            var status = HttpContext.Response.StatusCode;
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = status >= 400 ? status : null
            });
        }
    }
}
