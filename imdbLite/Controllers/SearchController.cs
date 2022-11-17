using imdbLite.Data;
using imdbLite.Models;
using Microsoft.AspNetCore.Mvc;
using SpotifyAPI.Web;
using static imdbLite.Controllers.SearchController;

namespace imdbLite.Controllers
{
    public class SearchController : Controller
    {
        private readonly imdbLiteContext _context;

        public SearchController(imdbLiteContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> _Search(string search)
        {
            var spotify = new SpotifyClient(SpotifyController.GetAccessToken());
            Console.WriteLine("Running _Search");

            var track = await spotify.Tracks.Get("1s6ux0lNiTziSrd7iUAADH");
            var searchRes = await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, "Paranoid"));

            Console.WriteLine(searchRes.Tracks.Items);

            return View();
        }



    }
}
