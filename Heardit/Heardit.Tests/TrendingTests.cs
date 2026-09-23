using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.Services;
using Heardit.Tests.Infrastructure;

namespace Heardit.Tests;

public class TrendingTests
{
    private static Song SongNamed(string id) => new(id, $"Title {id}", $"Artist {id}", "Album", $"https://img/{id}");

    private static Review ReviewOf(HearditUser author, string songId, decimal rating, DateTime createdAt)
    {
        var review = new Review($"{author.UserName} on {songId}", author, rating, songId, songId.ToUpperInvariant())
        {
            CreatedAt = createdAt
        };
        return review;
    }

    private static ReviewService Service(HearditDbContext ctx) => new(ctx, TestData.SubstituteUserManager());

    [Fact]
    public async Task GetTrendingAsync_EmptyDatabase_ReturnsNothing()
    {
        using var db = new SqliteInMemoryDb();
        using var ctx = db.CreateContext();

        var result = await Service(ctx).GetTrendingAsync();

        Assert.Empty(result.Songs);
    }

    [Fact]
    public async Task GetTrendingAsync_BusyWeek_RanksByReviewsPlusLikes_WithinTheWindow()
    {
        using var db = new SqliteInMemoryDb();
        var now = DateTime.UtcNow;
        var users = Enumerable.Range(0, 4).Select(i => TestData.MakeUser($"u{i}")).ToList();

        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(users);
            foreach (var id in new[] { "a", "b", "c", "d", "old" })
            {
                seed.Songs.Add(SongNamed(id));
            }

            // "a": 2 recent reviews, no likes → score 2.
            seed.Reviews.Add(ReviewOf(users[0], "a", 8m, now.AddDays(-1)));
            seed.Reviews.Add(ReviewOf(users[1], "a", 6m, now.AddDays(-2)));

            // "b": 1 recent review with 3 likes → score 4, beats "a".
            var liked = ReviewOf(users[0], "b", 9m, now.AddDays(-3));
            seed.Reviews.Add(liked);

            // "c" and "d": one recent review each → score 1; "d" is more recent, so it breaks the tie.
            seed.Reviews.Add(ReviewOf(users[2], "c", 5m, now.AddDays(-4)));
            seed.Reviews.Add(ReviewOf(users[2], "d", 7m, now.AddHours(-2)));

            // "old": lots of activity, but all of it before the window.
            foreach (var u in users)
            {
                seed.Reviews.Add(ReviewOf(u, "old", 10m, now.AddDays(-30)));
            }

            await seed.SaveChangesAsync();

            foreach (var u in users.Skip(1))
            {
                seed.ReviewLikes.Add(new ReviewLike { ReviewId = liked.ReviewId, UserId = u.Id, CreatedAt = now });
            }

            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var result = await Service(ctx).GetTrendingAsync(take: 8);

        Assert.False(result.IsAllTime);
        Assert.Equal(new[] { "b", "a", "d", "c" }, result.Songs.Select(s => s.Song.Id));

        // The shelf shows each song's overall rating, not just the week's.
        var a = result.Songs.Single(s => s.Song.Id == "a");
        Assert.Equal(7.0m, a.Average);
        Assert.Equal(2, a.ReviewCount);
        Assert.Equal("https://img/a", a.Song.AlbumArt);
    }

    [Fact]
    public async Task GetTrendingAsync_QuietWeek_FallsBackToAllTime()
    {
        using var db = new SqliteInMemoryDb();
        var now = DateTime.UtcNow;
        var users = Enumerable.Range(0, 3).Select(i => TestData.MakeUser($"u{i}")).ToList();

        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(users);
            foreach (var id in new[] { "recent", "classic", "deep" })
            {
                seed.Songs.Add(SongNamed(id));
            }

            seed.Reviews.Add(ReviewOf(users[0], "recent", 7m, now.AddDays(-1)));
            foreach (var u in users)
            {
                seed.Reviews.Add(ReviewOf(u, "classic", 9m, now.AddDays(-60)));
            }

            seed.Reviews.Add(ReviewOf(users[1], "deep", 6m, now.AddDays(-90)));
            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var result = await Service(ctx).GetTrendingAsync(take: 8);

        // One song in the last week can't fill a shelf, so the heading switches to all-time.
        Assert.True(result.IsAllTime);
        Assert.Equal("classic", result.Songs[0].Song.Id);
        Assert.Equal(3, result.Songs.Count);
    }

    [Fact]
    public async Task GetTrendingAsync_RespectsTake()
    {
        using var db = new SqliteInMemoryDb();
        var user = TestData.MakeUser("u");

        using (var seed = db.CreateContext())
        {
            seed.Users.Add(user);
            for (var i = 0; i < 10; i++)
            {
                seed.Songs.Add(SongNamed($"s{i}"));
                seed.Reviews.Add(ReviewOf(user, $"s{i}", 5m, DateTime.UtcNow.AddHours(-i)));
            }

            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var result = await Service(ctx).GetTrendingAsync(take: 6);

        Assert.Equal(6, result.Songs.Count);
        Assert.False(result.IsAllTime);
    }

    [Fact]
    public async Task GetRecentReviewsAsync_EveryoneNewestFirst_Paged()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        var now = DateTime.UtcNow;

        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob);
            for (var i = 0; i < 25; i++)
            {
                seed.Reviews.Add(ReviewOf(i % 2 == 0 ? alice : bob, $"s{i}", 5m, now.AddMinutes(-i)));
            }

            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var service = Service(ctx);
        var first = await service.GetRecentReviewsAsync();
        var second = await service.GetRecentReviewsAsync(2);

        Assert.Equal(20, first.Count);
        Assert.True(first.HasNext);
        Assert.Equal("s0", first.Items[0].SongId);
        Assert.Equal(new[] { "alice", "bob" }, first.Items.Take(2).Select(r => r.User.UserName));
        Assert.Equal(5, second.Count);
        Assert.False(second.HasNext);
    }
}
