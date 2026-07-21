namespace Heardit.ViewModels
{
    /// <summary>What _Pager needs to draw the two links. Labels vary because not every list is chronological.</summary>
    public class PagerViewModel
    {
        public required int Page { get; init; }

        public required bool HasNext { get; init; }

        public bool HasPrevious => Page > 1;

        public string PreviousLabel { get; init; } = "Newer";

        public string NextLabel { get; init; } = "Older";

        public static PagerViewModel For<T>(PagedList<T> page) =>
            new() { Page = page.Page, HasNext = page.HasNext };
    }
}
