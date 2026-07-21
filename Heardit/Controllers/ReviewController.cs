using Heardit.Areas.Identity.Data;
using Heardit.Services;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Heardit.Controllers
{
    public class ReviewController : Controller
    {
        private readonly IReviewService _reviewService;

        public ReviewController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        public async Task<IActionResult> Index(string reviewId)
        {
            var review = await _reviewService.GetReviewAsync(reviewId);
            if (review == null)
            {
                return NotFound();
            }

            var likeStats = await _reviewService.GetLikeStatsAsync(
                new[] { review.ReviewId }, User.GetLoggedInUserId<string>());

            return View("Review", ReviewViewModel.For(review, likeStats));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleLike(string reviewId, string? returnUrl)
        {
            var liked = await _reviewService.ToggleLikeAsync(reviewId, User.GetLoggedInUserId<string>());
            if (liked == null)
            {
                return NotFound();
            }

            // The form carries wherever the review was rendered, so the reader lands back on the same
            // list at the same page. Anything that isn't ours goes home instead.
            return Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl!)
                : RedirectToAction(nameof(Index), "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string reviewid)
        {
            var result = await _reviewService.DeleteReviewAsync(reviewid, User.GetLoggedInUserId<string>());

            if (result.Status == ReviewDeleteStatus.Deleted)
            {
                TempData["Flash"] = "Your review was deleted.";
            }

            return result.Status switch
            {
                ReviewDeleteStatus.NotFound => NotFound(),
                ReviewDeleteStatus.Forbidden => Forbid(),
                _ => RedirectToAction("Index", "Songs", new { songId = result.SongId })
            };
        }
    }
}
