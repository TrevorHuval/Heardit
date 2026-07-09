using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Services
{
    public interface IProfileService
    {
        Task<ProfileModel?> GetProfileAsync(string username, string? currentUserId);

        Task<FollowModel?> GetFollowsAsync(string username, string? currentUserId);

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

        public async Task<ProfileModel?> GetProfileAsync(string username, string? currentUserId)
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

            return new ProfileModel
            {
                User = user,
                Reviews = await _context.Reviews
                    .AsNoTracking()
                    .Where(r => r.User.UserName == username)
                    .Include(r => r.User)
                    .ToListAsync(),
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

        public async Task<FollowModel?> GetFollowsAsync(string username, string? currentUserId)
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

            return new FollowModel
            {
                User = user,
                FollowersList = await _context.Follows
                    .AsNoTracking()
                    .Where(f => f.UserId == user.Id)
                    .Include(f => f.Follower)
                    .Select(f => f.Follower)
                    .ToListAsync(),
                FollowingList = await _context.Follows
                    .AsNoTracking()
                    .Where(f => f.FollowerId == user.Id)
                    .Include(f => f.User)
                    .Select(f => f.User)
                    .ToListAsync(),
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
