using System.Security.Claims;
using Heardit.Areas.Identity.Data;
using Heardit.Controllers;
using Heardit.Models;
using Heardit.Services;
using Heardit.Tests.Infrastructure;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SpotifyAPI.Web;

namespace Heardit.Tests;

public class FollowingFeedTests
{
    /// <summary>A review with a chosen timestamp, so ordering assertions don't race the clock.</summary>
    private static Review ReviewAt(HearditUser author, string songId, DateTime createdAt)
    {
        var review = new Review($"{author.UserName} on {songId}", author, 6m, songId, songId.ToUpperInvariant());
        review.CreatedAt = createdAt;
        return review;
    }

    [Fact]
    public async Task GetFollowingFeedAsync_ReturnsOnlyFollowedAuthors_NewestFirst()
    {
        using var db = new SqliteInMemoryDb();
        var reader = TestData.MakeUser("reader");
        var followed = TestData.MakeUser("followed");
        var stranger = TestData.MakeUser("stranger");
        var baseline = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(reader, followed, stranger);
            seed.Follows.Add(new Follows { UserId = followed.Id, FollowerId = reader.Id });
            seed.Reviews.Add(ReviewAt(followed, "older", baseline));
            seed.Reviews.Add(ReviewAt(followed, "newer", baseline.AddHours(1)));
            seed.Reviews.Add(ReviewAt(stranger, "unfollowed", baseline.AddHours(2)));
            seed.Reviews.Add(ReviewAt(reader, "mine", baseline.AddHours(3)));   // you don't follow yourself
            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var page = await new ReviewService(ctx, TestData.SubstituteUserManager()).GetFollowingFeedAsync(reader.Id);

        Assert.Equal(new[] { "newer", "older" }, page.Items.Select(r => r.SongId));
        Assert.Equal("followed", page.Items[0].User.UserName);   // the author comes along for the ride
        Assert.False(page.HasNext);
    }

    [Fact]
    public async Task GetFollowingFeedAsync_FollowingNobody_IsEmpty()
    {
        using var db = new SqliteInMemoryDb();
        var reader = TestData.MakeUser("reader");
        var stranger = TestData.MakeUser("stranger");

        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(reader, stranger);
            seed.Reviews.Add(ReviewAt(stranger, "song1", DateTime.UtcNow));
            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, TestData.SubstituteUserManager());

