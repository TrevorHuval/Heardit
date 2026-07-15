using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Services
{
    public interface ISongService
    {
        /// <summary>The song plus its reviews for the song page, or null if the id can't be resolved.</summary>
        Task<SongViewModel?> GetSongPageAsync(string songId);

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

        public async Task<SongViewModel?> GetSongPageAsync(string songId)
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

            var reviews = await _context.Reviews
                .AsNoTracking()
                .Where(r => r.SongId == songId)
                .Include(r => r.User)
                .ToListAsync();

            return new SongViewModel { Song = song, Reviews = reviews };
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
