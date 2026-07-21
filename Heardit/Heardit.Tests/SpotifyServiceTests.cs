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

    [Fact]
    public async Task GetNewReleaseTracksAsync_ReturnsLeadTrackOfEachAlbumAndCachesThem()
    {
        var client = Substitute.For<ISpotifyClient>();
#pragma warning disable CS0618 // SDK-obsolete endpoints; SpotifyService suppresses them too.
        client.Browse.GetNewReleases().Returns(new NewReleasesResponse
        {
            Albums = new Paging<SimpleAlbum, NewReleasesResponse>
            {
                Items = new List<SimpleAlbum> { new() { Id = "album1" }, new() { Id = "album2" } }
            }
        });
        client.Albums.GetSeveral(Arg.Any<AlbumsRequest>()).Returns(new AlbumsResponse
        {
            Albums = new List<FullAlbum>
            {
                new()
                {
                    Tracks = new Paging<SimpleTrack>
                    {
                        Items = new List<SimpleTrack> { new() { Id = "t1", Name = "Lead One" }, new() { Id = "t2", Name = "Deep Cut" } }
                    }
                },
                new()
                {
                    Tracks = new Paging<SimpleTrack>
                    {
                        Items = new List<SimpleTrack> { new() { Id = "t3", Name = "Lead Two" } }
                    }
                }
            }
        });
#pragma warning restore CS0618
        var service = CreateService(client);

        var first = await service.GetNewReleaseTracksAsync();
        var second = await service.GetNewReleaseTracksAsync();

        Assert.NotNull(first);
        Assert.Equal(new[] { "Lead One", "Lead Two" }, first!.Select(t => t.Name));
        Assert.Same(first, second);
#pragma warning disable CS0618
        await client.Browse.Received(1).GetNewReleases();
#pragma warning restore CS0618
    }

    [Fact]
    public async Task GetNewReleaseTracksAsync_ApiException_ReturnsNull()
    {
        var client = Substitute.For<ISpotifyClient>();
        // GetNewReleases is SDK-obsolete but SpotifyService still calls it (suppressed there too); mirror that here.
#pragma warning disable CS0618
        client.Browse.GetNewReleases().Returns<Task<NewReleasesResponse>>(_ => throw new APIException("simulated Spotify failure"));
#pragma warning restore CS0618
        var service = CreateService(client);

        // Null is "couldn't reach Spotify" — the views say so instead of claiming there are no releases.
        Assert.Null(await service.GetNewReleaseTracksAsync());
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
