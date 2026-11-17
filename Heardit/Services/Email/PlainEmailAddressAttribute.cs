using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace Heardit.Services.Email
{
    /// <summary>
    /// A single bare address (name@example.com) and nothing else. [EmailAddress] only checks for one '@',
    /// so it also passes "Someone &lt;name@example.com&gt;", quoted local parts and "a@b.com,x": forms the mail
    /// library reads as a display name or a recipient list. Those would let two accounts share one inbox
    /// under different spellings, or fail when the message is built.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class PlainEmailAddressAttribute : ValidationAttribute
    {
        public const int MaxLength = 254;

        private const string Forbidden = " \t\r\n\",;:<>()[]\\";

        public PlainEmailAddressAttribute() => ErrorMessage = "That doesn't look like an email address.";

        /// <summary>Empty passes, as with the built-in attributes; pair with [Required] where it's needed.</summary>
        public override bool IsValid(object? value) => value switch
        {
            null => true,
            string text => text.Trim().Length == 0 || IsPlainAddress(text.Trim()),
            _ => false
        };

        public static bool IsPlainAddress(string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
            {
                return false;
            }

            foreach (var c in value)
            {
                if (char.IsControl(c) || char.IsWhiteSpace(c) || Forbidden.Contains(c))
                {
                    return false;
                }
            }

            return MailAddress.TryCreate(value, out var parsed)
                && string.Equals(parsed.Address, value, StringComparison.Ordinal)
                && parsed.Host.Contains('.');
        }
    }
}
