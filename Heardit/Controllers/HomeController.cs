using Heardit.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpotifyAPI.Web;
using System.Collections.Generic;
using System.Diagnostics;
using static Heardit.Controllers.SpotifyController;

namespace Heardit.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> IndexAsync()
        {
            var spotify = GetSpotifyClient();

            var playlistGetItemsRequest = new PlaylistGetItemsRequest();
            playlistGetItemsRequest.Fields.Add("items(track(name,type))");

            var topFiftyPlaylist = await spotify.Playlists.Get("37i9dQZEVXbMDoHDwVN2tF");

            List<SpotifyAPI.Web.FullTrack> topFiftySongs = new List<SpotifyAPI.Web.FullTrack>();

            foreach (PlaylistTrack<IPlayableItem> item in topFiftyPlaylist.Tracks.Items)
            {
                if (item.Track is SpotifyAPI.Web.FullTrack track)
                {
                    topFiftySongs.Add(track);
                }
            }


            //IEnumerable<SpotifyAPI.Web.FullTrack> listofTracks = topFiftySongs.Cast<SpotifyAPI.Web.FullTrack>();
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