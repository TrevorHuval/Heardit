using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.Services;
using Heardit.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Heardit.Tests;

public class ReviewServiceTests
{
    [Fact]
    public async Task AddOrUpdateReviewAsync_NewReview_PersistsAndReturnsAdded()
    {
        using var db = new SqliteInMemoryDb();
        var user = TestData.MakeUser("alice");
        var userManager = TestData.SubstituteUserManager();

        ReviewUpsertStatus status;
        using (var ctx = db.CreateContext())
        {
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();               // user is now tracked as Unchanged by ctx
            userManager.FindByIdAsync(user.Id).Returns(user);

            var service = new ReviewService(ctx, userManager);
            status = await service.AddOrUpdateReviewAsync("Loved it", 4.5m, "song1", "Karma Police", user.Id);
        }

        Assert.Equal(ReviewUpsertStatus.Added, status);
        using (var verify = db.CreateContext())
        {
            var review = await verify.Reviews.Include(r => r.User).SingleAsync();
            Assert.Equal("Loved it", review.WrittenReview);
            Assert.Equal(4.5m, review.Rating);
            Assert.Equal("song1", review.SongId);
            Assert.Equal("Karma Police", review.SongName);
            Assert.Equal(user.Id, review.User.Id);
            Assert.NotEqual(default, review.CreatedAt);
            Assert.Null(review.UpdatedAt);
        }
    }

    [Fact]
    public async Task AddOrUpdateReviewAsync_ExistingReview_UpdatesInPlace()
    {
        using var db = new SqliteInMemoryDb();
        var user = TestData.MakeUser("alice");
        var userManager = TestData.SubstituteUserManager();
        userManager.FindByIdAsync(user.Id).Returns(user);

        using (var ctx = db.CreateContext())
        {
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();
            var service = new ReviewService(ctx, userManager);
            await service.AddOrUpdateReviewAsync("first take", 4.0m, "song1", "Karma Police", user.Id);
        }

        ReviewUpsertStatus status;
        using (var ctx = db.CreateContext())
        {
            var service = new ReviewService(ctx, userManager);
            status = await service.AddOrUpdateReviewAsync("changed my mind", 9.0m, "song1", "Karma Police", user.Id);
        }

        Assert.Equal(ReviewUpsertStatus.Updated, status);
        using (var verify = db.CreateContext())
        {
            var review = await verify.Reviews.SingleAsync();   // still exactly one row
            Assert.Equal("changed my mind", review.WrittenReview);
            Assert.Equal(9.0m, review.Rating);
            Assert.NotNull(review.UpdatedAt);
        }
    }

    [Fact]
    public async Task AddOrUpdateReviewAsync_UserMissing_PersistsNothing()
    {
        using var db = new SqliteInMemoryDb();
        var userManager = TestData.SubstituteUserManager();
        userManager.FindByIdAsync(Arg.Any<string>()).Returns((HearditUser?)null);

        ReviewUpsertStatus status;
        using (var ctx = db.CreateContext())
        {
            var service = new ReviewService(ctx, userManager);
            status = await service.AddOrUpdateReviewAsync("orphan", 3.0m, "song1", "Song", "ghost-user");
        }

        Assert.Equal(ReviewUpsertStatus.Failed, status);
        using (var verify = db.CreateContext())
        {
            Assert.Equal(0, await verify.Reviews.CountAsync());
        }
    }

    [Fact]
    public async Task DeleteReviewAsync_Owner_DeletesAndReturnsSongId()
    {
        using var db = new SqliteInMemoryDb();
        var user = TestData.MakeUser("alice");
        string reviewId;
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(user);
            var review = new Review("txt", user, 3.5m, "song9", "Song Nine");
            reviewId = review.ReviewId;
            seed.Reviews.Add(review);
            await seed.SaveChangesAsync();
        }

        var userManager = TestData.SubstituteUserManager();
        ReviewDeleteResult result;
        using (var ctx = db.CreateContext())
        {
            var service = new ReviewService(ctx, userManager);
            result = await service.DeleteReviewAsync(reviewId, user.Id);
        }

