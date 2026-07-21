using Heardit.Areas.Identity.Data;
using Heardit.Models;
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
        public async Task<IActionResult> UpdateBio(string? bio)
        {
            if (!await _profileService.UpdateBioAsync(User.GetLoggedInUserId<string>(), bio))
            {
                TempData["FlashError"] = $"Your bio has to fit in {HearditUser.BioMaxLength} characters.";
            }
            else
            {
                TempData["Flash"] = "Your bio was saved.";
            }

            return RedirectToAction(nameof(Index), new { username = User.Identity?.Name });
        }

        [HttpPost]
        public async Task<IActionResult> AddFavorite(string songId, string? returnUrl)
        {
            var status = await _profileService.AddFavoriteAsync(User.GetLoggedInUserId<string>(), songId);
            if (status == FavoriteAddStatus.NotFound)
            {
                return NotFound();
            }

            if (status == FavoriteAddStatus.Full)
            {
                TempData["FlashError"] =
                    $"You already have {FavoriteTrack.MaxPerUser} favorites — remove one from your profile first.";
            }
            else if (status == FavoriteAddStatus.Added)
            {
                TempData["Flash"] = "Pinned to your profile.";
            }

            return Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl!)
                : RedirectToAction(nameof(Index), new { username = User.Identity?.Name });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFavorite(string songId)
        {
            await _profileService.RemoveFavoriteAsync(User.GetLoggedInUserId<string>(), songId);
            TempData["Flash"] = "Removed from your favorites.";

            return RedirectToAction(nameof(Index), new { username = User.Identity?.Name });
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
