using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Services
{
    public enum ReviewDeleteStatus
    {
        NotFound,
        Forbidden,
        Deleted
    }

    public record ReviewDeleteResult(ReviewDeleteStatus Status, string? SongId);

    public interface IReviewService
    {
        Task<Review?> GetReviewAsync(string reviewId);

        Task AddReviewAsync(string writtenReview, decimal rating, string songId, string songName, string userId);

        /// <summary>Deletes a review only if it belongs to the current user.</summary>
        Task<ReviewDeleteResult> DeleteReviewAsync(string reviewId, string currentUserId);
    }

    public class ReviewService : IReviewService
    {
        private readonly HearditDbContext _context;
        private readonly UserManager<HearditUser> _userManager;

        public ReviewService(HearditDbContext context, UserManager<HearditUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<Review?> GetReviewAsync(string reviewId)
        {
            if (string.IsNullOrWhiteSpace(reviewId))
            {
                return null;
            }

            return await _context.Reviews
                .AsNoTracking()
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.ReviewId == reviewId);
        }

        public async Task AddReviewAsync(string writtenReview, decimal rating, string songId, string songName, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return;
            }

            var review = new Review(writtenReview, user, rating, songId, songName);
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
        }

        public async Task<ReviewDeleteResult> DeleteReviewAsync(string reviewId, string currentUserId)
        {
            var review = await _context.Reviews
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.ReviewId == reviewId);

            if (review == null)
            {
                return new ReviewDeleteResult(ReviewDeleteStatus.NotFound, null);
            }

            if (review.User?.Id != currentUserId)
            {
                return new ReviewDeleteResult(ReviewDeleteStatus.Forbidden, review.SongId);
            }

            var songId = review.SongId;
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return new ReviewDeleteResult(ReviewDeleteStatus.Deleted, songId);
        }
    }
}
