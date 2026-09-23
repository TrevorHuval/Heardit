using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using SpotifyAPI.Web;

namespace Heardit.Services
{
    /// <summary>
    /// What a feed card needs to draw a track without loading a Spotify player: identity, credits,
    /// and album art. Both the new-releases and search paths flatten Spotify's own models to this.
    /// </summary>
    public record TrackSummary(string Id, string Name, string Artists, string? ImageUrl);

    public interface ISpotifyService
    {
        /// <summary>Lead track of each current New Release album (homepage feed).
        /// Null means Spotify could not be reached; an empty list means it had nothing to give.</summary>
        Task<IReadOnlyList<TrackSummary>?> GetNewReleaseTracksAsync();

        /// <summary>A single track by Spotify id, or null if the id is invalid or not found.</summary>
        Task<FullTrack?> GetTrackAsync(string trackId);

        /// <summary>Track search results. Null means Spotify could not be reached; empty means no matches.</summary>
        Task<IReadOnlyList<TrackSummary>?> SearchTracksAsync(string query);
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

        // New-release feed: how far into the `tag:new` results to look, and how many releases to keep.
        private const string NewReleaseMarket = "US";
        private const int NewReleaseSearchPage = 50;
        private const int NewReleaseSearchDepth = 100;
        private const int NewReleaseCount = 20;
        private const int ArtistBatchSize = 50;

        // The cache is size-bounded (see Program.cs), so every entry declares a size: roughly its row
        // count, so a list of results costs more of the budget than a single track. Every Set below must
        // set one — an entry without a size throws once the cache has a SizeLimit.
        private const long ListEntrySize = 20;
        private const long TrackEntrySize = 1;

        // Search keys are the user's own text. Cap the length that can become a key so nobody can fill
        // the cache with long junk queries; anything longer just skips the cache and goes to Spotify.
        private const int MaxCacheableQueryLength = 100;

        private readonly ISpotifyClient _spotify;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SpotifyService> _logger;

        public SpotifyService(ISpotifyClient spotify, IMemoryCache cache, ILogger<SpotifyService> logger)
        {
            _spotify = spotify;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IReadOnlyList<TrackSummary>?> GetNewReleaseTracksAsync()
        {
            if (_cache.TryGetValue(NewReleasesCacheKey, out IReadOnlyList<TrackSummary>? cached) && cached != null)
            {
                return cached;
            }

            try
            {
                _logger.LogInformation("Cache miss: fetching new releases from the Spotify API.");

                // Spotify's own /browse/new-releases stopped updating in April 2024 for this app, so the
                // feed is built from search instead: `tag:new` matches albums released in the last two
                // weeks. Those come back unranked and global, and a fresh album's own popularity is always
                // 0, so the ranking comes from how popular each release's lead artist is.
                var albums = new List<SimpleAlbum>();
                for (var offset = 0; offset < NewReleaseSearchDepth; offset += NewReleaseSearchPage)
                {
                    var page = await _spotify.Search.Item(new SearchRequest(SearchRequest.Types.Album, "tag:new")
                    {
                        Market = NewReleaseMarket,
                        Limit = NewReleaseSearchPage,
                        Offset = offset
                    });

                    var items = page.Albums?.Items;
                    if (items == null || items.Count == 0)
                    {
                        break;
                    }

                    albums.AddRange(items.Where(a => a != null && !string.IsNullOrEmpty(a.Id) && a.Artists?.Count > 0));
                    if (items.Count < NewReleaseSearchPage)
                    {
                        break;
                    }
                }

                if (albums.Count == 0)
                {
                    return Array.Empty<TrackSummary>();
                }

                var popularity = await GetArtistPopularityAsync(albums.Select(a => a.Artists[0].Id));

                // One release per artist, counting every credited artist, not just the lead: a deluxe
                // edition and its lead single don't take two slots, and a soundtrack's singles (each
                // credited to its performer plus the soundtrack) don't fill the shelf with one cover.
                // Without popularity (the artist lookup failed) the newest releases come first.
                var ranked = albums
                    .Select((album, index) => (album, index))
                    .OrderByDescending(x => popularity.GetValueOrDefault(x.album.Artists[0].Id))
                    .ThenByDescending(x => x.album.ReleaseDate, StringComparer.Ordinal)
                    .ThenBy(x => x.index)
                    .Select(x => x.album);

                var seenArtists = new HashSet<string>();
                var picked = new List<SimpleAlbum>();
                foreach (var album in ranked)
                {
                    var artistIds = album.Artists.Select(a => a.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
                    if (artistIds.Any(seenArtists.Contains))
                    {
                        continue;
                    }

                    seenArtists.UnionWith(artistIds);
                    picked.Add(album);
                    if (picked.Count == NewReleaseCount)
                    {
                        break;
                    }
                }

                // Search albums carry no track listing; one batched album lookup finds each lead track.
#pragma warning disable CS0618 // SDK marks Albums.GetSeveral obsolete; the endpoint still works for this app.
                var full = await _spotify.Albums.GetSeveral(new AlbumsRequest(picked.Select(a => a.Id).ToList()));
#pragma warning restore CS0618

                var leadTracks = new List<TrackSummary>();
                foreach (var album in full.Albums.Where(a => a != null))
                {
                    var firstTrack = album.Tracks?.Items?.FirstOrDefault();
                    if (firstTrack != null && !string.IsNullOrEmpty(firstTrack.Id))
                    {
                        // A track inside an album response carries no album of its own; the art comes from the parent.
                        leadTracks.Add(new TrackSummary(
                            firstTrack.Id,
                            firstTrack.Name,
                            JoinArtists(firstTrack.Artists),
                            CardImage(album.Images)));
                    }
                }

                if (leadTracks.Count > 0)
                {
                    _cache.Set(NewReleasesCacheKey, (IReadOnlyList<TrackSummary>)leadTracks, Entry(NewReleasesTtl, ListEntrySize));
                }

                return leadTracks;
            }
            catch (APIException ex)
            {
                _logger.LogError(ex, "Failed to load new releases from Spotify.");
                return null;
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
                    _cache.Set(cacheKey, track, Entry(TrackTtl, TrackEntrySize));
                }

                return track;
            }
            catch (APIException ex)
            {
                _logger.LogWarning(ex, "Failed to fetch track {TrackId} from Spotify.", trackId);
                return null;
            }
        }

        public async Task<IReadOnlyList<TrackSummary>?> SearchTracksAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<TrackSummary>();
            }

