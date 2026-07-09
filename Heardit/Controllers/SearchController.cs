using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Microsoft.AspNetCore.Mvc;
using SpotifyAPI.Web;

namespace Heardit.Controllers
{
    public class SearchController : Controller
    {
        private readonly HearditDbContext _context;
        private readonly ISpotifyClient _spotify;

        public SearchController(HearditDbContext context, ISpotifyClient spotify)
        {
            _context = context;
            _spotify = spotify;
        }

        public async Task<IActionResult> _Search(string SearchString)
        {
            if (string.IsNullOrWhiteSpace(SearchString)) { return View("~/Views/Home/Index.cshtml"); }

            var searchRes = await _spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, SearchString));

            var searchSongs = searchRes.Tracks?.Items ?? new List<SpotifyAPI.Web.FullTrack>();

            return View(searchSongs);
        }
    }
}