        Assert.Empty((await service.GetFollowingFeedAsync(reader.Id)).Items);
        Assert.Empty((await service.GetFollowingFeedAsync("")).Items);
    }

    [Fact]
    public async Task GetFollowingFeedAsync_PagesTwentyAtATime()
    {
        using var db = new SqliteInMemoryDb();
        var reader = TestData.MakeUser("reader");
        var followed = TestData.MakeUser("followed");
        var baseline = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(reader, followed);
            seed.Follows.Add(new Follows { UserId = followed.Id, FollowerId = reader.Id });
            // 21 reviews: song00 oldest … song20 newest.
            for (var i = 0; i < 21; i++)
            {
                seed.Reviews.Add(ReviewAt(followed, $"song{i:00}", baseline.AddMinutes(i)));
            }
            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, TestData.SubstituteUserManager());

        var first = await service.GetFollowingFeedAsync(reader.Id, 1);
        Assert.Equal(PagedList<Review>.PageSize, first.Count);
        Assert.True(first.HasNext);
        Assert.False(first.HasPrevious);
        Assert.Equal("song20", first.Items[0].SongId);

        var second = await service.GetFollowingFeedAsync(reader.Id, 2);
        Assert.Single(second.Items);
        Assert.Equal("song00", second.Items[0].SongId);   // the oldest one falls onto page two
        Assert.False(second.HasNext);
        Assert.True(second.HasPrevious);
    }

    [Fact]
    public async Task IsFollowingAnyoneAsync_TrueOnlyWhenTheUserIsTheFollower()
    {
        using var db = new SqliteInMemoryDb();
        var follower = TestData.MakeUser("follower");
        var followed = TestData.MakeUser("followed");

        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(follower, followed);
            seed.Follows.Add(new Follows { UserId = followed.Id, FollowerId = follower.Id });
            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var service = new ProfileService(ctx, TestData.SubstituteUserManager());

        Assert.True(await service.IsFollowingAnyoneAsync(follower.Id));
        // Being followed is not following: followed's home should still open on New Releases.
        Assert.False(await service.IsFollowingAnyoneAsync(followed.Id));
        Assert.False(await service.IsFollowingAnyoneAsync(""));
    }

    // Default homepage dependencies: every feed empty, Spotify answering with nothing.
    private static IReviewService EmptyReviewService()
    {
        var reviews = Substitute.For<IReviewService>();
        reviews.GetFollowingFeedAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(PagedList<Review>.Empty());
        reviews.GetRecentReviewsAsync(Arg.Any<int>()).Returns(PagedList<Review>.Empty());
        reviews.GetTrendingAsync(Arg.Any<int>()).Returns(new TrendingResult(Array.Empty<TrendingSong>(), false));
        reviews.GetSongStatsAsync(Arg.Any<IEnumerable<string>>()).Returns(new Dictionary<string, SongReviewStats>());
        reviews.GetLikeStatsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string?>())
            .Returns(new Dictionary<string, ReviewLikeStats>());
        return reviews;
    }

    private static HomeController MakeHomeController(
        IReviewService reviews, IProfileService profiles, string userId,
        IReadOnlyList<TrackSummary>? releases = null, bool spotifyDown = false)
    {
        var spotify = Substitute.For<ISpotifyService>();
        spotify.GetNewReleaseTracksAsync().Returns(spotifyDown ? null : releases ?? Array.Empty<TrackSummary>());

        var listenLater = Substitute.For<IListenLaterService>();
        listenLater.GetSavedSongIdsAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(new HashSet<string>());

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test");
        return new HomeController(spotify, reviews, profiles, listenLater)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static async Task<HomeIndexViewModel> IndexModelAsync(HomeController controller)
    {
        var result = Assert.IsType<ViewResult>(await controller.Index(null));
        return Assert.IsType<HomeIndexViewModel>(result.Model);
    }

    private static PagedList<Review> PageOf(int count, bool hasNext = false)
    {
        var author = TestData.MakeUser("author");
        var items = Enumerable.Range(0, count)
            .Select(i => ReviewAt(author, $"song{i}", DateTime.UtcNow.AddMinutes(-i)))
            .ToList();
        return new PagedList<Review> { Items = items, Page = 1, HasNext = hasNext };
    }

    [Theory]
    [InlineData("following", "Following")]
    [InlineData("FOLLOWING", "Following")]
    [InlineData("new", "NewReleases")]
    public async Task Index_OldTabLinks_RedirectToTheirSeeAllPage(string tab, string expectedAction)
    {
        var controller = MakeHomeController(EmptyReviewService(), Substitute.For<IProfileService>(), "user-1");

        var redirect = Assert.IsType<RedirectToActionResult>(await controller.Index(tab));

        Assert.Equal(expectedAction, redirect.ActionName);
    }

    [Fact]
    public async Task Index_FollowingNobody_SkipsTheFriendsFeed()
    {
        var reviews = EmptyReviewService();
        var profiles = Substitute.For<IProfileService>();
        profiles.IsFollowingAnyoneAsync("user-1").Returns(false);

        var model = await IndexModelAsync(MakeHomeController(reviews, profiles, "user-1"));

        Assert.False(model.FollowsAnyone);
        Assert.Empty(model.FriendReviews);
        await reviews.DidNotReceive().GetFollowingFeedAsync(Arg.Any<string>(), Arg.Any<int>());
    }

    [Theory]
    [InlineData(3, false, 3, false)]   // fits in the preview: no See all
    [InlineData(6, false, 4, true)]    // more on this page than the preview shows
    [InlineData(4, true, 4, true)]     // exactly a preview's worth, but older pages exist
    public async Task Index_PreviewsFriendReviews(int onPage, bool hasNext, int expectedShown, bool expectMore)
    {
        var reviews = EmptyReviewService();
        reviews.GetFollowingFeedAsync("user-1", Arg.Any<int>()).Returns(PageOf(onPage, hasNext));
        var profiles = Substitute.For<IProfileService>();
        profiles.IsFollowingAnyoneAsync("user-1").Returns(true);

        var model = await IndexModelAsync(MakeHomeController(reviews, profiles, "user-1"));

        Assert.Equal(expectedShown, model.FriendReviews.Count);
        Assert.Equal(expectMore, model.MoreFriendReviews);
    }

    [Fact]
    public async Task Index_SpotifyDown_StillRendersTheOtherSections()
    {
        var reviews = EmptyReviewService();
        reviews.GetRecentReviewsAsync(Arg.Any<int>()).Returns(PageOf(7));
        var controller = MakeHomeController(reviews, Substitute.For<IProfileService>(), "user-1", spotifyDown: true);

        var model = await IndexModelAsync(controller);

        Assert.True(model.SpotifyUnavailable);
        Assert.Empty(model.NewReleases);
        Assert.Equal(5, model.LatestReviews.Count);
    }

    [Fact]
    public async Task Index_CapsTheNewReleasesShelf_AndBatchesLikeStatsOnce()
    {
        var reviews = EmptyReviewService();
        reviews.GetRecentReviewsAsync(Arg.Any<int>()).Returns(PageOf(3));
        var releases = Enumerable.Range(0, 20)
            .Select(i => new TrackSummary($"t{i}", $"Track {i}", "Artist", $"https://img/{i}"))
            .ToList();

        var model = await IndexModelAsync(
            MakeHomeController(reviews, Substitute.For<IProfileService>(), "user-1", releases));

        Assert.Equal(8, model.NewReleases.Count);
        Assert.Equal("https://img/0", model.NewReleases[0].ImageUrl);
        await reviews.Received(1).GetLikeStatsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Following_EmptyForSomeoneWhoFollowsNobody_SaysHowToStart()
    {
        var profiles = Substitute.For<IProfileService>();
        profiles.IsFollowingAnyoneAsync("user-1").Returns(false);
        var controller = MakeHomeController(EmptyReviewService(), profiles, "user-1");

        var view = Assert.IsType<ViewResult>(await controller.Following());
        var model = Assert.IsType<ReviewFeedViewModel>(view.Model);

        Assert.Equal("ReviewFeed", view.ViewName);
        Assert.Contains("not following anyone", model.EmptyMessage);
    }
}
