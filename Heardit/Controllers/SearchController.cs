using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Microsoft.AspNetCore.Mvc;
using SpotifyAPI.Web;
using static Heardit.Controllers.SearchController;
using static Heardit.Controllers.SpotifyController;

namespace Heardit.Controllers
{
    public class SearchController : Controller
    {
        private readonly HearditDbContext _context;

        public SearchController(HearditDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> _Search(string SearchString)
        {
            if (string.IsNullOrWhiteSpace(SearchString)) { return View("~/Views/Home/Index.cshtml"); }

            var spotify = GetSpotifyClient();

            var searchRes = await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, SearchString));

            var searchSongs = searchRes.Tracks.Items;

            return View(searchSongs);
        }



    }
}
