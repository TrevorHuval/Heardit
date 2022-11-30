using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Heardit.Models;
using Heardit.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using SpotifyAPI.Web;
using static Heardit.Controllers.SpotifyController;
using System.Diagnostics;
using Heardit.Migrations;
using Microsoft.AspNetCore.Authorization;

namespace Heardit.Controllers
{
    [Authorize]
    public class SongsController : Controller
    {
        private readonly HearditDbContext _context;
        private readonly UserManager<HearditUser> _userManager;

        public SongsController(UserManager<HearditUser> userManager, HearditDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index(string songId)
        {
            if (string.IsNullOrWhiteSpace(songId)) { return NotFound(); }
            try
            {
                var song = _context.Songs.AsNoTracking().Where(s => s.Id.Equals(songId)).FirstOrDefault();

                if (song == null)
                {
                    song = await GenerateSong(songId);
                }

                var songModel = new SongModel
                {
                    Song = song,
                    Reviews = _context.Reviews.AsNoTracking().Where(r => r.SongId.Equals(songId)).Include(r => r.User).ToList()
                };

                return View("Song", songModel);
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(string writtenReview, decimal rating, string songId, string songName)
        {
            HearditUser user = await _userManager.FindByIdAsync(User.GetLoggedInUserId<string>());

            var review = new Review(writtenReview, user, rating, songId, songName);
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            ModelState.Clear();

            var song = _context.Songs.AsNoTracking().Where(s => s.Id.Equals(songId)).FirstOrDefault();

            var songModel = new SongModel
            {
                Song = song,
                Reviews = _context.Reviews.AsNoTracking().Where(r => r.SongId.Equals(songId)).Include(r => r.User).ToList()
            };

            return View("Song", songModel);
        }


        public async Task<Song> GenerateSong(string songId)
        {

            var spotify = GetSpotifyClient();

            var songResponse = await spotify.Tracks.Get(songId);

            _context.Songs.Add(new Song(songId, songResponse.Name, songResponse.Artists[0].Name, songResponse.Album.Name, songResponse.Album.Images[0].Url));
            await _context.SaveChangesAsync();
            ModelState.Clear();

            var song = _context.Songs.AsNoTracking().Where(s => s.Id.Equals(songId)).FirstOrDefault();

            return song;
        }

        // GET: Songs
        public async Task<IActionResult> SongsDb(string searchString)
        {
            if (_context.Songs == null)
            {
                return Problem("Entity set 'Heardit.Song'  is null.");
            }

            var songs = from m in _context.Songs
                        select m;

            if (!String.IsNullOrEmpty(searchString))
            {
                songs = songs.Where(s => s.Title!.Contains(searchString));
            }

            return View(await songs.ToListAsync());
        }

        // GET: Songs/Details/5
        public async Task<IActionResult> Details(string? id)
        {
            if (id == null || _context.Songs == null)
            {
                return NotFound();
            }

            var song = await _context.Songs
                .FirstOrDefaultAsync(m => m.Id == id);
            if (song == null)
            {
                return NotFound();
            }

            return View(song);
        }

        // GET: Songs/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Songs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Artist,Album")] Song song)
        {
            if (ModelState.IsValid)
            {
                _context.Add(song);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(song);
        }

        // GET: Songs/Edit/5
        public async Task<IActionResult> Edit(string? id)
        {
            if (id == null || _context.Songs == null)
            {
                return NotFound();
            }

            var song = await _context.Songs.FindAsync(id);
            if (song == null)
            {
                return NotFound();
            }
            return View(song);
        }

        // POST: Songs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,Title,Artist,Album")] Song song)
        {
            if (id != song.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(song);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SongExists(song.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(song);
        }

        // GET: Songs/Delete/5
        public async Task<IActionResult> Delete(string? id)
        {
            if (id == null || _context.Songs == null)
            {
                return NotFound();
            }

            var song = await _context.Songs
                .FirstOrDefaultAsync(m => m.Id == id);
            if (song == null)
            {
                return NotFound();
            }

            return View(song);
        }

        // POST: Songs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (_context.Songs == null)
            {
                return Problem("Entity set 'HearditDbContext.Song'  is null.");
            }
            var song = await _context.Songs.FindAsync(id);
            if (song != null)
            {
                _context.Songs.Remove(song);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SongExists(string id)
        {
            return (_context.Songs?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
