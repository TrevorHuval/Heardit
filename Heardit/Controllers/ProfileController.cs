using Heardit.Areas.Identity.Data;
using Heardit.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heardit.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        public async Task<IActionResult> Index(string username)
        {
            var model = await _profileService.GetProfileAsync(username, User.GetLoggedInUserId<string>());
            if (model == null)
            {
                return NotFound();
            }

            return View("Profile", model);
        }

        public async Task<IActionResult> Follow(string userId)
        {
            var username = await _profileService.FollowAsync(userId, User.GetLoggedInUserId<string>());
            if (username == null)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index), new { username });
        }

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
