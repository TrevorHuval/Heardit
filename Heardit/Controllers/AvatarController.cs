using Heardit.Services;
using Microsoft.AspNetCore.Mvc;

namespace Heardit.Controllers
{
    public class AvatarController : Controller
    {
        private readonly IAvatarService _avatars;

        public AvatarController(IAvatarService avatars) => _avatars = avatars;

        /// <summary>
        /// A profile photo. URLs carry the photo's version (?v=), so a given URL never changes content and
        /// the browser can keep it for a year; a new photo gets a new URL.
        /// </summary>
        [HttpGet("avatars/{userId}")]
        public async Task<IActionResult> Photo(string userId, string? v)
        {
            var avatar = await _avatars.GetAsync(userId);
            if (avatar == null)
            {
                return NotFound();
            }

            if (!string.Equals(v, avatar.Version, StringComparison.Ordinal))
            {
                // An old or missing version: point at the current one rather than caching the wrong bytes.
                return RedirectToAction(nameof(Photo), new { userId, v = avatar.Version });
            }

            // Private: the whole app is behind a login, so shared caches shouldn't keep these.
            Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return File(avatar.Data, avatar.ContentType);
        }
    }
}
