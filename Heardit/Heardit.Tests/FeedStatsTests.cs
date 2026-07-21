using Heardit.Services;
using Heardit.ViewModels;
using NSubstitute;

namespace Heardit.Tests;

public class FeedStatsTests
{
    private static FeedItemViewModel Item(string id) => new() { Id = id, Name = $"Track {id}" };

    [Fact]
    public async Task ApplyAsync_FillsEachItemFromOneBatchedLookup()
    {
        var reviewService = Substitute.For<IReviewService>();
        reviewService.GetSongStatsAsync(Arg.Any<IEnumerable<string>>()).Returns(
            new Dictionary<string, SongReviewStats>
            {
                ["song1"] = new(4.7m, 3),
                ["song2"] = new(2.5m, 1)
            });

        var feed = new List<FeedItemViewModel> { Item("song1"), Item("song2"), Item("song3") };

        await FeedStats.ApplyAsync(reviewService, feed);

        Assert.Equal(4.7m, feed[0].AverageRating);
        Assert.Equal(3, feed[0].ReviewCount);
        Assert.Equal(2.5m, feed[1].AverageRating);
        Assert.Equal(1, feed[1].ReviewCount);
        // An unreviewed track keeps its untouched defaults so the card shows the "be the first" prompt.
        Assert.Null(feed[2].AverageRating);
        Assert.Equal(0, feed[2].ReviewCount);
        await reviewService.Received(1).GetSongStatsAsync(Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task ApplyAsync_EmptyFeed_DoesNotQuery()
    {
        var reviewService = Substitute.For<IReviewService>();

        await FeedStats.ApplyAsync(reviewService, Array.Empty<FeedItemViewModel>());

        await reviewService.DidNotReceive().GetSongStatsAsync(Arg.Any<IEnumerable<string>>());
    }
}
