using System.Diagnostics;
using Heardit.Models;
using Heardit.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heardit.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ISpotifyService _spotify;

        public HomeController(ISpotifyService spotify)
        {
            _spotify = spotify;
        }

        public async Task<IActionResult> Index()
        {
            var newReleases = await _spotify.GetNewReleaseTracksAsync();
            return View(newReleases);
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
