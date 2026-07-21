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

    private static HomeController MakeHomeController(IReviewService reviews, IProfileService profiles, string userId)
    {
        var spotify = Substitute.For<ISpotifyService>();
        spotify.GetNewReleaseTracksAsync().Returns(Array.Empty<SimpleTrack>());

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

    private static async Task<HomeIndexViewModel> IndexModelAsync(HomeController controller, string? tab)
    {
        var result = Assert.IsType<ViewResult>(await controller.Index(tab));
        return Assert.IsType<HomeIndexViewModel>(result.Model);
    }

    [Theory]
    [InlineData(true, null, true)]        // follows someone, no tab asked for → Following
    [InlineData(false, null, false)]      // follows nobody → New Releases leads
    [InlineData(true, "new", false)]      // an explicit tab always wins
    [InlineData(false, "following", true)]
    public async Task Index_DefaultsToFollowingOnlyWhenTheUserFollowsSomeone(bool followsAnyone, string? tab, bool expectFollowing)
    {
        var reviews = Substitute.For<IReviewService>();
        reviews.GetFollowingFeedAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(PagedList<Review>.Empty());
        reviews.GetLikeStatsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string?>())
            .Returns(new Dictionary<string, ReviewLikeStats>());

        var profiles = Substitute.For<IProfileService>();
        profiles.IsFollowingAnyoneAsync("user-1").Returns(followsAnyone);

        var model = await IndexModelAsync(MakeHomeController(reviews, profiles, "user-1"), tab);

        Assert.Equal(expectFollowing, model.ShowingFollowing);
        Assert.Equal(followsAnyone, model.FollowsAnyone);
    }

    [Fact]
    public async Task Index_NewReleasesTab_NeverQueriesTheFollowingFeed()
    {
        var reviews = Substitute.For<IReviewService>();
        reviews.GetSongStatsAsync(Arg.Any<IEnumerable<string>>())
            .Returns(new Dictionary<string, SongReviewStats>());

        var profiles = Substitute.For<IProfileService>();
        profiles.IsFollowingAnyoneAsync("user-1").Returns(true);

        var model = await IndexModelAsync(MakeHomeController(reviews, profiles, "user-1"), "new");

        Assert.False(model.ShowingFollowing);
        await reviews.DidNotReceive().GetFollowingFeedAsync(Arg.Any<string>(), Arg.Any<int>());
    }
}
