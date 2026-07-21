using System.Diagnostics;
using Heardit.Areas.Identity.Data;
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
        private readonly IProfileService _profileService;

        public HomeController(ISpotifyService spotify, IReviewService reviewService, IProfileService profileService)
        {
            _spotify = spotify;
            _reviewService = reviewService;
            _profileService = profileService;
        }

        // One action serves both tabs. The Following tab never touches Spotify, so it just inherits the
        // generous limiter rather than needing one of its own.
        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> Index(string? tab, int page = 1)
        {
            var userId = User.GetLoggedInUserId<string>();
            var followsAnyone = await _profileService.IsFollowingAnyoneAsync(userId);

            // Following is the better landing page once you follow someone; until then it would be a
            // wall of nothing, so new releases lead.
            var showingFollowing = tab == null
                ? followsAnyone
                : string.Equals(tab, "following", StringComparison.OrdinalIgnoreCase);

            var model = new HomeIndexViewModel
            {
                ShowingFollowing = showingFollowing,
                FollowsAnyone = followsAnyone
            };

            if (showingFollowing)
            {
                model.Reviews = await _reviewService.GetFollowingFeedAsync(userId, page);
                model.LikeStats = await _reviewService.GetLikeStatsAsync(
                    model.Reviews.Items.Select(r => r.ReviewId), userId);
                return View(model);
            }

            var tracks = await _spotify.GetNewReleaseTracksAsync();
            if (tracks == null)
            {
                model.SpotifyUnavailable = true;
                return View(model);
            }

            var feed = tracks.Select(t => new FeedItemViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Artists = string.Join(", ", t.Artists.Select(a => a.Name))
            }).ToList();

            await FeedStats.ApplyAsync(_reviewService, feed);
            model.Feed = feed;
            return View(model);
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
