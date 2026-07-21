using Heardit.Models;
using Heardit.Services;
using Heardit.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Heardit.Tests;

public class ListenLaterServiceTests
{
    /// <summary>A song service that hands back whatever is already stored, like the real one does.</summary>
    private static ISongService SongServiceOver(SqliteInMemoryDb db)
    {
        var songs = Substitute.For<ISongService>();
        songs.GetOrCreateSongAsync(Arg.Any<string>()).Returns(async call =>
        {
            using var ctx = db.CreateContext();
            return await ctx.Songs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == call.Arg<string>());
        });
        return songs;
    }

    [Fact]
    public async Task ToggleAsync_StoredSong_SavesItWithoutTouchingSpotify()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(alice);
            seed.Songs.Add(new Song("song1", "Karma Police", "Radiohead", "OK Computer", "art"));
            await seed.SaveChangesAsync();
        }

        var songs = SongServiceOver(db);
        using var ctx = db.CreateContext();
        var service = new ListenLaterService(ctx, songs);

        Assert.True(await service.ToggleAsync("song1", alice.Id));

        using var verify = db.CreateContext();
        var saved = await verify.ListenLater.SingleAsync();
        Assert.Equal("song1", saved.SongId);
        Assert.Equal(alice.Id, saved.UserId);
    }

    [Fact]
    public async Task ToggleAsync_TrackFromAFeedCard_CreatesTheSongRowFirst()
    {
        // Feed cards carry nothing but a Spotify id, so the song may never have been stored. The
        // foreign key needs that row before the save can land.
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(alice);
            await seed.SaveChangesAsync();
        }

        var songs = Substitute.For<ISongService>();
        songs.GetOrCreateSongAsync("fresh").Returns(_ =>
        {
            using var creator = db.CreateContext();
            var song = new Song("fresh", "Idioteque", "Radiohead", "Kid A", "https://img/a.jpg");
            creator.Songs.Add(song);
            creator.SaveChanges();
            return song;
        });

        using var ctx = db.CreateContext();
        var service = new ListenLaterService(ctx, songs);

        Assert.True(await service.ToggleAsync("fresh", alice.Id));

        using var verify = db.CreateContext();
        Assert.Equal(1, await verify.Songs.CountAsync());
        Assert.Equal(1, await verify.ListenLater.CountAsync());
    }

    [Fact]
    public async Task ToggleAsync_UnresolvableTrack_ReturnsNullAndSavesNothing()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(alice);
            await seed.SaveChangesAsync();
        }

        var songs = Substitute.For<ISongService>();
        songs.GetOrCreateSongAsync("ghost").Returns((Song?)null);

        using var ctx = db.CreateContext();
        var service = new ListenLaterService(ctx, songs);

        Assert.Null(await service.ToggleAsync("ghost", alice.Id));

        using var verify = db.CreateContext();
        Assert.Equal(0, await verify.ListenLater.CountAsync());
    }

    [Fact]
    public async Task ToggleAsync_SecondPress_RemovesTheSave()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(alice);
            seed.Songs.Add(new Song("song1", "Karma Police", "Radiohead", "OK Computer", "art"));
            seed.ListenLater.Add(new ListenLater { UserId = alice.Id, SongId = "song1", CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        var songs = SongServiceOver(db);
        using var ctx = db.CreateContext();
        var service = new ListenLaterService(ctx, songs);

        Assert.False(await service.ToggleAsync("song1", alice.Id));

        using var verify = db.CreateContext();
        Assert.Equal(0, await verify.ListenLater.CountAsync());
        // Unsaving never needs the song looked up — the row is already there.
        await songs.DidNotReceive().GetOrCreateSongAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetQueueAsync_IsNewestFirstAndPaged()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob);
            for (var i = 0; i < 21; i++)
            {
                seed.Songs.Add(new Song($"song{i:00}", $"Track {i}", "Artist", "Album", "art"));
                seed.ListenLater.Add(new ListenLater
                {
                    UserId = alice.Id,
                    SongId = $"song{i:00}",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i)
                });
            }

            // Someone else's queue must not leak into alice's.
            seed.ListenLater.Add(new ListenLater { UserId = bob.Id, SongId = "song00", CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var service = new ListenLaterService(ctx, SongServiceOver(db));

        var first = await service.GetQueueAsync(alice.Id);
        Assert.Equal(20, first.Count);
        Assert.True(first.HasNext);
        Assert.Equal("song20", first.Items[0].SongId);          // newest save leads
        Assert.Equal("Track 20", first.Items[0].Song.Title);    // the song came along for the ride

        var second = await service.GetQueueAsync(alice.Id, page: 2);
        Assert.Single(second.Items);
        Assert.False(second.HasNext);
        Assert.Equal("song00", second.Items[0].SongId);
    }

    [Fact]
    public async Task GetSavedSongIdsAsync_ReturnsOnlyThisUsersSavesFromTheAskedForSet()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob);
            seed.Songs.AddRange(
                new Song("a", "A", "x", "y", "art"),
                new Song("b", "B", "x", "y", "art"),
                new Song("c", "C", "x", "y", "art"));
            seed.ListenLater.Add(new ListenLater { UserId = alice.Id, SongId = "a", CreatedAt = DateTime.UtcNow });
            seed.ListenLater.Add(new ListenLater { UserId = alice.Id, SongId = "c", CreatedAt = DateTime.UtcNow });
            seed.ListenLater.Add(new ListenLater { UserId = bob.Id, SongId = "b", CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        using var ctx = db.CreateContext();
        var service = new ListenLaterService(ctx, SongServiceOver(db));

        var saved = await service.GetSavedSongIdsAsync(alice.Id, new[] { "a", "b" });

        Assert.Equal(new[] { "a" }, saved);                  // "c" wasn't asked about, "b" is bob's
        Assert.Empty(await service.GetSavedSongIdsAsync(alice.Id, Array.Empty<string>()));
    }
}
