using Heardit.Areas.Identity.Data;
using Heardit.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heardit.Controllers
{
    [Authorize]
    public class SongsController : Controller
    {
        private readonly ISongService _songService;
        private readonly IReviewService _reviewService;

        public SongsController(ISongService songService, IReviewService reviewService)
        {
            _songService = songService;
            _reviewService = reviewService;
        }

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(string writtenReview, decimal rating, string songId, string songName)
        {
            await _reviewService.AddReviewAsync(writtenReview, rating, songId, songName, User.GetLoggedInUserId<string>());
            return RedirectToAction(nameof(Index), new { songId });
        }
    }
}
