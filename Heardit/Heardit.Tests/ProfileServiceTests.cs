using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.Services;
using Heardit.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Heardit.Tests;

public class ProfileServiceTests
{
    [Fact]
    public async Task GetProfileAsync_ReturnsCountsReviewsAndFollowState()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        var carol = TestData.MakeUser("carol");
        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob, carol);
            // bob and carol follow alice; alice follows bob.
            seed.Follows.Add(new Follows { UserId = alice.Id, FollowerId = bob.Id });
            seed.Follows.Add(new Follows { UserId = alice.Id, FollowerId = carol.Id });
            seed.Follows.Add(new Follows { UserId = bob.Id, FollowerId = alice.Id });
            seed.Reviews.Add(new Review("nice", alice, 4.0m, "song1", "Song One"));
            await seed.SaveChangesAsync();
        }

        var userManager = TestData.SubstituteUserManager();
        userManager.FindByNameAsync("alice").Returns(alice);

        using var ctx = db.CreateContext();
        var service = new ProfileService(ctx, userManager);
        var profile = await service.GetProfileAsync("alice", bob.Id);

        Assert.NotNull(profile);
        Assert.Equal(2, profile!.FollowersCount);   // bob, carol follow alice
        Assert.Equal(1, profile.FollowingCount);     // alice follows bob
        Assert.Single(profile.Reviews);
        Assert.True(profile.IsFollowing);            // current user (bob) follows alice
    }

    [Fact]
    public async Task GetProfileAsync_UnknownUser_ReturnsNull()
    {
        using var db = new SqliteInMemoryDb();
        var userManager = TestData.SubstituteUserManager();
        userManager.FindByNameAsync("ghost").Returns((HearditUser?)null);

        using var ctx = db.CreateContext();
        var service = new ProfileService(ctx, userManager);

        Assert.Null(await service.GetProfileAsync("ghost", null));
    }

    [Fact]
    public async Task FollowAsync_IsIdempotent()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob);
            await seed.SaveChangesAsync();
        }

        var userManager = TestData.SubstituteUserManager();
        userManager.FindByIdAsync(alice.Id).Returns(alice);

        using (var ctx = db.CreateContext())
        {
            var service = new ProfileService(ctx, userManager);
            var name1 = await service.FollowAsync(alice.Id, bob.Id);
            var name2 = await service.FollowAsync(alice.Id, bob.Id);
            Assert.Equal("alice", name1);
            Assert.Equal("alice", name2);
        }

        using (var verify = db.CreateContext())
        {
            Assert.Equal(1, await verify.Follows.CountAsync());
        }
    }

    [Fact]
    public async Task FollowAsync_Self_AddsNothing()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(alice);
            await seed.SaveChangesAsync();
        }

        var userManager = TestData.SubstituteUserManager();
        userManager.FindByIdAsync(alice.Id).Returns(alice);

        using (var ctx = db.CreateContext())
        {
            var service = new ProfileService(ctx, userManager);
            var name = await service.FollowAsync(alice.Id, alice.Id);
            Assert.Equal("alice", name);
        }

        using (var verify = db.CreateContext())
        {
            Assert.Equal(0, await verify.Follows.CountAsync());
        }
    }

    [Fact]
    public async Task FollowAsync_UnknownTarget_ReturnsNull()
    {
        using var db = new SqliteInMemoryDb();
        var userManager = TestData.SubstituteUserManager();
        userManager.FindByIdAsync("ghost").Returns((HearditUser?)null);

        using var ctx = db.CreateContext();
        var service = new ProfileService(ctx, userManager);

        Assert.Null(await service.FollowAsync("ghost", "someone"));
    }

    [Fact]
    public async Task UnfollowAsync_RemovesFollowRow()
    {
        // Exercises the ExecuteDeleteAsync path — the reason these tests run on SQLite, not the InMemory provider.
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob);
            seed.Follows.Add(new Follows { UserId = alice.Id, FollowerId = bob.Id });
            await seed.SaveChangesAsync();
        }

        var userManager = TestData.SubstituteUserManager();
        userManager.FindByIdAsync(alice.Id).Returns(alice);

        using (var ctx = db.CreateContext())
        {
            var service = new ProfileService(ctx, userManager);
            var name = await service.UnfollowAsync(alice.Id, bob.Id);
            Assert.Equal("alice", name);
        }

        using (var verify = db.CreateContext())
        {
            Assert.Equal(0, await verify.Follows.CountAsync());
        }
    }

    [Fact]
    public async Task GetFollowsAsync_ReturnsFollowerAndFollowingLists()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        var carol = TestData.MakeUser("carol");
        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob, carol);
            seed.Follows.Add(new Follows { UserId = alice.Id, FollowerId = bob.Id });   // bob follows alice
            seed.Follows.Add(new Follows { UserId = carol.Id, FollowerId = alice.Id }); // alice follows carol
            await seed.SaveChangesAsync();
        }

        var userManager = TestData.SubstituteUserManager();
        userManager.FindByNameAsync("alice").Returns(alice);

        using var ctx = db.CreateContext();
        var service = new ProfileService(ctx, userManager);
        var vm = await service.GetFollowsAsync("alice", bob.Id);

        Assert.NotNull(vm);
        Assert.Equal(1, vm!.FollowersCount);
        Assert.Equal(1, vm.FollowingCount);
        Assert.Contains(vm.FollowersList, u => u.UserName == "bob");
        Assert.Contains(vm.FollowingList, u => u.UserName == "carol");
        Assert.True(vm.IsFollowing);   // current user (bob) follows alice
    }
}
