using Heardit.Areas.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Heardit.Controllers
{
    /// <summary>
    /// Guards the public actions (review, like, follow) for accounts that still have to confirm their
    /// email. The listener is sent back where they came from with a note, rather than to an error page.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RequireVerifiedEmailAttribute : ActionFilterAttribute
    {
        public const string Message =
            "Confirm your email to review, like and follow. Check your inbox, or send a new link from Settings.";

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.User.CanPostReviews())
            {
                return;
            }

            if (context.Controller is Controller controller)
            {
                controller.TempData["FlashError"] = Message;
            }
            else
            {
                var factory = context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>();
                factory.GetTempData(context.HttpContext)["FlashError"] = Message;
            }

            context.Result = new LocalRedirectResult(BackTo(context));
        }

        /// <summary>The posted returnUrl, else the page the form was on, else home. Only ever a local path.</summary>
        private static string BackTo(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;
            var urlHelper = context.HttpContext.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Mvc.Routing.IUrlHelperFactory>()
                .GetUrlHelper(context);

            if (request.HasFormContentType && request.Form.TryGetValue("returnUrl", out var posted)
                && urlHelper.IsLocalUrl(posted.ToString()))
            {
                return posted.ToString();
            }

            // A same-host referrer's path already includes the path base, so it redirects as-is.
            if (Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
                && string.Equals(referer.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
                && urlHelper.IsLocalUrl(referer.PathAndQuery))
            {
                return referer.PathAndQuery;
            }

            return "~/";
        }
    }
}
