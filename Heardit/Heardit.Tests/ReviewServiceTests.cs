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
    public async Task AddReviewAsync_UserExists_PersistsReview()
    {
        using var db = new SqliteInMemoryDb();
        var user = TestData.MakeUser("alice");
        var userManager = TestData.SubstituteUserManager();

        using (var ctx = db.CreateContext())
        {
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();               // user is now tracked as Unchanged by ctx
            userManager.FindByIdAsync(user.Id).Returns(user);

            var service = new ReviewService(ctx, userManager);
            await service.AddReviewAsync("Loved it", 4.5m, "song1", "Karma Police", user.Id);
        }

        using (var verify = db.CreateContext())
        {
            var review = await verify.Reviews.Include(r => r.User).SingleAsync();
            Assert.Equal("Loved it", review.WrittenReview);
            Assert.Equal(4.5m, review.Rating);
            Assert.Equal("song1", review.SongId);
            Assert.Equal("Karma Police", review.SongName);
            Assert.Equal(user.Id, review.User.Id);
        }
    }

    [Fact]
    public async Task AddReviewAsync_UserMissing_PersistsNothing()
    {
        using var db = new SqliteInMemoryDb();
        var userManager = TestData.SubstituteUserManager();
        userManager.FindByIdAsync(Arg.Any<string>()).Returns((HearditUser?)null);

        using (var ctx = db.CreateContext())
        {
            var service = new ReviewService(ctx, userManager);
            await service.AddReviewAsync("orphan", 3.0m, "song1", "Song", "ghost-user");
        }

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
}
