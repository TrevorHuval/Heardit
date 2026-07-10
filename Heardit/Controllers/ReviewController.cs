using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.Services;
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

            return View("Review", new ReviewModel { Review = review });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string reviewid)
        {
            var result = await _reviewService.DeleteReviewAsync(reviewid, User.GetLoggedInUserId<string>());

            return result.Status switch
            {
                ReviewDeleteStatus.NotFound => NotFound(),
                ReviewDeleteStatus.Forbidden => Forbid(),
                _ => RedirectToAction("Index", "Songs", new { songId = result.SongId })
            };
        }
    }
}
