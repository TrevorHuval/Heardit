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
        private readonly IListenLaterService _listenLater;
        private readonly IProfileService _profileService;

        public SongsController(
            ISongService songService,
            IReviewService reviewService,
            IListenLaterService listenLater,
            IProfileService profileService)
        {
            _songService = songService;
            _reviewService = reviewService;
            _listenLater = listenLater;
            _profileService = profileService;
        }

        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> Index(string songId, int page = 1)
        {
            var currentUserId = User.GetLoggedInUserId<string>();
            var model = await _songService.GetSongPageAsync(songId, currentUserId, page);
            if (model == null)
            {
                return NotFound();
            }

            model.LikeStats = await _reviewService.GetLikeStatsAsync(
                model.Reviews.Items.Select(r => r.ReviewId), currentUserId);

            var saved = await _listenLater.GetSavedSongIdsAsync(currentUserId, new[] { model.Song.Id });
            model.Saved = saved.Contains(model.Song.Id);

            var favorite = await _profileService.GetFavoriteStateAsync(currentUserId, model.Song.Id);
            model.IsFavorite = favorite.IsFavorite;
            model.HasFavoriteSlot = favorite.HasSlot;

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
