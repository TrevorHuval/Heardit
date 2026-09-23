namespace Heardit.ViewModels
{
    /// <summary>A homepage section heading: title, optional one-liner, optional "See all" link.</summary>
    public record SectionHeadViewModel(string Id, string Title, string? Subtitle = null, string? SeeAllAction = null);
}
