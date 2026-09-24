namespace Heardit.Services.Email
{
    /// <summary>
    /// Outgoing mail over SMTP (Amazon SES's SMTP interface in production). Bound from the "Email"
    /// section: Email__Host, Email__Username, Email__Password, Email__From... Unset Host means no mail.
    /// </summary>
    public class EmailOptions
    {
        public const string SectionName = "Email";

        /// <summary>e.g. email-smtp.us-east-1.amazonaws.com</summary>
        public string? Host { get; set; }

        /// <summary>587 uses STARTTLS, which SES supports.</summary>
        public int Port { get; set; } = 587;

        public string? Username { get; set; }

        public string? Password { get; set; }

        /// <summary>Sender address, e.g. noreply@trevorhuval.com. Must be a verified SES identity.</summary>
        public string? From { get; set; }

        public string FromName { get; set; } = "Heardit";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
    }
}
