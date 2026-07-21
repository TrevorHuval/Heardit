using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Services
{
    public enum FavoriteAddStatus
    {
        /// <summary>The song id doesn't match a stored track.</summary>
        NotFound,
        /// <summary>All four slots are taken.</summary>
        Full,
        AlreadyThere,
        Added
    }

    /// <summary>What the song page needs to decide whether to offer the favorite button.</summary>
    public record FavoriteState(bool IsFavorite, bool HasSlot);

    public interface IProfileService
    {
        Task<ProfileViewModel?> GetProfileAsync(string username, string? currentUserId, int page = 1);

        /// <summary>Listeners whose username contains the query, alphabetical, capped at ten.</summary>
        Task<IReadOnlyList<HearditUser>> SearchUsersAsync(string query);

        /// <summary>Saves the user's bio. False when it's too long, or the user is gone.</summary>
        Task<bool> UpdateBioAsync(string userId, string? bio);

        /// <summary>Pins a track to the profile in the lowest free slot.</summary>
        Task<FavoriteAddStatus> AddFavoriteAsync(string userId, string songId);

        /// <summary>Unpins a track, freeing its slot for the next one.</summary>
        Task RemoveFavoriteAsync(string userId, string songId);

        /// <summary>Whether this track is pinned, and whether there's room for it.</summary>
        Task<FavoriteState> GetFavoriteStateAsync(string userId, string songId);

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
                    .CountAsync(f => f.FollowerId == user.Id),
                Favorites = await _context.FavoriteTracks
                    .AsNoTracking()
                    .Where(f => f.UserId == user.Id)
                    .Include(f => f.Song)
                    .OrderBy(f => f.Position)
                    .ToListAsync()
            };
        }

        public async Task<IReadOnlyList<HearditUser>> SearchUsersAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<HearditUser>();
            }

            // ToLower().Contains translates on both Postgres and the SQLite the tests run on;
            // EF.Functions.ILike would be tidier but only exists for Npgsql.
            var needle = query.Trim().ToLower();

            return await _context.Users
                .AsNoTracking()
                .Where(u => u.UserName!.ToLower().Contains(needle))
                .OrderBy(u => u.UserName)
                .Take(10)
                .ToListAsync();
        }

        public async Task<bool> UpdateBioAsync(string userId, string? bio)
        {
            var trimmed = bio?.Trim();
            if (trimmed?.Length > HearditUser.BioMaxLength)
            {
                return false;
            }

            // Tracked through the context rather than the UserManager: nothing here touches the
            // security stamp, and the bio isn't part of the identity cookie.
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return false;
            }

            user.Bio = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<FavoriteAddStatus> AddFavoriteAsync(string userId, string songId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(songId))
            {
                return FavoriteAddStatus.NotFound;
            }

            // Favorites are only ever added from a song page, which has already stored the track.
            if (!await _context.Songs.AsNoTracking().AnyAsync(s => s.Id == songId))
            {
                return FavoriteAddStatus.NotFound;
            }

            var taken = await _context.FavoriteTracks
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => new { f.SongId, f.Position })
                .ToListAsync();

            if (taken.Any(f => f.SongId == songId))
            {
                return FavoriteAddStatus.AlreadyThere;
            }

            var position = FreePosition(taken.Select(f => f.Position));
            if (position == null)
            {
                return FavoriteAddStatus.Full;
            }

            _context.FavoriteTracks.Add(new FavoriteTrack
            {
                UserId = userId,
                SongId = songId,
                Position = position.Value
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Two submissions raced for the same slot. Whether this one landed is a question for
                // the database, not the change tracker.
                _context.ChangeTracker.Clear();
                return await _context.FavoriteTracks.AsNoTracking()
                    .AnyAsync(f => f.UserId == userId && f.SongId == songId)
                    ? FavoriteAddStatus.AlreadyThere
                    : FavoriteAddStatus.Full;
            }

            return FavoriteAddStatus.Added;
        }

        public async Task RemoveFavoriteAsync(string userId, string songId)
        {
            await _context.FavoriteTracks
                .Where(f => f.UserId == userId && f.SongId == songId)
                .ExecuteDeleteAsync();
        }

        public async Task<FavoriteState> GetFavoriteStateAsync(string userId, string songId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(songId))
            {
                return new FavoriteState(false, false);
            }

            var positions = await _context.FavoriteTracks
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => new { f.SongId, f.Position })
                .ToListAsync();

            return new FavoriteState(
                positions.Any(f => f.SongId == songId),
                FreePosition(positions.Select(f => f.Position)) != null);
        }

        /// <summary>The lowest slot nobody's using, or null when all four are spoken for.</summary>
        private static int? FreePosition(IEnumerable<int> taken)
        {
            var used = taken.ToHashSet();
            for (var position = 1; position <= FavoriteTrack.MaxPerUser; position++)
            {
                if (!used.Contains(position))
                {
                    return position;
                }
            }

            return null;
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
