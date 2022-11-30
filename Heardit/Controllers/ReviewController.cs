using Heardit.Areas.Identity.Data;
using Heardit.Migrations;
using Heardit.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Heardit.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly UserManager<HearditUser> _userManager;
        private readonly HearditDbContext _context;

        public ReviewController(UserManager<HearditUser> userManager, HearditDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public IActionResult Index(string reviewId)
        {
            try
            {
                var reviewFind = _context.Reviews.AsNoTracking().Where(p => p.ReviewId.Equals(reviewId)).Include(p => p.User).FirstOrDefault();

                if (reviewFind == null)
                {
                    throw new NullReferenceException();
                }

                return View("Review", new ReviewModel() { Review = reviewFind });
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

        public async Task<IActionResult> Delete(string reviewid)
        {
            if (_context.Reviews == null)
            {
                return Problem("Entity set 'HearditDbContext.Review'  is null.");
            }
            var review = _context.Reviews.AsNoTracking().Where(p => p.ReviewId.Equals(reviewid)).Include(p => p.User).FirstOrDefault();
            if (review != null)
            {
                _context.Reviews.Remove(review);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Songs", new { songId = review.SongId });
        }
    }
}
