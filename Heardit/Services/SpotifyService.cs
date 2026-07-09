using System.Text.RegularExpressions;
using SpotifyAPI.Web;

namespace Heardit.Services
{
    public interface ISpotifyService
    {
        /// <summary>Lead track of each current New Release album (homepage feed).</summary>
        Task<IReadOnlyList<SimpleTrack>> GetNewReleaseTracksAsync();

        /// <summary>A single track by Spotify id, or null if the id is invalid or not found.</summary>
        Task<FullTrack?> GetTrackAsync(string trackId);

        /// <summary>Track search results, or an empty list on failure.</summary>
        Task<IReadOnlyList<FullTrack>> SearchTracksAsync(string query);
    }

    public class SpotifyService : ISpotifyService
    {
        // Spotify ids are 22-character base62 strings; validate before spending an API call.
        private static readonly Regex TrackIdPattern = new("^[A-Za-z0-9]{22}$", RegexOptions.Compiled);

        private readonly ISpotifyClient _spotify;
        private readonly ILogger<SpotifyService> _logger;

        public SpotifyService(ISpotifyClient spotify, ILogger<SpotifyService> logger)
        {
            _spotify = spotify;
            _logger = logger;
        }

        public async Task<IReadOnlyList<SimpleTrack>> GetNewReleaseTracksAsync()
        {
            try
            {
                var newReleases = await _spotify.Browse.GetNewReleases();

                var albumIds = newReleases.Albums?.Items?
                    .Where(a => !string.IsNullOrEmpty(a.Id))
                    .Select(a => a.Id)
                    .Take(20)
                    .ToList() ?? new List<string>();

                if (albumIds.Count == 0)
                {
                    return Array.Empty<SimpleTrack>();
                }

                var albums = await _spotify.Albums.GetSeveral(new AlbumsRequest(albumIds));

                var leadTracks = new List<SimpleTrack>();
                foreach (var album in albums.Albums)
                {
                    var firstTrack = album.Tracks?.Items?.FirstOrDefault();
                    if (firstTrack != null)
                    {
                        leadTracks.Add(firstTrack);
                    }
                }

                return leadTracks;
            }
            catch (APIException ex)
            {
                _logger.LogError(ex, "Failed to load new releases from Spotify.");
                return Array.Empty<SimpleTrack>();
            }
        }

        public async Task<FullTrack?> GetTrackAsync(string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId) || !TrackIdPattern.IsMatch(trackId))
            {
                return null;
            }

            try
            {
                return await _spotify.Tracks.Get(trackId);
            }
            catch (APIException ex)
            {
                _logger.LogWarning(ex, "Failed to fetch track {TrackId} from Spotify.", trackId);
                return null;
            }
        }

        public async Task<IReadOnlyList<FullTrack>> SearchTracksAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<FullTrack>();
            }

            try
            {
                var searchRes = await _spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, query));
                return searchRes.Tracks?.Items ?? new List<FullTrack>();
            }
            catch (APIException ex)
            {
                _logger.LogWarning(ex, "Spotify search failed for query {Query}.", query);
                return Array.Empty<FullTrack>();
            }
        }
    }
}
