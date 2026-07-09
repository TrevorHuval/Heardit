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

        public async Task<IActionResult> Index(string username)
        {
            var model = await _profileService.GetFollowsAsync(username, User.GetLoggedInUserId<string>());
            if (model == null)
            {
                return NotFound();
            }

            return View("Follow", model);
        }
    }
}
