using Microsoft.EntityFrameworkCore;

namespace Heardit.ViewModels
{
    /// <summary>
    /// One page of a listing. There is no total count anywhere in the app — pages are cheap because
    /// we fetch one row past the page to learn whether a next page exists, and the UI only ever offers
    /// "newer"/"older" rather than numbered pages.
    /// </summary>
    public class PagedList<T>
    {
        public const int PageSize = 20;

        public required IReadOnlyList<T> Items { get; init; }

        /// <summary>1-based.</summary>
        public required int Page { get; init; }

        public required bool HasNext { get; init; }

        public bool HasPrevious => Page > 1;

        public int Count => Items.Count;

        public static PagedList<T> Empty(int page = 1) =>
            new() { Items = Array.Empty<T>(), Page = Normalize(page), HasNext = false };

        public static async Task<PagedList<T>> CreateAsync(IQueryable<T> source, int page)
        {
            page = Normalize(page);

            var rows = await source
                .Skip((page - 1) * PageSize)
                .Take(PageSize + 1)
                .ToListAsync();

            var hasNext = rows.Count > PageSize;
            if (hasNext)
            {
                rows.RemoveAt(PageSize);
            }

            return new PagedList<T> { Items = rows, Page = page, HasNext = hasNext };
        }

        private static int Normalize(int page) => page < 1 ? 1 : page;
    }
}