            var trimmed = query.Trim();
            var cacheable = trimmed.Length <= MaxCacheableQueryLength;
            var cacheKey = $"spotify:search:{trimmed.ToLowerInvariant()}";

            if (cacheable && _cache.TryGetValue(cacheKey, out IReadOnlyList<TrackSummary>? cached) && cached != null)
            {
                return cached;
            }

            try
            {
                _logger.LogInformation("Cache miss: searching the Spotify API for {Query}.", query);
                var searchRes = await _spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, query));
                var results = (searchRes.Tracks?.Items ?? new List<FullTrack>())
                    .Where(t => !string.IsNullOrEmpty(t.Id))
                    .Select(t => new TrackSummary(t.Id, t.Name, JoinArtists(t.Artists), CardImage(t.Album?.Images)))
                    .ToList();
                if (cacheable && results.Count > 0)
                {
                    _cache.Set(cacheKey, (IReadOnlyList<TrackSummary>)results, Entry(SearchTtl, ListEntrySize));
                }

                return results;
            }
            catch (APIException ex)
            {
                _logger.LogWarning(ex, "Spotify search failed for query {Query}.", query);
                return null;
            }
        }

        /// <summary>
        /// Popularity (0–100) per artist id. A failed lookup yields an empty map rather than failing the
        /// feed: the releases are still worth showing, just unranked.
        /// </summary>
        private async Task<Dictionary<string, int>> GetArtistPopularityAsync(IEnumerable<string> artistIds)
        {
            var ids = artistIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var popularity = new Dictionary<string, int>();

            try
            {
                // The SDK marks GET /artists and `popularity` as removed, but both still answer for this
                // app (checked September 2026). If Spotify does pull them, this lands in the catch below
                // and the feed carries on unranked.
#pragma warning disable CS0618
                foreach (var batch in ids.Chunk(ArtistBatchSize))
                {
                    var response = await _spotify.Artists.GetSeveral(new ArtistsRequest(batch.ToList()));
                    foreach (var artist in response.Artists.Where(a => a != null))
                    {
                        popularity[artist.Id] = artist.Popularity;
                    }
                }
#pragma warning restore CS0618
            }
            catch (APIException ex)
            {
                _logger.LogWarning(ex, "Could not rank new releases by artist popularity; showing them unranked.");
            }

            return popularity;
        }

        private static MemoryCacheEntryOptions Entry(TimeSpan ttl, long size) =>
            new() { AbsoluteExpirationRelativeToNow = ttl, Size = size };

        private static string JoinArtists(IEnumerable<SimpleArtist>? artists) =>
            artists == null
                ? string.Empty
                : string.Join(", ", artists.Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)));

        // Spotify lists album images largest first (640 / 300 / 64). A feed card is ~300px wide, so the
        // middle size is the sweet spot; fall back to the first one when the set is unusual.
        private static string? CardImage(IEnumerable<Image>? images)
        {
            var list = images?.Where(i => !string.IsNullOrEmpty(i.Url)).ToList();
            if (list == null || list.Count == 0)
            {
                return null;
            }

            return (list.FirstOrDefault(i => i.Width is >= 200 and <= 400) ?? list[0]).Url;
        }
    }
}
