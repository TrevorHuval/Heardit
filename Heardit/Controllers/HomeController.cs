using System.Diagnostics;
using Heardit.Models;
using Heardit.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Heardit.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISpotifyService _spotify;

        public HomeController(ISpotifyService spotify)
        {
            _spotify = spotify;
        }

        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> Index()
        {
            var newReleases = await _spotify.GetNewReleaseTracksAsync();
            return View(newReleases);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
