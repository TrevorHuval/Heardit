using Heardit.Models;
using Heardit.Services;
using Heardit.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SpotifyAPI.Web;

namespace Heardit.Tests;

public class SongServiceTests
{
    [Fact]
    public async Task GetOrCreateSongAsync_ExistingSong_ReturnsItWithoutCallingSpotify()
    {
        using var db = new SqliteInMemoryDb();
        using (var seed = db.CreateContext())
        {
            seed.Songs.Add(new Song("song1", "Karma Police", "Radiohead", "OK Computer", "art"));
            await seed.SaveChangesAsync();
        }

        var spotify = Substitute.For<ISpotifyService>();
        using var ctx = db.CreateContext();
        var service = new SongService(ctx, spotify);

        var song = await service.GetOrCreateSongAsync("song1");

        Assert.NotNull(song);
        Assert.Equal("Karma Police", song!.Title);
        await spotify.DidNotReceive().GetTrackAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetOrCreateSongAsync_MissingSong_CreatesFromSpotifyAndPersists()
    {
        using var db = new SqliteInMemoryDb();

        var spotify = Substitute.For<ISpotifyService>();
        spotify.GetTrackAsync("song2").Returns(new FullTrack
        {
            Id = "song2",
            Name = "Paranoid Android",
            Artists = new List<SimpleArtist> { new() { Name = "Radiohead" } },
            Album = new SimpleAlbum
            {
                Name = "OK Computer",
                Images = new List<Image> { new() { Url = "https://img/art.jpg" } }
            }
        });

        using (var ctx = db.CreateContext())
        {
            var service = new SongService(ctx, spotify);
            var song = await service.GetOrCreateSongAsync("song2");

            Assert.NotNull(song);
            Assert.Equal("song2", song!.Id);
            Assert.Equal("Paranoid Android", song.Title);
            Assert.Equal("Radiohead", song.Artist);
            Assert.Equal("OK Computer", song.Album);
            Assert.Equal("https://img/art.jpg", song.AlbumArt);
        }

        using (var verify = db.CreateContext())
        {
            var stored = await verify.Songs.FindAsync("song2");
            Assert.NotNull(stored);
            Assert.Equal("Paranoid Android", stored!.Title);
        }
    }

    [Fact]
    public async Task GetOrCreateSongAsync_SpotifyReturnsNull_ReturnsNullAndPersistsNothing()
    {
        using var db = new SqliteInMemoryDb();

        var spotify = Substitute.For<ISpotifyService>();
        spotify.GetTrackAsync("missing").Returns((FullTrack?)null);

        using (var ctx = db.CreateContext())
        {
            var service = new SongService(ctx, spotify);
            var song = await service.GetOrCreateSongAsync("missing");
            Assert.Null(song);
        }

        using (var verify = db.CreateContext())
        {
            Assert.Equal(0, await verify.Songs.CountAsync());
        }
    }

    [Fact]
    public async Task GetOrCreateSongAsync_ConcurrentInsert_FallsBackToTheStoredRow()
    {
        // Two first views race. The second context loads its snapshot before the first commits, so its
        // own insert violates the primary key; the service must swallow that and re-read the winner.
        using var db = new SqliteInMemoryDb();

        var spotify = Substitute.For<ISpotifyService>();
        spotify.GetTrackAsync("race").Returns(_ =>
        {
            // Called after the service has already missed the row and before it saves its own — exactly
            // the window the other request commits in.
            using var winner = db.CreateContext();
            winner.Songs.Add(new Song("race", "Winner's Copy", "First Past", "Photo Finish", "https://img/a.jpg"));
            winner.SaveChanges();

            return new FullTrack
            {
                Id = "race",
                Name = "Loser's Copy",
                Artists = new List<SimpleArtist> { new() { Name = "Runner Up" } },
                Album = new SimpleAlbum { Name = "Photo Finish", Images = new List<Image> { new() { Url = "https://img/b.jpg" } } }
            };
        });

        using var ctx = db.CreateContext();
        var service = new SongService(ctx, spotify);

        var song = await service.GetOrCreateSongAsync("race");

        Assert.NotNull(song);
        Assert.Equal("Winner's Copy", song!.Title);
        using (var verify = db.CreateContext())
        {
            Assert.Equal(1, await verify.Songs.CountAsync());
        }
    }

    [Fact]
    public async Task GetSongPageAsync_SplitsOwnReviewFromTheRestAndTotalsThemAll()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        var bob = TestData.MakeUser("bob");
        using (var seed = db.CreateContext())
        {
            seed.Users.AddRange(alice, bob);
            seed.Songs.Add(new Song("song3", "No Surprises", "Radiohead", "OK Computer", "art"));
            seed.Reviews.Add(new Review("great track", alice, 4.0m, "song3", "No Surprises"));
            seed.Reviews.Add(new Review("overrated", bob, 3.0m, "song3", "No Surprises"));
            seed.Reviews.Add(new Review("unrelated", alice, 2.0m, "otherSong", "Other"));
            await seed.SaveChangesAsync();
        }

        var spotify = Substitute.For<ISpotifyService>();
        using var ctx = db.CreateContext();
        var service = new SongService(ctx, spotify);

        var vm = await service.GetSongPageAsync("song3", alice.Id);

        Assert.NotNull(vm);
        Assert.Equal("No Surprises", vm!.Song.Title);
        Assert.Equal("great track", vm.MyReview?.WrittenReview);
        Assert.Single(vm.Reviews.Items);
        Assert.Equal("overrated", vm.Reviews.Items[0].WrittenReview);
        Assert.Equal(2, vm.ReviewCount);            // totals cover both reviews of the song
        Assert.Equal(3.5m, vm.AverageRating);
    }

    [Fact]
    public async Task GetSongPageAsync_PagesOtherPeoplesReviews()
    {
        using var db = new SqliteInMemoryDb();
        var alice = TestData.MakeUser("alice");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(alice);
            seed.Songs.Add(new Song("song4", "Let Down", "Radiohead", "OK Computer", "art"));
            for (var i = 0; i < 21; i++)
            {
                var author = TestData.MakeUser($"fan{i:00}");
                seed.Users.Add(author);
                seed.Reviews.Add(new Review($"take {i}", author, 5m, "song4", "Let Down")
                {
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i)
                });
            }

            await seed.SaveChangesAsync();
        }

        var spotify = Substitute.For<ISpotifyService>();
        using var ctx = db.CreateContext();
        var service = new SongService(ctx, spotify);

        var first = await service.GetSongPageAsync("song4", alice.Id, page: 1);
        Assert.Equal(20, first!.Reviews.Count);
        Assert.True(first.Reviews.HasNext);
        Assert.Equal(21, first.ReviewCount);        // the header counts every page

        var second = await service.GetSongPageAsync("song4", alice.Id, page: 2);
        Assert.Single(second!.Reviews.Items);
        Assert.False(second.Reviews.HasNext);
        Assert.Null(second.MyReview);
    }

    [Fact]
    public async Task GetSongPageAsync_UnknownSongAndNoSpotifyMatch_ReturnsNull()
    {
        using var db = new SqliteInMemoryDb();

        var spotify = Substitute.For<ISpotifyService>();
        spotify.GetTrackAsync(Arg.Any<string>()).Returns((FullTrack?)null);

        using var ctx = db.CreateContext();
        var service = new SongService(ctx, spotify);

        var vm = await service.GetSongPageAsync("ghost", "anyone");

        Assert.Null(vm);
    }
}
