using Heardit.Areas.Identity.Data;
using Heardit.Services;
using Microsoft.AspNetCore.Mvc;

namespace Heardit.Controllers
{
    public class FollowController : Controller
    {
        private readonly IProfileService _profileService;

        public FollowController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        public async Task<IActionResult> Index(string username, string? tab, int page = 1)
        {
            var showingFollowing = string.Equals(tab, "following", StringComparison.OrdinalIgnoreCase);
            var model = await _profileService.GetFollowsAsync(username, User.GetLoggedInUserId<string>(), showingFollowing, page);
            if (model == null)
            {
                return NotFound();
            }

            return View("Follow", model);
        }
    }
}
