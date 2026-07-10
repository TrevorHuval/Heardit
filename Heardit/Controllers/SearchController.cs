using Heardit.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpotifyAPI.Web;

namespace Heardit.Controllers
{
    public class SearchController : Controller
    {
        private readonly ISpotifyService _spotify;

        public SearchController(ISpotifyService spotify)
        {
            _spotify = spotify;
        }

        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> _Search(string SearchString)
        {
            if (string.IsNullOrWhiteSpace(SearchString))
            {
                return View("~/Views/Home/Index.cshtml", Array.Empty<SimpleTrack>());
            }

            var results = await _spotify.SearchTracksAsync(SearchString);
            return View(results);
        }
    }
}
