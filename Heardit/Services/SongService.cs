using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Services
{
    public interface ISongService
    {
        /// <summary>The song plus a page of its reviews, or null if the id can't be resolved.</summary>
        Task<SongViewModel?> GetSongPageAsync(string songId, string? currentUserId, int page = 1);

        /// <summary>Returns the stored song, lazily creating it from Spotify on first view.</summary>
        Task<Song?> GetOrCreateSongAsync(string songId);
    }

    public class SongService : ISongService
    {
        private readonly HearditDbContext _context;
        private readonly ISpotifyService _spotify;

        public SongService(HearditDbContext context, ISpotifyService spotify)
        {
            _context = context;
            _spotify = spotify;
        }

        public async Task<SongViewModel?> GetSongPageAsync(string songId, string? currentUserId, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(songId))
            {
                return null;
            }

            var song = await GetOrCreateSongAsync(songId);
            if (song == null)
            {
                return null;
            }

            // The header stats cover every review of the song, not just the page being shown.
            var totals = await _context.Reviews
                .AsNoTracking()
                .Where(r => r.SongId == songId)
                .GroupBy(r => r.SongId)
                .Select(g => new { Average = g.Average(r => r.Rating), Count = g.Count() })
                .FirstOrDefaultAsync();

            // No signed-in user means no review of their own; the empty id matches nothing and excludes nothing.
            var readerId = currentUserId ?? string.Empty;

            var myReview = await _context.Reviews
                .AsNoTracking()
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.SongId == songId && r.UserId == readerId);

            var others = await PagedList<Review>.CreateAsync(
                _context.Reviews
                    .AsNoTracking()
                    .Where(r => r.SongId == songId && r.UserId != readerId)
                    .Include(r => r.User)
                    .OrderByDescending(r => r.CreatedAt),
                page);

            return new SongViewModel
            {
                Song = song,
                Reviews = others,
                MyReview = myReview,
                AverageRating = totals == null ? 0 : Math.Round(totals.Average, 1),
                ReviewCount = totals?.Count ?? 0
            };
        }

        public async Task<Song?> GetOrCreateSongAsync(string songId)
        {
            var song = await _context.Songs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == songId);
            if (song != null)
            {
                return song;
            }

            var track = await _spotify.GetTrackAsync(songId);
            if (track == null)
            {
                return null;
            }

            song = new Song(
                songId,
                track.Name,
                track.Artists.FirstOrDefault()?.Name ?? string.Empty,
                track.Album?.Name ?? string.Empty,
                track.Album?.Images?.FirstOrDefault()?.Url ?? string.Empty);

            _context.Songs.Add(song);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent first view already inserted this song; fall back to the stored row.
                _context.Entry(song).State = EntityState.Detached;
                song = await _context.Songs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == songId);
            }

            return song;
        }
    }
}
