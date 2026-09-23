using System.Diagnostics;
using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Heardit.Services;
using Heardit.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Heardit.Controllers
{
    public class HomeController : Controller
    {
        // How much of each feed the dashboard previews; the rest is one "See all" away.
        private const int FriendPreview = 4;
        private const int LatestPreview = 5;
        private const int ShelfSize = 8;
        private const int TrendingPageSize = 20;

        private readonly ISpotifyService _spotify;
        private readonly IReviewService _reviewService;
        private readonly IProfileService _profileService;
        private readonly IListenLaterService _listenLater;

        public HomeController(
            ISpotifyService spotify,
            IReviewService reviewService,
            IProfileService profileService,
            IListenLaterService listenLater)
        {
            _spotify = spotify;
            _reviewService = reviewService;
            _profileService = profileService;
            _listenLater = listenLater;
        }

        // The dashboard may call Spotify on a new-releases cache miss, so it shares that limiter.
        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> Index(string? tab)
        {
            // The homepage used to be two tabs; keep old links and bookmarks landing somewhere sensible.
            if (string.Equals(tab, "following", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Following));
            }

            if (string.Equals(tab, "new", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(NewReleases));
            }

            var userId = User.GetLoggedInUserId<string>();
            var model = new HomeIndexViewModel
            {
                FollowsAnyone = await _profileService.IsFollowingAnyoneAsync(userId)
            };

            if (model.FollowsAnyone)
            {
                var friends = await _reviewService.GetFollowingFeedAsync(userId);
                model.FriendReviews = friends.Items.Take(FriendPreview).ToList();
                model.MoreFriendReviews = friends.Count > FriendPreview || friends.HasNext;
            }

            var trending = await _reviewService.GetTrendingAsync(ShelfSize);
            model.Trending = trending.Songs.Select(ShelfTileViewModel.From).ToList();
            model.TrendingIsAllTime = trending.IsAllTime;

            var releases = await LoadNewReleasesAsync(userId);
            model.SpotifyUnavailable = releases == null;
            model.NewReleases = (releases ?? new List<FeedItemViewModel>())
                .Take(ShelfSize)
                .Select(ShelfTileViewModel.From)
                .ToList();

            var latest = await _reviewService.GetRecentReviewsAsync();
            model.LatestReviews = latest.Items.Take(LatestPreview).ToList();

            model.LikeStats = await _reviewService.GetLikeStatsAsync(
                model.FriendReviews.Concat(model.LatestReviews).Select(r => r.ReviewId), userId);

            return View(model);
        }

        [EnableRateLimiting("spotify")]
        public async Task<IActionResult> NewReleases()
        {
            var releases = await LoadNewReleasesAsync(User.GetLoggedInUserId<string>());
            return View("TrackGrid", new TrackGridViewModel
            {
                Title = "New Releases",
                Subtitle = "The biggest drops on Spotify from the last two weeks. Play a track, then rate it.",
                Tracks = releases ?? new List<FeedItemViewModel>(),
                SpotifyUnavailable = releases == null,
                EmptyMessage = "No new releases to show right now. Check back soon."
            });
        }

        public async Task<IActionResult> Trending()
        {
            var trending = await _reviewService.GetTrendingAsync(TrendingPageSize);
            var tracks = trending.Songs.Select(t => new FeedItemViewModel
            {
                Id = t.Song.Id,
                Name = t.Song.Title ?? string.Empty,
                Artists = t.Song.Artist ?? string.Empty,
                ImageUrl = string.IsNullOrWhiteSpace(t.Song.AlbumArt) ? null : t.Song.AlbumArt,
                AverageRating = t.ReviewCount > 0 ? t.Average : null,
                ReviewCount = t.ReviewCount
            }).ToList();

            await SavedState.ApplyAsync(_listenLater, tracks, User.GetLoggedInUserId<string>());

            return View("TrackGrid", new TrackGridViewModel
            {
                Title = trending.IsAllTime ? "Most reviewed" : "Trending this week",
                Subtitle = trending.IsAllTime
                    ? "The tracks Heardit listeners have talked about most."
                    : "The tracks getting the most reviews and likes on Heardit over the last seven days.",
                Tracks = tracks,
                EmptyMessage = "Nobody has reviewed anything yet. Be the first."
            });
        }

        public async Task<IActionResult> Following(int page = 1)
        {
            var userId = User.GetLoggedInUserId<string>();
            var reviews = await _reviewService.GetFollowingFeedAsync(userId, page);
            var followsAnyone = await _profileService.IsFollowingAnyoneAsync(userId);

            return View("ReviewFeed", new ReviewFeedViewModel
            {
                Title = "Following",
                Subtitle = "The latest reviews from the listeners you follow.",
                Reviews = reviews,
                LikeStats = await _reviewService.GetLikeStatsAsync(reviews.Items.Select(r => r.ReviewId), userId),
                EmptyMessage = followsAnyone
                    ? "Nothing from people you follow yet."
                    : "You're not following anyone yet. Find people in the latest reviews or by searching their name."
            });
        }

        public async Task<IActionResult> Latest(int page = 1)
        {
            var userId = User.GetLoggedInUserId<string>();
            var reviews = await _reviewService.GetRecentReviewsAsync(page);

            return View("ReviewFeed", new ReviewFeedViewModel
            {
                Title = "Latest reviews",
                Subtitle = "What everyone on Heardit has been listening to, newest first.",
                Reviews = reviews,
                LikeStats = await _reviewService.GetLikeStatsAsync(reviews.Items.Select(r => r.ReviewId), userId),
                EmptyMessage = "Nobody has reviewed anything yet. Be the first."
            });
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
            // When reached via UseStatusCodePagesWithReExecute the original status code is preserved
            // on the response, so the view can tailor its copy (404 vs 429 vs a generic failure).
            var status = HttpContext.Response.StatusCode;
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = status >= 400 ? status : null
            });
        }

        /// <summary>New releases as feed items with review stats and saved state. Null when Spotify is unreachable.</summary>
        private async Task<List<FeedItemViewModel>?> LoadNewReleasesAsync(string userId)
        {
            var tracks = await _spotify.GetNewReleaseTracksAsync();
            if (tracks == null)
            {
                return null;
            }

            var feed = tracks.Select(t => new FeedItemViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Artists = t.Artists,
                ImageUrl = t.ImageUrl
            }).ToList();

            await FeedStats.ApplyAsync(_reviewService, feed);
            await SavedState.ApplyAsync(_listenLater, feed, userId);
            return feed;
        }
    }
}
