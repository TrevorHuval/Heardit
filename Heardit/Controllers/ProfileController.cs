using Heardit.Areas.Identity.Data;
using Heardit.Services;
using Microsoft.AspNetCore.Mvc;

namespace Heardit.Controllers
{
    public class ProfileController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly IReviewService _reviewService;

        public ProfileController(IProfileService profileService, IReviewService reviewService)
        {
            _profileService = profileService;
            _reviewService = reviewService;
        }

        public async Task<IActionResult> Index(string username, int page = 1)
        {
            var currentUserId = User.GetLoggedInUserId<string>();
            var model = await _profileService.GetProfileAsync(username, currentUserId, page);
            if (model == null)
            {
                return NotFound();
            }

            model.LikeStats = await _reviewService.GetLikeStatsAsync(
                model.Reviews.Items.Select(r => r.ReviewId), currentUserId);

            return View("Profile", model);
        }

        [HttpPost]
        public async Task<IActionResult> Follow(string userId)
        {
            var username = await _profileService.FollowAsync(userId, User.GetLoggedInUserId<string>());
            if (username == null)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index), new { username });
        }

        [HttpPost]
        public async Task<IActionResult> UnFollow(string userId)
        {
            var username = await _profileService.UnfollowAsync(userId, User.GetLoggedInUserId<string>());
            if (username == null)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index), new { username });
        }
    }
}
