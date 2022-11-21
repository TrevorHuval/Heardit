using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Controllers
{
    public class FollowController : Controller
    {
        private readonly UserManager<HearditUser> _userManager;
        private readonly HearditDbContext _context;

        public FollowController(UserManager<HearditUser> userManager, HearditDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }


        public IActionResult Index(string username)
        {
            var currentUserId = User.GetLoggedInUserId<string>();
            var followedUserId = _userManager.FindByNameAsync(username).Result.Id;
            var user = _userManager.FindByNameAsync(username).Result;

            var data = new FollowModel()
            {
                FollowersList = _context.Follows.AsNoTracking().Where(f => f.UserId.Equals(user.Id)).Include(f => f.Follower).Select(f => f.Follower).ToList(),
                FollowingList = _context.Follows.AsNoTracking().Where(f => f.FollowerId.Equals(user.Id)).Include(f => f.User).Select(f => f.User).ToList(),
                User = user,
                FollowersCount = _context.Follows.AsNoTracking().Where(f => f.UserId.Equals(followedUserId)).Count(),
                FollowingCount = _context.Follows.AsNoTracking().Where(f => f.FollowerId.Equals(followedUserId)).Count(),
                IsFollowing = _context.Follows.AsNoTracking().Where(f => f.UserId.Equals(user.Id) && f.FollowerId.Equals(currentUserId)).ToList().Count != 0,

            };

            return View("Follows", data);
        }
    }
}
