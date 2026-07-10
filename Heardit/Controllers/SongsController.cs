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
        public async Task<IActionResult> Index(string songId)
        {
            var model = await _songService.GetSongPageAsync(songId);
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
                return RedirectToAction(nameof(Index), new { songId = input.SongId });
            }

            // Resolve the song (and its trusted title) server-side rather than trusting a posted name.
            var song = await _songService.GetOrCreateSongAsync(input.SongId);
            if (song == null)
            {
                return NotFound();
            }

            await _reviewService.AddReviewAsync(
                input.WrittenReview ?? string.Empty,
                input.Rating,
                input.SongId,
                song.Title ?? string.Empty,
                User.GetLoggedInUserId<string>());

            return RedirectToAction(nameof(Index), new { songId = input.SongId });
        }
    }
}
