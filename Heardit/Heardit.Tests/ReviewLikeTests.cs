using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.Services;
using Heardit.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Tests;

public class ReviewLikeTests
{
    /// <summary>Seeds one review by <paramref name="author"/>, plus any onlookers, and returns its id.</summary>
    private static async Task<string> SeedReviewAsync(SqliteInMemoryDb db, HearditUser author, params HearditUser[] others)
    {
        using var seed = db.CreateContext();
        seed.Users.Add(author);
        seed.Users.AddRange(others);

        var review = new Review("solid", author, 7m, "song1", "Song One");
        seed.Reviews.Add(review);
        await seed.SaveChangesAsync();
        return review.ReviewId;
    }

    [Fact]
    public async Task ToggleLikeAsync_FirstCallLikes_SecondCallUnlikes()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        var reviewId = await SeedReviewAsync(db, alice, bob);

        bool? liked;
        using (var ctx = db.CreateContext())
        {
            liked = await new ReviewService(ctx, TestData.SubstituteUserManager()).ToggleLikeAsync(reviewId, bob.Id);
        }

        Assert.True(liked);
        using (var verify = db.CreateContext())
        {
            var like = await verify.ReviewLikes.SingleAsync();
            Assert.Equal(reviewId, like.ReviewId);
            Assert.Equal(bob.Id, like.UserId);
            Assert.NotEqual(default, like.CreatedAt);
        }

        bool? unliked;
        using (var ctx = db.CreateContext())
        {
            unliked = await new ReviewService(ctx, TestData.SubstituteUserManager()).ToggleLikeAsync(reviewId, bob.Id);
        }

        Assert.False(unliked);
        using (var verify = db.CreateContext())
        {
            Assert.Equal(0, await verify.ReviewLikes.CountAsync());
        }
    }

    [Fact]
    public async Task ToggleLikeAsync_OwnReview_IsAllowed()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var reviewId = await SeedReviewAsync(db, alice);

        using var ctx = db.CreateContext();
        var liked = await new ReviewService(ctx, TestData.SubstituteUserManager()).ToggleLikeAsync(reviewId, alice.Id);

        Assert.True(liked);
    }

    [Fact]
    public async Task ToggleLikeAsync_MissingReview_ReturnsNullAndStoresNothing()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        await SeedReviewAsync(db, alice);

        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, TestData.SubstituteUserManager());

        Assert.Null(await service.ToggleLikeAsync("no-such-review", alice.Id));
        Assert.Null(await service.ToggleLikeAsync("   ", alice.Id));
        Assert.Equal(0, await ctx.ReviewLikes.CountAsync());
    }

    [Fact]
    public async Task GetLikeStatsAsync_CountsEveryLikeButFlagsOnlyMine()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        var carol = TestData.MakeUser("carol");
        var liked = new Review("liked", alice, 8m, "song1", "Song One");
        var untouched = new Review("untouched", alice, 4m, "song2", "Song Two");
        var likedByOthers = new Review("popular", carol, 9m, "song3", "Song Three");

        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob, carol);
            seed.Reviews.AddRange(liked, untouched, likedByOthers);
            seed.ReviewLikes.Add(new ReviewLike { ReviewId = liked.ReviewId, UserId = bob.Id, CreatedAt = DateTime.UtcNow });
            seed.ReviewLikes.Add(new ReviewLike { ReviewId = liked.ReviewId, UserId = carol.Id, CreatedAt = DateTime.UtcNow });
            seed.ReviewLikes.Add(new ReviewLike { ReviewId = likedByOthers.ReviewId, UserId = alice.Id, CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, TestData.SubstituteUserManager());

        var stats = await service.GetLikeStatsAsync(
            new[] { liked.ReviewId, untouched.ReviewId, likedByOthers.ReviewId }, bob.Id);

        // An unliked review has no entry at all, exactly like GetSongStatsAsync's unreviewed songs.
        Assert.Equal(2, stats.Count);
        Assert.Equal(2, stats[liked.ReviewId].Count);
        Assert.True(stats[liked.ReviewId].LikedByMe);
        Assert.Equal(1, stats[likedByOthers.ReviewId].Count);
        Assert.False(stats[likedByOthers.ReviewId].LikedByMe);
        Assert.False(stats.ContainsKey(untouched.ReviewId));
    }

    [Fact]
    public async Task GetLikeStatsAsync_NoIdsOrNoReader_StillBehaves()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var review = new Review("liked", alice, 8m, "song1", "Song One");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(alice);
            seed.Reviews.Add(review);
            seed.ReviewLikes.Add(new ReviewLike { ReviewId = review.ReviewId, UserId = alice.Id, CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var service = new ReviewService(ctx, TestData.SubstituteUserManager());

        Assert.Empty(await service.GetLikeStatsAsync(Array.Empty<string>(), alice.Id));

        // A signed-out reader still sees the tally, just none of it as their own.
        var anonymous = await service.GetLikeStatsAsync(new[] { review.ReviewId, "" }, null);
        Assert.Equal(1, anonymous[review.ReviewId].Count);
        Assert.False(anonymous[review.ReviewId].LikedByMe);
    }

    [Fact]
    public async Task DeletingAReview_TakesItsLikesWithIt()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        var reviewId = await SeedReviewAsync(db, alice, bob);

        using (var ctx = db.CreateContext())
        {
            var service = new ReviewService(ctx, TestData.SubstituteUserManager());
            await service.ToggleLikeAsync(reviewId, bob.Id);
            await service.ToggleLikeAsync(reviewId, alice.Id);
        }

        using (var ctx = db.CreateContext())
        {
            var result = await new ReviewService(ctx, TestData.SubstituteUserManager())
                .DeleteReviewAsync(reviewId, alice.Id);
            Assert.Equal(ReviewDeleteStatus.Deleted, result.Status);
        }

        using (var verify = db.CreateContext())
        {
            Assert.Equal(0, await verify.ReviewLikes.CountAsync());
        }
    }

    [Fact]
    public async Task DeletingAUser_TakesTheLikesTheyGaveWithThem()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        var reviewId = await SeedReviewAsync(db, alice, bob);

        using (var ctx = db.CreateContext())
        {
            await new ReviewService(ctx, TestData.SubstituteUserManager()).ToggleLikeAsync(reviewId, bob.Id);
        }

        using (var ctx = db.CreateContext())
        {
            ctx.Users.Remove(await ctx.Users.SingleAsync(u => u.Id == bob.Id));
            await ctx.SaveChangesAsync();
        }

        using (var verify = db.CreateContext())
        {
            // Alice's review survives bob's account; the like he left does not.
            Assert.Equal(1, await verify.Reviews.CountAsync());
            Assert.Equal(0, await verify.ReviewLikes.CountAsync());
        }
    }
}
