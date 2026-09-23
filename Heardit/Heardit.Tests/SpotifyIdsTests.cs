using Heardit.Controllers;
using Heardit.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Heardit.Tests;

public class SpotifyIdsTests
{
    [Theory]
    [InlineData("3HfEgAaf0koxBpBB8NvGda", true)]
    [InlineData("3HfEgAaf0koxBpBB8NvGd", false)]          // 21 chars
    [InlineData("3HfEgAaf0koxBpBB8NvGdaX", false)]        // 23 chars
    [InlineData("3HfEgAaf0koxBpBB8NvG-a", false)]         // not base62
    [InlineData("3HfEgAaf0koxBpBB8NvGd\n", false)]        // a line break can't sneak in
    [InlineData("\"><script>alert(1)</script>", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTrackId_AcceptsOnlyTwentyTwoBase62Characters(string? value, bool expected)
    {
        Assert.Equal(expected, SpotifyIds.IsTrackId(value));
    }

    [Theory]
    [InlineData("not-a-track")]
    [InlineData("\"><script>alert(1)</script>")]
    [InlineData("")]
    public async Task SongPage_MalformedId_IsNotFoundWithoutAnyLookup(string songId)
    {
        var songs = Substitute.For<ISongService>();
        var controller = new SongsController(
            songs,
            Substitute.For<IReviewService>(),
            Substitute.For<IListenLaterService>(),
            Substitute.For<IProfileService>());

        var result = await controller.Index(songId);

        Assert.IsType<NotFoundResult>(result);
        await songs.DidNotReceiveWithAnyArgs().GetSongPageAsync(default!, default, default);
    }
}
