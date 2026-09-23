using Heardit.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SpotifyAPI.Web;

namespace Heardit.Tests;

public class SpotifyServiceTests
{
    // A 22-character base62 string — the shape SpotifyService's id regex accepts.
    private const string ValidTrackId = "1234567890abcdefghijAB";

    // Mirrors Program.cs: a bounded cache, so a Set that forgot its size would throw here too.
    private static SpotifyService CreateService(ISpotifyClient client, IMemoryCache? cache = null) =>
        new(client,
            cache ?? new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 }),
            NullLogger<SpotifyService>.Instance);

    private static SearchResponse SearchResponseWith(params FullTrack[] tracks) =>
        new() { Tracks = new Paging<FullTrack, SearchResponse> { Items = tracks.ToList() } };

    [Fact]
    public async Task GetTrackAsync_InvalidId_ShortCircuitsWithoutCallingSpotify()
    {
        var client = Substitute.For<ISpotifyClient>();
        var service = CreateService(client);

        var result = await service.GetTrackAsync("not-a-valid-id");

        Assert.Null(result);
        await client.Tracks.DidNotReceive().Get(Arg.Any<string>());
    }

    [Fact]
    public async Task GetTrackAsync_BlankId_ShortCircuitsWithoutCallingSpotify()
    {
        var client = Substitute.For<ISpotifyClient>();
        var service = CreateService(client);

        var result = await service.GetTrackAsync("   ");

        Assert.Null(result);
        await client.Tracks.DidNotReceive().Get(Arg.Any<string>());
    }

    [Fact]
    public async Task GetTrackAsync_CachesResult_SecondCallSkipsApi()
    {
        var client = Substitute.For<ISpotifyClient>();
        client.Tracks.Get(ValidTrackId).Returns(new FullTrack { Id = ValidTrackId, Name = "Cached Track" });
        var service = CreateService(client);

        var first = await service.GetTrackAsync(ValidTrackId);
        var second = await service.GetTrackAsync(ValidTrackId);

        Assert.NotNull(first);
        Assert.Same(first, second);
        await client.Tracks.Received(1).Get(ValidTrackId);
    }

    [Fact]
    public async Task GetTrackAsync_ApiException_ReturnsNull()
    {
        var client = Substitute.For<ISpotifyClient>();
        client.Tracks.Get(ValidTrackId).Returns<Task<FullTrack>>(_ => throw new APIException("simulated Spotify failure"));
        var service = CreateService(client);

        var result = await service.GetTrackAsync(ValidTrackId);

        Assert.Null(result);
    }

    // ----- New releases: search `tag:new`, rank by lead-artist popularity, one per artist -----

    private static SimpleAlbum Album(string id, string artistId, string releaseDate) => new()
    {
        Id = id,
        Name = $"Album {id}",
        ReleaseDate = releaseDate,
        Artists = new List<SimpleArtist> { new() { Id = artistId, Name = $"Artist {artistId}" } }
    };

    private static SearchResponse AlbumSearch(params SimpleAlbum[] albums) =>
        new() { Albums = new Paging<SimpleAlbum, SearchResponse> { Items = albums.ToList() } };

    /// <summary>Wires the three calls the new-release feed makes. Each album's lead track is "track-{albumId}".</summary>
    private static ISpotifyClient NewReleaseClient(IReadOnlyList<SimpleAlbum> firstPage, IDictionary<string, int>? popularity,
        IReadOnlyList<SimpleAlbum>? secondPage = null)
    {
        var client = Substitute.For<ISpotifyClient>();
        client.Search.Item(Arg.Is<SearchRequest>(r => r != null && r.Offset == 0)).Returns(AlbumSearch(firstPage.ToArray()));
        client.Search.Item(Arg.Is<SearchRequest>(r => r != null && r.Offset == 50)).Returns(AlbumSearch((secondPage ?? Array.Empty<SimpleAlbum>()).ToArray()));

#pragma warning disable CS0618 // SDK-obsolete members the service still relies on (suppressed there too).
        if (popularity == null)
        {
            client.Artists.GetSeveral(Arg.Any<ArtistsRequest>())
                .Returns<Task<ArtistsResponse>>(_ => throw new APIException("simulated: artists endpoint gone"));
        }
        else
        {
            client.Artists.GetSeveral(Arg.Any<ArtistsRequest>()).Returns(ci => new ArtistsResponse
            {
                Artists = ci.Arg<ArtistsRequest>()!.Ids!
                    .Select(id => new FullArtist { Id = id, Popularity = popularity.TryGetValue(id, out var p) ? p : 0 })
                    .ToList()
            });
        }

        client.Albums.GetSeveral(Arg.Any<AlbumsRequest>()).Returns(ci => new AlbumsResponse
        {
            Albums = ci.Arg<AlbumsRequest>()!.Ids!.Select(id => new FullAlbum
            {
                Id = id,
                Images = new List<Image> { new() { Url = $"https://img/{id}/300", Width = 300, Height = 300 } },
                Tracks = new Paging<SimpleTrack>
                {
                    Items = new List<SimpleTrack>
                    {
                        new() { Id = $"track-{id}", Name = $"Lead of {id}", Artists = new List<SimpleArtist> { new() { Name = "A" }, new() { Name = "B" } } }
                    }
                }
            }).ToList()
        });
#pragma warning restore CS0618
        return client;
    }

    [Fact]
    public async Task GetNewReleaseTracksAsync_RanksByArtistPopularity_OnePerArtist_AndCaches()
    {
        var client = NewReleaseClient(
            new[]
            {
                Album("quiet", "artist-quiet", "2026-09-20"),
                Album("star-single", "artist-star", "2026-09-10"),
                Album("star-album", "artist-star", "2026-09-18"),   // same artist, newer: this one wins the slot
                Album("mid", "artist-mid", "2026-09-19")
            },
            new Dictionary<string, int> { ["artist-quiet"] = 10, ["artist-star"] = 95, ["artist-mid"] = 60 });
        var service = CreateService(client);

        var first = await service.GetNewReleaseTracksAsync();
        var second = await service.GetNewReleaseTracksAsync();

        Assert.NotNull(first);
        Assert.Equal(new[] { "track-star-album", "track-mid", "track-quiet" }, first!.Select(t => t.Id));
        Assert.Equal("https://img/star-album/300", first[0].ImageUrl);
        Assert.Equal("A, B", first[0].Artists);
        Assert.Same(first, second);
        // A short first page means there is nothing further to fetch.
        await client.Search.Received(1).Item(Arg.Any<SearchRequest>());
    }

    [Fact]
    public async Task GetNewReleaseTracksAsync_SoundtrackSinglesSharingACredit_KeepOnlyTheTopOne()
    {
        // Each single credits its performer plus the soundtrack itself.
        var soundtrackA = Album("ost-a", "artist-a", "2026-09-17");
        var soundtrackB = Album("ost-b", "artist-b", "2026-09-17");
        soundtrackA.Artists.Add(new SimpleArtist { Id = "soundtrack", Name = "The Game" });
        soundtrackB.Artists.Add(new SimpleArtist { Id = "soundtrack", Name = "The Game" });
        var client = NewReleaseClient(
            new[] { soundtrackA, soundtrackB, Album("solo", "artist-c", "2026-09-18") },
            new Dictionary<string, int> { ["artist-a"] = 90, ["artist-b"] = 80, ["artist-c"] = 70 });
        var service = CreateService(client);

        var result = await service.GetNewReleaseTracksAsync();

        Assert.Equal(new[] { "track-ost-a", "track-solo" }, result!.Select(t => t.Id));
    }

    [Fact]
    public async Task GetNewReleaseTracksAsync_FullFirstPage_FetchesTheSecond()
    {
        var firstPage = Enumerable.Range(0, 50).Select(i => Album($"a{i}", $"artist{i}", "2026-09-20")).ToList();
        var client = NewReleaseClient(firstPage, new Dictionary<string, int> { ["artist-late"] = 99 },
            secondPage: new[] { Album("late", "artist-late", "2026-09-21") });
        var service = CreateService(client);

        var result = await service.GetNewReleaseTracksAsync();

        Assert.NotNull(result);
        Assert.Equal(20, result!.Count);
        // The most popular artist was on page two and still leads.
        Assert.Equal("track-late", result[0].Id);
        await client.Search.Received(2).Item(Arg.Any<SearchRequest>());
    }

    [Fact]
    public async Task GetNewReleaseTracksAsync_ArtistLookupFails_StillReturnsReleasesNewestFirst()
    {
        var client = NewReleaseClient(
            new[] { Album("older", "x", "2026-09-12"), Album("newer", "y", "2026-09-20") },
            popularity: null);
        var service = CreateService(client);

        var result = await service.GetNewReleaseTracksAsync();

        Assert.NotNull(result);
        Assert.Equal(new[] { "track-newer", "track-older" }, result!.Select(t => t.Id));
    }

    [Fact]
    public async Task GetNewReleaseTracksAsync_SearchFails_ReturnsNull()
    {
        var client = Substitute.For<ISpotifyClient>();
        client.Search.Item(Arg.Any<SearchRequest>())
            .Returns<Task<SearchResponse>>(_ => throw new APIException("simulated Spotify failure"));
        var service = CreateService(client);

        // Null is "couldn't reach Spotify" — the views say so instead of claiming there are no releases.
        Assert.Null(await service.GetNewReleaseTracksAsync());
    }

    [Fact]
    public async Task GetNewReleaseTracksAsync_NoResults_ReturnsEmptyWithoutLookups()
    {
        var client = NewReleaseClient(Array.Empty<SimpleAlbum>(), new Dictionary<string, int>());
        var service = CreateService(client);

        var result = await service.GetNewReleaseTracksAsync();

        Assert.NotNull(result);
        Assert.Empty(result!);
#pragma warning disable CS0618
        await client.Albums.DidNotReceive().GetSeveral(Arg.Any<AlbumsRequest>());
#pragma warning restore CS0618
    }

    [Fact]
    public async Task SearchTracksAsync_ReturnsMatchesAndCachesThem()
    {
        var client = Substitute.For<ISpotifyClient>();
        client.Search.Item(Arg.Any<SearchRequest>())
            .Returns(SearchResponseWith(new FullTrack { Id = "t1", Name = "Creep" }));
        var service = CreateService(client);

        var first = await service.SearchTracksAsync("creep");
        var second = await service.SearchTracksAsync("  CREEP  ");   // same key once trimmed and lowered

        Assert.NotNull(first);
        Assert.Equal("Creep", Assert.Single(first!).Name);
        Assert.Same(first, second);
        await client.Search.Received(1).Item(Arg.Any<SearchRequest>());
    }

    [Fact]
    public async Task SearchTracksAsync_LongQuery_SkipsTheCacheEntirely()
    {
        var client = Substitute.For<ISpotifyClient>();
        client.Search.Item(Arg.Any<SearchRequest>())
            .Returns(SearchResponseWith(new FullTrack { Id = "t1", Name = "Creep" }));
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        var service = CreateService(client, cache);

        var longQuery = new string('a', 101);
        await service.SearchTracksAsync(longQuery);
        await service.SearchTracksAsync(longQuery);

        // Nothing cached, so both calls went to Spotify — long queries can't crowd out real entries.
        Assert.Equal(0, cache.Count);
        await client.Search.Received(2).Item(Arg.Any<SearchRequest>());
    }

    [Fact]
    public async Task SearchTracksAsync_ApiException_ReturnsNull()
    {
        var client = Substitute.For<ISpotifyClient>();
        client.Search.Item(Arg.Any<SearchRequest>()).Returns<Task<SearchResponse>>(_ => throw new APIException("simulated Spotify failure"));
        var service = CreateService(client);

        Assert.Null(await service.SearchTracksAsync("radiohead"));
    }

    [Fact]
    public async Task SearchTracksAsync_BlankQuery_ShortCircuitsWithoutCallingSpotify()
    {
        var client = Substitute.For<ISpotifyClient>();
        var service = CreateService(client);

        var result = await service.SearchTracksAsync("   ");

        Assert.NotNull(result);
        Assert.Empty(result!);
        await client.Search.DidNotReceive().Item(Arg.Any<SearchRequest>());
    }
}
