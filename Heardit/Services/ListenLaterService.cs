using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Services
{
    public interface IListenLaterService
    {
        /// <summary>
        /// Saves the track if it isn't saved, removes it if it is. Null when the id can't be resolved
        /// to a track at all.
        /// </summary>
        Task<bool?> ToggleAsync(string songId, string userId);

        /// <summary>The user's queue, newest save first, one page at a time.</summary>
        Task<PagedList<ListenLater>> GetQueueAsync(string userId, int page = 1);

        /// <summary>Which of these songs the user has already saved, for a page of feed cards.</summary>
        Task<HashSet<string>> GetSavedSongIdsAsync(string userId, IEnumerable<string> songIds);
    }

    public class ListenLaterService : IListenLaterService
    {
        private readonly HearditDbContext _context;
        private readonly ISongService _songService;

        public ListenLaterService(HearditDbContext context, ISongService songService)
        {
            _context = context;
            _songService = songService;
        }

        public async Task<bool?> ToggleAsync(string songId, string userId)
        {
            if (string.IsNullOrWhiteSpace(songId) || string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var removed = await _context.ListenLater
                .Where(l => l.SongId == songId && l.UserId == userId)
                .ExecuteDeleteAsync();

            if (removed > 0)
            {
                return false;
            }

            // A feed card carries nothing but a Spotify id, so the song may never have been stored.
            // The foreign key needs the row to exist before the save does.
            var song = await _songService.GetOrCreateSongAsync(songId);
            if (song == null)
            {
                return null;
            }

            _context.ListenLater.Add(new ListenLater
            {
                UserId = userId,
                SongId = songId,
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A double-submitted form raced us to the same row; the track is saved either way.
                _context.ChangeTracker.Clear();
            }

            return true;
        }

        public async Task<PagedList<ListenLater>> GetQueueAsync(string userId, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return PagedList<ListenLater>.Empty(page);
            }

            return await PagedList<ListenLater>.CreateAsync(
                _context.ListenLater
                    .AsNoTracking()
                    .Where(l => l.UserId == userId)
                    .Include(l => l.Song)
                    .OrderByDescending(l => l.CreatedAt),
                page);
        }

        public async Task<HashSet<string>> GetSavedSongIdsAsync(string userId, IEnumerable<string> songIds)
        {
            var ids = songIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            if (ids.Count == 0 || string.IsNullOrWhiteSpace(userId))
            {
                return new HashSet<string>();
            }

            var saved = await _context.ListenLater
                .AsNoTracking()
                .Where(l => l.UserId == userId && ids.Contains(l.SongId))
                .Select(l => l.SongId)
                .ToListAsync();

            return saved.ToHashSet();
        }
    }
}
