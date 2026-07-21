using Heardit.Areas.Identity.Data;
using Heardit.Services;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Heardit.Controllers
{
    public class SongsController : Controller
    {
        private readonly ISongService _songService;
        private readonly IReviewService _reviewService;

        public SongsController(ISongService songService, IReviewService reviewService)
        {
            _songService = songService;
            _reviewService = reviewService;
        }

        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> Index(string songId, int page = 1)
        {
            var model = await _songService.GetSongPageAsync(songId, User.GetLoggedInUserId<string>(), page);
            if (model == null)
            {
                return NotFound();
            }

            return View("Song", model);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitReview(ReviewInputModel input)
        {
            if (!ModelState.IsValid)
            {
                // Say why, rather than bouncing back to an unchanged page.
                TempData["FlashError"] = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                    ?? "That review couldn't be saved. Check the rating and try again.";

                return RedirectToAction(nameof(Index), new { songId = input.SongId });
            }

            // Resolve the song (and its trusted title) server-side rather than trusting a posted name.
            var song = await _songService.GetOrCreateSongAsync(input.SongId);
            if (song == null)
            {
                return NotFound();
            }

            var result = await _reviewService.AddOrUpdateReviewAsync(
                input.WrittenReview ?? string.Empty,
                input.Rating,
                input.SongId,
                song.Title ?? string.Empty,
                User.GetLoggedInUserId<string>());

            TempData["Flash"] = result == ReviewUpsertStatus.Updated ? "Your review was updated." : "Your review was posted.";

            return RedirectToAction(nameof(Index), new { songId = input.SongId });
        }
    }
}
