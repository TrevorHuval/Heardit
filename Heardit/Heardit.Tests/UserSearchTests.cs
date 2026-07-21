using Heardit.Services;
using Heardit.Tests.Infrastructure;

namespace Heardit.Tests;

public class UserSearchTests
{
    private static async Task<ProfileService> ServiceWithUsersAsync(SqliteInMemoryDb db, params string[] usernames)
    {
        using (var seed = db.CreateContext())
        {
            foreach (var username in usernames)
            {
                seed.Users.Add(TestData.MakeUser(username));
            }

            await seed.SaveChangesAsync();
        }

        return new ProfileService(db.CreateContext(), TestData.SubstituteUserManager());
    }

    [Fact]
    public async Task SearchUsersAsync_MatchesAnywhereInTheUsername()
    {
        using var db = new SqliteInMemoryDb();
        var service = await ServiceWithUsersAsync(db, "phase2user", "phase2friend", "someoneelse");

        var found = await service.SearchUsersAsync("phase2");

        Assert.Equal(new[] { "phase2friend", "phase2user" }, found.Select(u => u.UserName));
    }

    [Fact]
    public async Task SearchUsersAsync_IgnoresCaseOnBothSides()
    {
        using var db = new SqliteInMemoryDb();
        var service = await ServiceWithUsersAsync(db, "RadioheadFan");

        Assert.Single(await service.SearchUsersAsync("radiohead"));
        Assert.Single(await service.SearchUsersAsync("HEADFAN"));
        Assert.Single(await service.SearchUsersAsync("  RadioheadFan  "));
    }

    [Fact]
    public async Task SearchUsersAsync_CapsAtTenResults()
    {
        using var db = new SqliteInMemoryDb();
        var service = await ServiceWithUsersAsync(
            db, Enumerable.Range(0, 15).Select(i => $"listener{i:00}").ToArray());

        var found = await service.SearchUsersAsync("listener");

        Assert.Equal(10, found.Count);
        Assert.Equal("listener00", found[0].UserName);   // alphabetical, so the cap is deterministic
    }

    [Fact]
    public async Task SearchUsersAsync_NoMatchOrBlankQuery_ReturnsNothing()
    {
        using var db = new SqliteInMemoryDb();
        var service = await ServiceWithUsersAsync(db, "alice");

        Assert.Empty(await service.SearchUsersAsync("bob"));
        Assert.Empty(await service.SearchUsersAsync("   "));
        Assert.Empty(await service.SearchUsersAsync(""));
    }
}
