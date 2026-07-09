using Heardit.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpotifyAPI.Web;
using System.Collections.Generic;
using System.Diagnostics;

namespace Heardit.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ISpotifyClient _spotify;

        public HomeController(ILogger<HomeController> logger, ISpotifyClient spotify)
        {
            _logger = logger;
            _spotify = spotify;
        }

        public async Task<IActionResult> IndexAsync()
        {
            // Spotify closed Web API access to its editorial "Top 50" playlists in Nov 2024,
            // so the homepage surfaces new releases instead. New releases come back as albums;
            // we resolve each album's lead track so the existing song-review flow still works.
            var newReleases = await _spotify.Browse.GetNewReleases();

            var albumIds = newReleases.Albums?.Items?
                .Where(a => !string.IsNullOrEmpty(a.Id))
                .Select(a => a.Id)
                .Take(20)
                .ToList() ?? new List<string>();

            var leadTracks = new List<SimpleTrack>();

            if (albumIds.Count > 0)
            {
                var albums = await _spotify.Albums.GetSeveral(new AlbumsRequest(albumIds));

                foreach (var album in albums.Albums)
                {
                    var firstTrack = album.Tracks?.Items?.FirstOrDefault();
                    if (firstTrack != null)
                    {
                        leadTracks.Add(firstTrack);
                    }
                }
            }

            return View(leadTracks);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}