        Assert.Equal(ReviewDeleteStatus.Deleted, result.Status);
        Assert.Equal("song9", result.SongId);
        using (var verify = db.CreateContext())
        {
            Assert.Equal(0, await verify.Reviews.CountAsync());
        }
    }

    [Fact]
    public async Task DeleteReviewAsync_NonOwner_ForbiddenAndRowRemains()
    {
        using var db = new SqliteInMemoryDb();
        var owner = TestData.MakeUser("alice");
        string reviewId;
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(owner);
            var review = new Review("txt", owner, 3.5m, "song9", "Song Nine");
            reviewId = review.ReviewId;
            seed.Reviews.Add(review);
            await seed.SaveChangesAsync();
        }

        var userManager = TestData.SubstituteUserManager();
        ReviewDeleteResult result;
        using (var ctx = db.CreateContext())
        {
            var service = new ReviewService(ctx, userManager);
            result = await service.DeleteReviewAsync(reviewId, "intruder-user-id");
        }

        Assert.Equal(ReviewDeleteStatus.Forbidden, result.Status);
        Assert.Equal("song9", result.SongId);
        using (var verify = db.CreateContext())
        {
            Assert.Equal(1, await verify.Reviews.CountAsync());
        }
    }

    [Fact]
    public async Task DeleteReviewAsync_MissingReview_ReturnsNotFound()
    {
        using var db = new SqliteInMemoryDb();
        var userManager = TestData.SubstituteUserManager();

        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, userManager);

        var result = await service.DeleteReviewAsync("no-such-review", "anyone");

        Assert.Equal(ReviewDeleteStatus.NotFound, result.Status);
        Assert.Null(result.SongId);
    }

    [Fact]
    public async Task GetReviewAsync_ReturnsReviewWithUser()
    {
        using var db = new SqliteInMemoryDb();
        var user = TestData.MakeUser("alice");
        string reviewId;
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(user);
            var seeded = new Review("solid", user, 4.0m, "song5", "Song Five");
            reviewId = seeded.ReviewId;
            seed.Reviews.Add(seeded);
            await seed.SaveChangesAsync();
        }

        var userManager = TestData.SubstituteUserManager();
        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, userManager);

        var review = await service.GetReviewAsync(reviewId);

        Assert.NotNull(review);
        Assert.Equal("solid", review!.WrittenReview);
        Assert.Equal("alice", review.User.UserName);
    }

    [Fact]
    public async Task GetReviewAsync_BlankId_ReturnsNull()
    {
        using var db = new SqliteInMemoryDb();
        var userManager = TestData.SubstituteUserManager();
        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, userManager);

        Assert.Null(await service.GetReviewAsync("   "));
    }

    [Fact]
    public async Task GetSongStatsAsync_NoIds_ReturnsEmptyWithoutQuerying()
    {
        using var db = new SqliteInMemoryDb();
        var userManager = TestData.SubstituteUserManager();
        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, userManager);

        Assert.Empty(await service.GetSongStatsAsync(Array.Empty<string>()));
    }

    [Fact]
    public async Task GetSongStatsAsync_AveragesPerSongAndRoundsToOneDecimal()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        var carol = TestData.MakeUser("carol");
        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob, carol);
            // song1 averages 4.6666… → 4.7; song2 has a single review.
            seed.Reviews.Add(new Review("a", alice, 4.0m, "song1", "Song One"));
            seed.Reviews.Add(new Review("b", bob, 5.0m, "song1", "Song One"));
            seed.Reviews.Add(new Review("c", carol, 5.0m, "song1", "Song One"));
            seed.Reviews.Add(new Review("d", alice, 2.5m, "song2", "Song Two"));
            await seed.SaveChangesAsync();
        }

        var userManager = TestData.SubstituteUserManager();
        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, userManager);

        var stats = await service.GetSongStatsAsync(new[] { "song1", "song2" });

        Assert.Equal(2, stats.Count);
        Assert.Equal(4.7m, stats["song1"].Average);
        Assert.Equal(3, stats["song1"].Count);
        Assert.Equal(2.5m, stats["song2"].Average);
        Assert.Equal(1, stats["song2"].Count);
    }

    [Fact]
    public async Task GetSongStatsAsync_UnknownAndBlankIds_AreSimplyAbsent()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(alice);
            seed.Reviews.Add(new Review("a", alice, 4.0m, "song1", "Song One"));
            await seed.SaveChangesAsync();
        }

        var userManager = TestData.SubstituteUserManager();
        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, userManager);

        // Duplicates and blanks are filtered out; an unreviewed id yields no entry rather than a zero.
        var stats = await service.GetSongStatsAsync(new[] { "song1", "song1", "never-reviewed", "" });

        Assert.Single(stats);
        Assert.True(stats.ContainsKey("song1"));
        Assert.False(stats.ContainsKey("never-reviewed"));
    }
}
