using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Heardit.Areas.Identity.Pages.Account
{
    /// <summary>Identity tokens contain '+', '/' and '='; links carry them base64url-encoded instead.</summary>
    public static class AccountTokens
    {
        public static string Encode(string token) =>
            WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        /// <summary>The raw token, or null when the link's code was mangled (a mail client wrapping it, say).</summary>
        public static string? Decode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            try
            {
                return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
