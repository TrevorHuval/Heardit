using System.Globalization;
using System.Security.Claims;
using System.Text;
using Heardit.Areas.Identity.Data;

namespace Heardit.Services
{
    public enum ExternalSignUpOutcome
    {
        /// <summary>No account has this email: ask for a username and create one.</summary>
        CreateNew,

        /// <summary>Both sides have verified the same address: attach the Google login and sign in.</summary>
        LinkToExisting,

        /// <summary>
        /// An account has this email but one side hasn't proven it. Linking would let whoever registered
        /// the address first into the other person's account, so they must sign in with their password
        /// and connect Google from Settings instead.
        /// </summary>
        SignInFirst
    }

    /// <summary>The rules for signing in with an outside provider (Google).</summary>
    public static class ExternalAccounts
    {
        /// <summary>Claim the Google handler maps from the userinfo "verified_email" field.</summary>
        public const string EmailVerifiedClaim = "urn:google:email_verified";

        public static bool ProviderVerifiedEmail(ClaimsPrincipal principal) =>
            string.Equals(principal.FindFirstValue(EmailVerifiedClaim), "true", StringComparison.OrdinalIgnoreCase);

        public static ExternalSignUpOutcome Decide(HearditUser? accountWithSameEmail, bool providerVerifiedEmail)
        {
            if (accountWithSameEmail == null)
            {
                return ExternalSignUpOutcome.CreateNew;
            }

            return accountWithSameEmail.EmailConfirmed && providerVerifiedEmail
                ? ExternalSignUpOutcome.LinkToExisting
                : ExternalSignUpOutcome.SignInFirst;
        }

        /// <summary>
        /// A starting username from the provider's display name or email, shaped to the username rules.
        /// The person can change it before the account is created; uniqueness is checked there.
        /// </summary>
        public static string SuggestUserName(string? displayName, string? email)
        {
            var source = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : email?.Split('@')[0].Split('+')[0] ?? string.Empty;   // drop a "+tag" from me+tag@x

            // "José Núñez" → "Jose.Nunez": drop accents, turn spaces into dots, keep only allowed characters.
            var normalized = source.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    builder.Append('.');
                }
                else if (UserNameRules.IsAllowed(c))
                {
                    builder.Append(c);
                }
            }

            var suggestion = builder.ToString().Trim('.', '-', '_');
            while (suggestion.Contains(".."))
            {
                suggestion = suggestion.Replace("..", ".");
            }

            if (suggestion.Length > UserNameRules.MaxLength)
            {
                suggestion = suggestion[..UserNameRules.MaxLength].TrimEnd('.', '-', '_');
            }

            return suggestion.Length >= UserNameRules.MinLength ? suggestion : suggestion.PadRight(UserNameRules.MinLength, '1');
        }
    }

    /// <summary>What a username may look like. Mirrors IdentityOptions.User.AllowedUserNameCharacters.</summary>
    public static class UserNameRules
    {
        public const int MinLength = 4;
        public const int MaxLength = 30;
        public const string Pattern = "^[A-Za-z0-9._-]+$";
        public const string Hint = "4–30 letters, numbers, dots, dashes or underscores.";

        public static bool IsAllowed(char c) =>
            c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '_' or '-';
    }
}
