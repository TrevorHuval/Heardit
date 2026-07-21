using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.Services;
using Heardit.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Heardit.Tests;

public class ProfileBioFavoritesTests
{
    private static async Task<HearditUser> SeedListenerAsync(SqliteInMemoryDb db, int songs = 0)
    {
        var alice = TestData.MakeUser("alice");
        using var seed = db.CreateContext();
        seed.Users.Add(alice);
        for (var i = 0; i < songs; i++)
        {
            seed.Songs.Add(new Song($"song{i}", $"Track {i}", "Artist", "Album", $"https://img/{i}.jpg"));
        }

        await seed.SaveChangesAsync();
        return alice;
    }

    [Fact]
    public async Task UpdateBioAsync_SavesTrimmedText()
    {
        using var db = new SqliteInMemoryDb();
        var alice = await SeedListenerAsync(db);

        using (var ctx = db.CreateContext())
        {
            var service = new ProfileService(ctx, TestData.SubstituteUserManager());
            Assert.True(await service.UpdateBioAsync(alice.Id, "  mostly shoegaze  "));
        }

        using var verify = db.CreateContext();
        Assert.Equal("mostly shoegaze", (await verify.Users.FindAsync(alice.Id))!.Bio);
    }

    [Fact]
    public async Task UpdateBioAsync_OverTheLimit_IsRejectedAndChangesNothing()
    {
        using var db = new SqliteInMemoryDb();
        var alice = await SeedListenerAsync(db);

        using (var ctx = db.CreateContext())
        {
            var service = new ProfileService(ctx, TestData.SubstituteUserManager());
            Assert.True(await service.UpdateBioAsync(alice.Id, new string('a', HearditUser.BioMaxLength)));
            Assert.False(await service.UpdateBioAsync(alice.Id, new string('b', HearditUser.BioMaxLength + 1)));
        }

        using var verify = db.CreateContext();
        var stored = (await verify.Users.FindAsync(alice.Id))!.Bio;
        Assert.Equal(new string('a', HearditUser.BioMaxLength), stored);   // the long one never landed
    }

    [Fact]
    public async Task UpdateBioAsync_BlankClearsItBackToNull()
    {
        using var db = new SqliteInMemoryDb();
        var alice = await SeedListenerAsync(db);

        using (var ctx = db.CreateContext())
        {
            var service = new ProfileService(ctx, TestData.SubstituteUserManager());
            await service.UpdateBioAsync(alice.Id, "something");
            Assert.True(await service.UpdateBioAsync(alice.Id, "   "));
        }

        using var verify = db.CreateContext();
        Assert.Null((await verify.Users.FindAsync(alice.Id))!.Bio);
    }

    [Fact]
    public async Task AddFavoriteAsync_FillsSlotsInOrderAndRejectsTheFifth()
    {
        using var db = new SqliteInMemoryDb();
        var alice = await SeedListenerAsync(db, songs: 5);

        using (var ctx = db.CreateContext())
        {
            var service = new ProfileService(ctx, TestData.SubstituteUserManager());
            for (var i = 0; i < FavoriteTrack.MaxPerUser; i++)
            {
                Assert.Equal(FavoriteAddStatus.Added, await service.AddFavoriteAsync(alice.Id, $"song{i}"));
            }

            Assert.Equal(FavoriteAddStatus.Full, await service.AddFavoriteAsync(alice.Id, "song4"));
            Assert.Equal(FavoriteAddStatus.AlreadyThere, await service.AddFavoriteAsync(alice.Id, "song0"));
            Assert.Equal(FavoriteAddStatus.NotFound, await service.AddFavoriteAsync(alice.Id, "neverStored"));
        }

        using var verify = db.CreateContext();
        var positions = await verify.FavoriteTracks
            .Where(f => f.UserId == alice.Id)
            .OrderBy(f => f.Position)
            .Select(f => new { f.SongId, f.Position })
            .ToListAsync();

        Assert.Equal(new[] { 1, 2, 3, 4 }, positions.Select(p => p.Position));
        Assert.Equal(new[] { "song0", "song1", "song2", "song3" }, positions.Select(p => p.SongId));
    }

    [Fact]
    public async Task RemoveFavoriteAsync_FreesTheSlotForTheNextTrack()
    {
        using var db = new SqliteInMemoryDb();
        var alice = await SeedListenerAsync(db, songs: 5);

        using var ctx = db.CreateContext();
        var service = new ProfileService(ctx, TestData.SubstituteUserManager());
        for (var i = 0; i < FavoriteTrack.MaxPerUser; i++)
        {
            await service.AddFavoriteAsync(alice.Id, $"song{i}");
        }

        // Vacate slot 2, and the next favorite should land in it rather than at the end.
        await service.RemoveFavoriteAsync(alice.Id, "song1");
        Assert.Equal(FavoriteAddStatus.Added, await service.AddFavoriteAsync(alice.Id, "song4"));

        using var verify = db.CreateContext();
        var newcomer = await verify.FavoriteTracks.SingleAsync(f => f.SongId == "song4");
        Assert.Equal(2, newcomer.Position);
        Assert.Equal(FavoriteTrack.MaxPerUser, await verify.FavoriteTracks.CountAsync(f => f.UserId == alice.Id));
    }

    [Fact]
    public async Task GetFavoriteStateAsync_ReportsPinnedAndWhetherThereIsRoom()
    {
        using var db = new SqliteInMemoryDb();
        var alice = await SeedListenerAsync(db, songs: 5);

        using var ctx = db.CreateContext();
        var service = new ProfileService(ctx, TestData.SubstituteUserManager());

        Assert.Equal(new FavoriteState(false, true), await service.GetFavoriteStateAsync(alice.Id, "song0"));

        for (var i = 0; i < FavoriteTrack.MaxPerUser; i++)
        {
            await service.AddFavoriteAsync(alice.Id, $"song{i}");
        }

        Assert.Equal(new FavoriteState(true, false), await service.GetFavoriteStateAsync(alice.Id, "song0"));
        Assert.Equal(new FavoriteState(false, false), await service.GetFavoriteStateAsync(alice.Id, "song4"));
    }

    [Fact]
    public async Task GetProfileAsync_CarriesFavoritesInSlotOrderWithTheirSongs()
    {
        using var db = new SqliteInMemoryDb();
        var alice = await SeedListenerAsync(db, songs: 3);

        using (var ctx = db.CreateContext())
        {
            var service = new ProfileService(ctx, TestData.SubstituteUserManager());
            await service.AddFavoriteAsync(alice.Id, "song2");
            await service.AddFavoriteAsync(alice.Id, "song0");
        }

        var userManager = TestData.SubstituteUserManager();
        userManager.FindByNameAsync("alice").Returns(alice);

        using var read = db.CreateContext();
        var profile = await new ProfileService(read, userManager).GetProfileAsync("alice", alice.Id);

        Assert.Equal(new[] { "song2", "song0" }, profile!.Favorites.Select(f => f.SongId));
        Assert.Equal("Track 2", profile.Favorites[0].Song.Title);
        Assert.Equal("https://img/2.jpg", profile.Favorites[0].Song.AlbumArt);
    }
}
