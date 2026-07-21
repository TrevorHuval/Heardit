using Heardit.Areas.Identity.Data;
using Heardit.Services;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Heardit.Controllers
{
    public class ListenLaterController : Controller
    {
        private readonly IListenLaterService _listenLater;
        private readonly IReviewService _reviewService;

        public ListenLaterController(IListenLaterService listenLater, IReviewService reviewService)
        {
            _listenLater = listenLater;
            _reviewService = reviewService;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            var userId = User.GetLoggedInUserId<string>();
            var queue = await _listenLater.GetQueueAsync(userId, page);

            // The queue renders as feed cards, so it reads like the rest of the app — stats and all.
            var tracks = queue.Items.Select(l => new FeedItemViewModel
            {
                Id = l.SongId,
                Name = l.Song.Title ?? string.Empty,
                Artists = l.Song.Artist ?? string.Empty,
                Saved = true
            }).ToList();

            await FeedStats.ApplyAsync(_reviewService, tracks);

            return View(new PagedList<FeedItemViewModel>
            {
                Items = tracks,
                Page = queue.Page,
                HasNext = queue.HasNext
            });
        }

        // Saving a track the app has never stored means a Spotify lookup, so this shares the limiter
        // with the other endpoints that can call out.
        [HttpPost]
        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> Toggle(string songId, string? returnUrl)
        {
            var saved = await _listenLater.ToggleAsync(songId, User.GetLoggedInUserId<string>());
            if (saved == null)
            {
                return NotFound();
            }

            TempData["Flash"] = saved.Value ? "Saved to your queue." : "Removed from your queue.";

            // Same trick as the like button: the form carries wherever the card was rendered.
            return Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl!)
                : RedirectToAction(nameof(Index));
        }
    }
}
