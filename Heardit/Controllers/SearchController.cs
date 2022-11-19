using Heardit.Data;
using Heardit.Models;
using Microsoft.AspNetCore.Mvc;
using SpotifyAPI.Web;
using static Heardit.Controllers.SearchController;
using static Heardit.Controllers.SpotifyController;

namespace Heardit.Controllers
{
    public class SearchController : Controller
    {
        private readonly HearditContext _context;

        public SearchController(HearditContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> _Search(string SearchString)
        {
            if (string.IsNullOrWhiteSpace(SearchString)) { return View("~/Views/Home/Index.cshtml"); }

            //var tokenType = "Bearer";
            //var spotify = new SpotifyClient(await SpotifyController.GetAccessToken(), tokenType);
            //var spotify = new SpotifyClient("BQB0ZT6y7mwlw0QKAH8ewAZCkbZrDreYwVQrTVOmxLKrPPtNo6j0LtxLqed_3EHTswatAcz1hZegZJHhhmjer1klm3GA_xy6TRVP2JufeTlFks5j0Biynrq949sQHE6tJJ65Sd_nupPewZWE5L4Dn8rDqcWYoDTbwO7p3djjU4nlXEQ");
            //System.Diagnostics.Debug.WriteLine("Running _Search");

            var spotify = GetSpotifyClient();

            //var track = await spotify.Tracks.Get("1s6ux0lNiTziSrd7iUAADH");
            var searchRes = await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, SearchString));


            //System.Diagnostics.Debug.WriteLine(searchRes.Tracks.Items[0].Name + " " + searchRes.Tracks.Items[0].Artists[0].Name);
            var searchSongs = searchRes.Tracks.Items;
            //var IEnumerable<FullTrack> searchTracks = searchRes.Tracks;
            return View(searchSongs);
            //return View(searchRes.Tracks.Items[0].Name + " " + searchRes.Tracks.Items[0].Artists[0].Name);
        }



    }
}
