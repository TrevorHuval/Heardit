using Microsoft.AspNetCore.Mvc;

namespace Heardit.Services
{
    /// <summary>
    /// A returnUrl arrives from the query string or a form, so it is only used once it is known to be a
    /// path on this site. Anything else (another host, "//host", "/\host") is treated as absent.
    /// </summary>
    public static class ReturnUrls
    {
        public static string? LocalOrNull(this IUrlHelper url, string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl) ? returnUrl : null;

        public static string LocalOrHome(this IUrlHelper url, string? returnUrl) =>
            url.LocalOrNull(returnUrl) ?? url.Content("~/");
    }
}
