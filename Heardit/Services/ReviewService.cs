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

    public enum ReviewUpsertStatus
    {
        Failed,
        Added,
        Updated
    }

    public record ReviewDeleteResult(ReviewDeleteStatus Status, string? SongId);

    /// <summary>Aggregate review stats for a single song, used by the feed cards.</summary>
    public record SongReviewStats(decimal Average, int Count);

    public interface IReviewService
    {
        Task<Review?> GetReviewAsync(string reviewId);

        /// <summary>Creates the user's review for a song, or updates it if they already reviewed it.</summary>
        Task<ReviewUpsertStatus> AddOrUpdateReviewAsync(string writtenReview, decimal rating, string songId, string songName, string userId);

        /// <summary>Deletes a review only if it belongs to the current user.</summary>
        Task<ReviewDeleteResult> DeleteReviewAsync(string reviewId, string currentUserId);

        /// <summary>Average rating and review count per song id, for the tracks shown in a feed.</summary>
        Task<IReadOnlyDictionary<string, SongReviewStats>> GetSongStatsAsync(IEnumerable<string> songIds);
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

        public async Task<ReviewUpsertStatus> AddOrUpdateReviewAsync(string writtenReview, decimal rating, string songId, string songName, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ReviewUpsertStatus.Failed;
            }

            // One review per user per song: update in place if it already exists.
            var existing = await _context.Reviews
                .FirstOrDefaultAsync(r => r.SongId == songId && r.UserId == userId);

            if (existing != null)
            {
                existing.WrittenReview = writtenReview;
                existing.Rating = rating;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return ReviewUpsertStatus.Updated;
            }

            var review = new Review(writtenReview, user, rating, songId, songName);
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            return ReviewUpsertStatus.Added;
        }

        public async Task<ReviewDeleteResult> DeleteReviewAsync(string reviewId, string currentUserId)
        {
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.ReviewId == reviewId);

            if (review == null)
            {
                return new ReviewDeleteResult(ReviewDeleteStatus.NotFound, null);
            }

            if (review.UserId != currentUserId)
            {
                return new ReviewDeleteResult(ReviewDeleteStatus.Forbidden, review.SongId);
            }

            var songId = review.SongId;
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return new ReviewDeleteResult(ReviewDeleteStatus.Deleted, songId);
        }

        public async Task<IReadOnlyDictionary<string, SongReviewStats>> GetSongStatsAsync(IEnumerable<string> songIds)
        {
            var ids = songIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<string, SongReviewStats>();
            }

            var rows = await _context.Reviews
                .AsNoTracking()
                .Where(r => ids.Contains(r.SongId))
                .GroupBy(r => r.SongId)
                .Select(g => new { SongId = g.Key, Average = g.Average(r => r.Rating), Count = g.Count() })
                .ToListAsync();

            return rows.ToDictionary(
                r => r.SongId,
                r => new SongReviewStats(Math.Round(r.Average, 1), r.Count));
        }
    }
}
