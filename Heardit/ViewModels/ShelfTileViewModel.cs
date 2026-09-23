using Heardit.Services;

namespace Heardit.ViewModels
{
    /// <summary>One album-art tile on a homepage shelf. Links to the song page; no player.</summary>
    public class ShelfTileViewModel
    {
        public required string Id { get; init; }

        public required string Title { get; init; }

        public string Artist { get; init; } = string.Empty;

        public string? ImageUrl { get; init; }

        /// <summary>Average rating, shown as a score chip when the track has reviews.</summary>
        public decimal? Average { get; init; }

        public int ReviewCount { get; init; }

        public static ShelfTileViewModel From(FeedItemViewModel item) => new()
        {
            Id = item.Id,
            Title = item.Name,
            Artist = item.Artists,
            ImageUrl = item.ImageUrl,
            Average = item.ReviewCount > 0 ? item.AverageRating : null,
            ReviewCount = item.ReviewCount
        };

        public static ShelfTileViewModel From(TrendingSong trending) => new()
        {
            Id = trending.Song.Id,
            Title = trending.Song.Title ?? string.Empty,
            Artist = trending.Song.Artist ?? string.Empty,
            ImageUrl = string.IsNullOrWhiteSpace(trending.Song.AlbumArt) ? null : trending.Song.AlbumArt,
            Average = trending.ReviewCount > 0 ? trending.Average : null,
            ReviewCount = trending.ReviewCount
        };
    }
}
