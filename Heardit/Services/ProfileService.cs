using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Services
{
    public interface IProfileService
    {
        Task<ProfileViewModel?> GetProfileAsync(string username, string? currentUserId, int page = 1);

        /// <summary>One page of either the follower or the following list, plus both counts.</summary>
        Task<FollowViewModel?> GetFollowsAsync(string username, string? currentUserId, bool showingFollowing, int page = 1);

        /// <summary>Whether this user follows anyone at all — decides which home tab opens by default.</summary>
        Task<bool> IsFollowingAnyoneAsync(string userId);

        /// <summary>Follows the target user; returns the target's username, or null if not found.</summary>
        Task<string?> FollowAsync(string targetUserId, string currentUserId);

        /// <summary>Unfollows the target user; returns the target's username, or null if not found.</summary>
        Task<string?> UnfollowAsync(string targetUserId, string currentUserId);
    }

    public class ProfileService : IProfileService
    {
        private readonly HearditDbContext _context;
        private readonly UserManager<HearditUser> _userManager;

        public ProfileService(HearditDbContext context, UserManager<HearditUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<ProfileViewModel?> GetProfileAsync(string username, string? currentUserId, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                return null;
            }

            return new ProfileViewModel
            {
                User = user,
                Reviews = await PagedList<Review>.CreateAsync(
                    _context.Reviews
                        .AsNoTracking()
                        .Where(r => r.UserId == user.Id)
                        .Include(r => r.User)
                        .OrderByDescending(r => r.CreatedAt),
                    page),
                IsFollowing = await _context.Follows
                    .AsNoTracking()
                    .AnyAsync(f => f.UserId == user.Id && f.FollowerId == currentUserId),
                FollowersCount = await _context.Follows
                    .AsNoTracking()
                    .CountAsync(f => f.UserId == user.Id),
                FollowingCount = await _context.Follows
                    .AsNoTracking()
                    .CountAsync(f => f.FollowerId == user.Id)
            };
        }

        public async Task<FollowViewModel?> GetFollowsAsync(string username, string? currentUserId, bool showingFollowing, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                return null;
            }

            // Only the visible tab is queried; the other is a link away.
            var listeners = showingFollowing
                ? _context.Follows.AsNoTracking().Where(f => f.FollowerId == user.Id).Select(f => f.User)
                : _context.Follows.AsNoTracking().Where(f => f.UserId == user.Id).Select(f => f.Follower);

            return new FollowViewModel
            {
                User = user,
                ShowingFollowing = showingFollowing,
                Listeners = await PagedList<HearditUser>.CreateAsync(listeners.OrderBy(u => u.UserName), page),
                FollowersCount = await _context.Follows
                    .AsNoTracking()
                    .CountAsync(f => f.UserId == user.Id),
                FollowingCount = await _context.Follows
                    .AsNoTracking()
                    .CountAsync(f => f.FollowerId == user.Id),
                IsFollowing = await _context.Follows
                    .AsNoTracking()
                    .AnyAsync(f => f.UserId == user.Id && f.FollowerId == currentUserId)
            };
        }

        public async Task<bool> IsFollowingAnyoneAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return await _context.Follows.AsNoTracking().AnyAsync(f => f.FollowerId == userId);
        }

        public async Task<string?> FollowAsync(string targetUserId, string currentUserId)
        {
            var target = await _userManager.FindByIdAsync(targetUserId);
            if (target == null)
            {
                return null;
            }

            if (targetUserId != currentUserId)
            {
                var alreadyFollowing = await _context.Follows
                    .AnyAsync(f => f.UserId == targetUserId && f.FollowerId == currentUserId);

                if (!alreadyFollowing)
                {
                    _context.Follows.Add(new Follows { UserId = targetUserId, FollowerId = currentUserId });
                    await _context.SaveChangesAsync();
                }
            }

            return target.UserName;
        }

        public async Task<string?> UnfollowAsync(string targetUserId, string currentUserId)
        {
            var target = await _userManager.FindByIdAsync(targetUserId);
            if (target == null)
            {
                return null;
            }

            if (targetUserId != currentUserId)
            {
                await _context.Follows
                    .Where(f => f.UserId == targetUserId && f.FollowerId == currentUserId)
                    .ExecuteDeleteAsync();
            }

            return target.UserName;
        }
    }
}
