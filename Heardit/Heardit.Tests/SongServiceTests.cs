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
    public async Task GetSongPageAsync_ReturnsSongWithOnlyItsReviews()
    {
        using var db = new SqliteInMemoryDb();
        var user = TestData.MakeUser("alice");
        using (var seed = db.CreateContext())
        {
            seed.Users.Add(user);
            seed.Songs.Add(new Song("song3", "No Surprises", "Radiohead", "OK Computer", "art"));
            seed.Reviews.Add(new Review("great track", user, 4.5m, "song3", "No Surprises"));
            seed.Reviews.Add(new Review("unrelated", user, 2.0m, "otherSong", "Other"));
            await seed.SaveChangesAsync();
        }

        var spotify = Substitute.For<ISpotifyService>();
        using var ctx = db.CreateContext();
        var service = new SongService(ctx, spotify);

        var vm = await service.GetSongPageAsync("song3");

        Assert.NotNull(vm);
        Assert.Equal("No Surprises", vm!.Song.Title);
        Assert.Single(vm.Reviews);
        Assert.Equal("great track", vm.Reviews[0].WrittenReview);
    }

    [Fact]
    public async Task GetSongPageAsync_UnknownSongAndNoSpotifyMatch_ReturnsNull()
    {
        using var db = new SqliteInMemoryDb();

        var spotify = Substitute.For<ISpotifyService>();
        spotify.GetTrackAsync(Arg.Any<string>()).Returns((FullTrack?)null);

        using var ctx = db.CreateContext();
        var service = new SongService(ctx, spotify);

        var vm = await service.GetSongPageAsync("ghost");

        Assert.Null(vm);
    }
}
