using System.Text.RegularExpressions;

namespace Heardit.Services
{
    /// <summary>Google Analytics 4. The measurement id is public (it ships in every page), so it is plain
    /// configuration, not a secret. Unset or malformed means no tag is emitted at all.</summary>
    public static partial class Analytics
    {
        public const string ConfigKey = "GoogleAnalytics:MeasurementId";

        /// <summary>The id to put in the page, or null when analytics is off or the value isn't a GA4 id.</summary>
        public static string? MeasurementId(string? configured)
        {
            var id = configured?.Trim();
            return id != null && GaId().IsMatch(id) ? id : null;
        }

        [GeneratedRegex("^G-[A-Z0-9]{6,12}$")]
        private static partial Regex GaId();
    }
}
