using Heardit.Models;
using Heardit.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Heardit.Tests;

/// <summary>
/// Deleting an account used to throw: the follow rows were NO ACTION and a review's author was optional,
/// so rows could be left pointing at nobody (and then NRE the review partial). These pin the mapping.
/// Cascades are declared on the model, so the SQLite schema built by EnsureCreated carries them too.
/// </summary>
public class CascadeDeleteTests
{
    [Fact]
    public async Task DeletingAUser_TakesTheirReviewsAndFollowsWithThem()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");

        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob);
            seed.Reviews.Add(new Review("mine", alice, 5m, "song1", "Song One"));
            seed.Reviews.Add(new Review("theirs", bob, 4m, "song1", "Song One"));
            seed.Follows.Add(new Follows { UserId = alice.Id, FollowerId = bob.Id });   // bob follows alice
            seed.Follows.Add(new Follows { UserId = bob.Id, FollowerId = alice.Id });   // alice follows bob
            await seed.SaveChangesAsync();
        }

        using (var ctx = db.CreateContext())
        {
            ctx.Users.Remove(await ctx.Users.SingleAsync(u => u.Id == alice.Id));
            await ctx.SaveChangesAsync();
        }

        using (var verify = db.CreateContext())
        {
            // Alice's review is gone; bob's survives. Both follow rows went, whichever end alice was on.
            var review = Assert.Single(await verify.Reviews.ToListAsync());
            Assert.Equal("theirs", review.WrittenReview);
            Assert.Empty(await verify.Follows.ToListAsync());
            Assert.Single(await verify.Users.ToListAsync());
        }
    }

    [Fact]
    public async Task AReviewCannotBeSavedWithoutAnAuthor()
    {
        using var db = new SqliteInMemoryDb();
        using var ctx = db.CreateContext();

        ctx.Reviews.Add(new Review
        {
            ReviewId = Guid.NewGuid().ToString(),
            SongId = "song1",
            SongName = "Song One",
            WrittenReview = "nobody wrote this",
            Rating = 5m,
            CreatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }
}
