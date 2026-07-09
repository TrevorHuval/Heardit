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
            var playlistItems = await _spotify.Playlists.GetPlaylistItems("37i9dQZEVXbMDoHDwVN2tF");

            List<SpotifyAPI.Web.FullTrack> topFiftySongs = new List<SpotifyAPI.Web.FullTrack>();

            if (playlistItems.Items != null)
            {
                foreach (PlaylistTrack<IPlayableItem> item in playlistItems.Items)
                {
                    if (item.Track is SpotifyAPI.Web.FullTrack track)
                    {
                        topFiftySongs.Add(track);
                    }
                }
            }

            return View(topFiftySongs);
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