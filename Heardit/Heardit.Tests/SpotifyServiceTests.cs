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

    private static SpotifyService CreateService(ISpotifyClient client) =>
        new(client, new MemoryCache(new MemoryCacheOptions()), NullLogger<SpotifyService>.Instance);

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
    public async Task GetNewReleaseTracksAsync_ApiException_ReturnsEmpty()
    {
        var client = Substitute.For<ISpotifyClient>();
        // GetNewReleases is SDK-obsolete but SpotifyService still calls it (suppressed there too); mirror that here.
#pragma warning disable CS0618
        client.Browse.GetNewReleases().Returns<Task<NewReleasesResponse>>(_ => throw new APIException("simulated Spotify failure"));
#pragma warning restore CS0618
        var service = CreateService(client);

        var result = await service.GetNewReleaseTracksAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchTracksAsync_ApiException_ReturnsEmpty()
    {
        var client = Substitute.For<ISpotifyClient>();
        client.Search.Item(Arg.Any<SearchRequest>()).Returns<Task<SearchResponse>>(_ => throw new APIException("simulated Spotify failure"));
        var service = CreateService(client);

        var result = await service.SearchTracksAsync("radiohead");

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchTracksAsync_BlankQuery_ShortCircuitsWithoutCallingSpotify()
    {
        var client = Substitute.For<ISpotifyClient>();
        var service = CreateService(client);

        var result = await service.SearchTracksAsync("   ");

        Assert.Empty(result);
        await client.Search.DidNotReceive().Item(Arg.Any<SearchRequest>());
    }
}
