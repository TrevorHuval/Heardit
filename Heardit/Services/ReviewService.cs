using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.ViewModels;
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

    /// <summary>Like tally for a single review, plus whether the reader is one of the likers.</summary>
    public record ReviewLikeStats(int Count, bool LikedByMe);

    /// <summary>A track that's drawing attention, with its overall rating across every review.</summary>
    public record TrendingSong(Song Song, decimal Average, int ReviewCount);

    /// <summary>
    /// The trending list, and whether it had to fall back to all-time activity because the recent
    /// window was too quiet to fill a shelf (the heading changes to say so).
    /// </summary>
    public record TrendingResult(IReadOnlyList<TrendingSong> Songs, bool IsAllTime);

    public interface IReviewService
    {
        Task<Review?> GetReviewAsync(string reviewId);

        /// <summary>Reviews written by the people this user follows, newest first, one page at a time.</summary>
        Task<PagedList<Review>> GetFollowingFeedAsync(string userId, int page = 1);

        /// <summary>Everyone's reviews, newest first, one page at a time.</summary>
        Task<PagedList<Review>> GetRecentReviewsAsync(int page = 1);

        /// <summary>
        /// Tracks with the most activity (reviews plus likes on those reviews) over the last week,
        /// widening to all time when the week can't fill <paramref name="take"/> slots.
        /// </summary>
        Task<TrendingResult> GetTrendingAsync(int take = 8);

        /// <summary>Creates the user's review for a song, or updates it if they already reviewed it.</summary>
        Task<ReviewUpsertStatus> AddOrUpdateReviewAsync(string writtenReview, decimal rating, string songId, string songName, string userId);

        /// <summary>Deletes a review only if it belongs to the current user.</summary>
        Task<ReviewDeleteResult> DeleteReviewAsync(string reviewId, string currentUserId);

        /// <summary>Average rating and review count per song id, for the tracks shown in a feed.</summary>
        Task<IReadOnlyDictionary<string, SongReviewStats>> GetSongStatsAsync(IEnumerable<string> songIds);

        /// <summary>Likes the review if it isn't liked yet, unlikes it if it is. Null when the review is gone.</summary>
        Task<bool?> ToggleLikeAsync(string reviewId, string userId);

        /// <summary>Like counts and the reader's own likes for a page of reviews, in one batch.</summary>
        Task<IReadOnlyDictionary<string, ReviewLikeStats>> GetLikeStatsAsync(IEnumerable<string> reviewIds, string? currentUserId);
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

        public async Task<PagedList<Review>> GetFollowingFeedAsync(string userId, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return PagedList<Review>.Empty(page);
            }

            return await PagedList<Review>.CreateAsync(
                _context.Reviews
                    .AsNoTracking()
                    .Where(r => _context.Follows.Any(f => f.FollowerId == userId && f.UserId == r.UserId))
                    .Include(r => r.User)
                    .OrderByDescending(r => r.CreatedAt),
                page);
        }

        public async Task<PagedList<Review>> GetRecentReviewsAsync(int page = 1)
        {
            return await PagedList<Review>.CreateAsync(
                _context.Reviews
                    .AsNoTracking()
                    .Include(r => r.User)
                    .OrderByDescending(r => r.CreatedAt),
                page);
        }

        public async Task<TrendingResult> GetTrendingAsync(int take = 8)
        {
            if (take < 1)
            {
                return new TrendingResult(Array.Empty<TrendingSong>(), false);
            }

            // A week of activity is "trending"; below a handful of tracks it reads as broken rather
            // than quiet, so a young site shows its all-time most-talked-about tracks instead.
            var since = DateTime.UtcNow - TrendingWindow;
            var recent = await RankSongsAsync(since, take);
            if (recent.Count >= Math.Min(take, TrendingMinimum))
            {
                return new TrendingResult(await ToTrendingSongsAsync(recent), false);
            }

            var allTime = await RankSongsAsync(null, take);
            return new TrendingResult(await ToTrendingSongsAsync(allTime), true);
        }

        private static readonly TimeSpan TrendingWindow = TimeSpan.FromDays(7);
        private const int TrendingMinimum = 4;
        private const int TrendingCandidates = 200;

        /// <summary>Song ids ordered by activity score: reviews in the window plus likes on those reviews.</summary>
        private async Task<List<string>> RankSongsAsync(DateTime? since, int take)
        {
            var reviews = _context.Reviews.AsNoTracking();
            if (since != null)
            {
                reviews = reviews.Where(r => r.CreatedAt >= since);
            }

            // Counts and dates group cleanly in SQL on every provider; the ratings (decimal) stay out of
            // it and are averaged later for just the songs that make the list.
            var counts = await reviews
                .GroupBy(r => r.SongId)
                .Select(g => new { SongId = g.Key, Reviews = g.Count(), Latest = g.Max(r => r.CreatedAt) })
                .OrderByDescending(x => x.Reviews)
                .ThenByDescending(x => x.Latest)
                .Take(TrendingCandidates)
                .ToListAsync();

            if (counts.Count == 0)
            {
                return new List<string>();
            }

            var songIds = counts.Select(c => c.SongId).ToList();
            var likes = await _context.ReviewLikes
                .AsNoTracking()
                .Where(l => songIds.Contains(l.Review.SongId) && (since == null || l.Review.CreatedAt >= since))
                .GroupBy(l => l.Review.SongId)
                .Select(g => new { SongId = g.Key, Likes = g.Count() })
                .ToDictionaryAsync(x => x.SongId, x => x.Likes);

            return counts
                .OrderByDescending(c => c.Reviews + likes.GetValueOrDefault(c.SongId))
                .ThenByDescending(c => c.Reviews)
                .ThenByDescending(c => c.Latest)
                .Select(c => c.SongId)
                .Take(take)
                .ToList();
        }

        /// <summary>Pairs ranked ids with their stored song and all-time rating, keeping the rank order.</summary>
        private async Task<IReadOnlyList<TrendingSong>> ToTrendingSongsAsync(IReadOnlyList<string> songIds)
        {
            if (songIds.Count == 0)
            {
                return Array.Empty<TrendingSong>();
            }

            var songs = await _context.Songs
                .AsNoTracking()
                .Where(s => songIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id);

            var ratings = await _context.Reviews
                .AsNoTracking()
                .Where(r => songIds.Contains(r.SongId))
                .Select(r => new { r.SongId, r.Rating })
                .ToListAsync();

            var stats = ratings
                .GroupBy(r => r.SongId)
                .ToDictionary(g => g.Key, g => (Average: Math.Round(g.Average(r => r.Rating), 1), Count: g.Count()));

            // A review always has its song stored first (SongService.GetOrCreateSongAsync), but skip
            // rather than throw if one is ever missing.
            return songIds
                .Where(songs.ContainsKey)
                .Select(id => new TrendingSong(
                    songs[id],
                    stats.TryGetValue(id, out var s) ? s.Average : 0,
                    stats.TryGetValue(id, out var c) ? c.Count : 0))
                .ToList();
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

        public async Task<bool?> ToggleLikeAsync(string reviewId, string userId)
        {
            if (string.IsNullOrWhiteSpace(reviewId) || string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            // Liking your own review is allowed — it's a bookmark as much as an endorsement.
            var reviewExists = await _context.Reviews.AsNoTracking().AnyAsync(r => r.ReviewId == reviewId);
            if (!reviewExists)
            {
                return null;
            }

            var removed = await _context.ReviewLikes
                .Where(l => l.ReviewId == reviewId && l.UserId == userId)
                .ExecuteDeleteAsync();

            if (removed > 0)
            {
                return false;
            }

            _context.ReviewLikes.Add(new ReviewLike
            {
                ReviewId = reviewId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A double-submitted form raced us to the same row; the like stands either way.
                _context.ChangeTracker.Clear();
            }

            return true;
        }

        public async Task<IReadOnlyDictionary<string, ReviewLikeStats>> GetLikeStatsAsync(IEnumerable<string> reviewIds, string? currentUserId)
        {
            var ids = reviewIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<string, ReviewLikeStats>();
            }

            // Two narrow queries rather than one grouped query with a conditional aggregate: the counts
            // group cleanly, and "did I like it" is a plain filter on the same small set of ids.
            var counts = await _context.ReviewLikes
                .AsNoTracking()
                .Where(l => ids.Contains(l.ReviewId))
                .GroupBy(l => l.ReviewId)
                .Select(g => new { ReviewId = g.Key, Count = g.Count() })
                .ToListAsync();

            var mine = string.IsNullOrEmpty(currentUserId)
                ? new List<string>()
                : await _context.ReviewLikes
                    .AsNoTracking()
                    .Where(l => ids.Contains(l.ReviewId) && l.UserId == currentUserId)
                    .Select(l => l.ReviewId)
                    .ToListAsync();

            var likedByMe = mine.ToHashSet();

            return counts.ToDictionary(
                c => c.ReviewId,
                c => new ReviewLikeStats(c.Count, likedByMe.Contains(c.ReviewId)));
        }
    }
}
