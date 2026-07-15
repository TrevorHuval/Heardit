using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
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

        // Cache keys and TTLs — Spotify results change slowly, so cache to cut API calls (and stay under rate limits).
        private const string NewReleasesCacheKey = "spotify:newreleases";
        private static readonly TimeSpan NewReleasesTtl = TimeSpan.FromHours(1);
        private static readonly TimeSpan TrackTtl = TimeSpan.FromHours(24);
        private static readonly TimeSpan SearchTtl = TimeSpan.FromMinutes(5);

        private readonly ISpotifyClient _spotify;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SpotifyService> _logger;

        public SpotifyService(ISpotifyClient spotify, IMemoryCache cache, ILogger<SpotifyService> logger)
        {
            _spotify = spotify;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IReadOnlyList<SimpleTrack>> GetNewReleaseTracksAsync()
        {
            if (_cache.TryGetValue(NewReleasesCacheKey, out IReadOnlyList<SimpleTrack>? cached) && cached != null)
            {
                return cached;
            }

            try
            {
                _logger.LogInformation("Cache miss: fetching new releases from the Spotify API.");
                // SpotifyAPI.Web 7 marks GetNewReleases / Albums.GetSeveral obsolete because Spotify
                // deprecated these endpoints, but they still return data for this app's client-credentials
                // token and back the New Releases homepage (see plan phases 1/4). Replacing the homepage
                // data source is tracked separately; suppress the deprecation noise until then.
#pragma warning disable CS0618 // Spotify endpoint deprecated but still functional; see comment above.
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
#pragma warning restore CS0618

                var leadTracks = new List<SimpleTrack>();
                foreach (var album in albums.Albums)
                {
                    var firstTrack = album.Tracks?.Items?.FirstOrDefault();
                    if (firstTrack != null)
                    {
                        leadTracks.Add(firstTrack);
                    }
                }

                if (leadTracks.Count > 0)
                {
                    _cache.Set(NewReleasesCacheKey, (IReadOnlyList<SimpleTrack>)leadTracks, NewReleasesTtl);
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

            var cacheKey = $"spotify:track:{trackId}";
            if (_cache.TryGetValue(cacheKey, out FullTrack? cached) && cached != null)
            {
                return cached;
            }

            try
            {
                _logger.LogInformation("Cache miss: fetching track {TrackId} from the Spotify API.", trackId);
                var track = await _spotify.Tracks.Get(trackId);
                if (track != null)
                {
                    _cache.Set(cacheKey, track, TrackTtl);
                }

                return track;
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

            var cacheKey = $"spotify:search:{query.Trim().ToLowerInvariant()}";
            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<FullTrack>? cached) && cached != null)
            {
                return cached;
            }

            try
            {
                _logger.LogInformation("Cache miss: searching the Spotify API for {Query}.", query);
                var searchRes = await _spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, query));
                var results = searchRes.Tracks?.Items ?? new List<FullTrack>();
                if (results.Count > 0)
                {
                    _cache.Set(cacheKey, (IReadOnlyList<FullTrack>)results, SearchTtl);
                }

                return results;
            }
            catch (APIException ex)
            {
                _logger.LogWarning(ex, "Spotify search failed for query {Query}.", query);
                return Array.Empty<FullTrack>();
            }
        }
    }
}
