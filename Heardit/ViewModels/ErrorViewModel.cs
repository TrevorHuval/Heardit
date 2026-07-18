namespace Heardit.ViewModels
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        /// <summary>The original HTTP status code that triggered this page (e.g. 404, 429), if known.</summary>
        public int? StatusCode { get; set; }
    }
}
