using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Heardit.Controllers
{
    /// <summary>
    /// Turns a failed antiforgery check (normally a bare 400) into a redirect with a note. It happens when
    /// a form was loaded under a different sign-in state than the one it's posted with: a second tab that
    /// logged in or out, the Back button onto a login page, a page left open across a deploy. Signing in
    /// or up while already signed in just lands on the destination.
    /// </summary>
    public sealed class AntiforgeryFailureFilter : IAlwaysRunResultFilter
    {
        private readonly ITempDataDictionaryFactory _tempData;
        private readonly ILogger<AntiforgeryFailureFilter> _logger;

        public AntiforgeryFailureFilter(ITempDataDictionaryFactory tempData, ILogger<AntiforgeryFailureFilter> logger)
        {
            _tempData = tempData;
            _logger = logger;
        }

        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is not IAntiforgeryValidationFailedResult)
            {
                return;
            }

            var http = context.HttpContext;
            var path = http.Request.Path.Value ?? "/";
            var signedIn = http.User.Identity?.IsAuthenticated == true;
            _logger.LogWarning("Antiforgery check failed on {Path} (signed in: {SignedIn}).", path, signedIn);

            var isAuthForm = path.StartsWith("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase);

            if (isAuthForm && signedIn)
            {
                // Already in (another tab got there first): carry on to where they were going.
                var returnUrl = http.Request.Query["returnUrl"].ToString();
                context.Result = new LocalRedirectResult(IsLocal(returnUrl) ? returnUrl : "~/");
                return;
            }

            // The request was short-circuited before MVC's TempData saver runs, so save the note here.
            var tempData = _tempData.GetTempData(http);
            tempData["FlashError"] = isAuthForm
                ? "That page had gone stale. Please try again."
                : "That page was out of date, so nothing was saved. Please try again.";
            tempData.Save();

            // Back to the page the form was on, as a fresh GET that issues a new token. Actions like
            // "like" are POST-only, so the posted URL itself is only a fallback for the account pages.
            context.Result = new LocalRedirectResult(
                SameSiteReferrer(http) ?? (isAuthForm ? "~" + path + http.Request.QueryString : "~/"));
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
        }

        private static string? SameSiteReferrer(HttpContext http)
        {
            if (!Uri.TryCreate(http.Request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
                || !string.Equals(referer.Host, http.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // The referrer's path already includes the path base (/heardit), so it redirects as-is.
            var local = referer.PathAndQuery;
            return IsLocal(local) ? local : null;
        }

        private static bool IsLocal(string url) =>
            !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");
    }
}
