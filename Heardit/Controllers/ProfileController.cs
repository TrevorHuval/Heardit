using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using static System.Reflection.Metadata.BlobBuilder;

namespace Heardit.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<HearditUser> _userManager;
        private readonly HearditDbContext _context;

        public ProfileController(UserManager<HearditUser> userManager, HearditDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public IActionResult Index(string username)
        {
            try
            {
                var currentUserId = User.GetLoggedInUserId<string>();

                var followedUserId = _userManager.FindByNameAsync(username).Result.Id;

                var data = new ProfileModel
                {
                    User = _userManager.FindByNameAsync(username).Result,
                    Reviews = _context.Reviews.AsNoTracking().Where(r => r.User.UserName.Equals(username)).Include(r => r.User).ToList(),
                    IsFollowing = _context.Follows.AsNoTracking().Where(f => f.UserId.Equals(followedUserId) && f.FollowerId.Equals(currentUserId)).ToList().Count != 0,
                    FollowersCount = _context.Follows.AsNoTracking().Where(f => f.UserId.Equals(followedUserId)).Count(),
                    FollowingCount = _context.Follows.AsNoTracking().Where(f => f.FollowerId.Equals(followedUserId)).Count()
                };

                return View("Profile", data);
            }
            catch (NullReferenceException)
            {
                return NotFound();
            }
            catch
            {
                return NotFound();
            }
        }

        public IActionResult Follow(string userId)
        {
            var currentUserId = User.GetLoggedInUserId<string>();
            var followedUserId = userId;

            if (!currentUserId.Equals(followedUserId))
            {
                var result = _context.Follows.AsNoTracking().Where(b => b.UserId.Equals(followedUserId) && b.FollowerId.Equals(currentUserId)).ToList();

                if (result.Count == 0)
                {
                    var f = new Follows() { UserId = followedUserId, FollowerId = currentUserId };
                    _context.Follows.Add(f);
                    _context.SaveChanges();
                }

            }

            return RedirectToAction("Index", new { username = _userManager.FindByIdAsync(followedUserId).Result.UserName });
        }

        public IActionResult UnFollow(string userId)
        {
            var currentUserId = User.GetLoggedInUserId<string>();
            var followedUserId = userId;

            if (!currentUserId.Equals(followedUserId))
            {
                var result = _context.Follows.AsNoTracking().Where(b => b.UserId.Equals(followedUserId) && b.FollowerId.Equals(currentUserId)).ToList();

                if (result.Count != 0)
                {
                    var f = new Follows() { UserId = followedUserId, FollowerId = currentUserId };
                    _context.Follows.Remove(f);
                    _context.SaveChanges();
                }
            }

            return RedirectToAction("Index", new { username = _userManager.FindByIdAsync(followedUserId).Result.UserName });
        }

    }
}
