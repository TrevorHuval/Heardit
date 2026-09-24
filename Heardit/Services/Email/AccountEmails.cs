using System.Net;

namespace Heardit.Services.Email
{
    /// <summary>The handful of emails the account system sends, in the app's voice.</summary>
    public interface IAccountEmails
    {
        Task<bool> SendVerificationAsync(string to, string userName, string link);

        Task<bool> SendPasswordResetAsync(string to, string userName, string link);

        Task<bool> SendEmailChangeAsync(string to, string userName, string link);
    }

    public class AccountEmails : IAccountEmails
    {
        private readonly IEmailSender _sender;

        public AccountEmails(IEmailSender sender) => _sender = sender;

        public Task<bool> SendVerificationAsync(string to, string userName, string link) =>
            _sender.SendAsync(Compose(
                to,
                "Confirm your email for Heardit",
                userName,
                "Confirm this is your email address so you can start rating tracks, liking reviews and following other listeners.",
                "Confirm email",
                link,
                "If you didn't create a Heardit account, you can ignore this email."));

        public Task<bool> SendPasswordResetAsync(string to, string userName, string link) =>
            _sender.SendAsync(Compose(
                to,
                "Reset your Heardit password",
                userName,
                "Someone asked to reset the password for your Heardit account. The link works for one day.",
                "Choose a new password",
                link,
                "If that wasn't you, ignore this email; your password stays the same."));

        public Task<bool> SendEmailChangeAsync(string to, string userName, string link) =>
            _sender.SendAsync(Compose(
                to,
                "Confirm your new email for Heardit",
                userName,
                "Confirm this address to make it the email on your Heardit account.",
                "Confirm new email",
                link,
                "If you didn't ask for this, ignore this email; nothing changes until the link is used."));

        public static EmailMessage Compose(string to, string subject, string userName, string lead, string action, string link, string footer)
        {
            var name = WebUtility.HtmlEncode(userName);
            var href = WebUtility.HtmlEncode(link);

            var html = $"""
                <!doctype html>
                <html><body style="margin:0;padding:32px 16px;background:#0b0b0d;font-family:-apple-system,Segoe UI,Helvetica,Arial,sans-serif;color:#f5f5f6">
                  <div style="max-width:480px;margin:0 auto;background:#151517;border:1px solid #27272b;border-radius:16px;padding:32px">
                    <p style="margin:0 0 24px;font-size:18px;font-weight:800;color:#1db954">Heardit</p>
                    <p style="margin:0 0 12px;font-size:16px">Hi {name},</p>
                    <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#bdbdc2">{WebUtility.HtmlEncode(lead)}</p>
                    <p style="margin:0 0 24px"><a href="{href}" style="display:inline-block;background:#1db954;color:#06210f;text-decoration:none;font-weight:700;padding:12px 22px;border-radius:999px">{WebUtility.HtmlEncode(action)}</a></p>
                    <p style="margin:0 0 8px;font-size:13px;color:#8b8b92">Or paste this link into your browser:</p>
                    <p style="margin:0 0 24px;font-size:13px;word-break:break-all"><a href="{href}" style="color:#1db954">{href}</a></p>
                    <p style="margin:0;font-size:13px;color:#8b8b92">{WebUtility.HtmlEncode(footer)}</p>
                  </div>
                </body></html>
                """;

            var text = $"""
                Hi {userName},

                {lead}

                {action}: {link}

                {footer}
                """;

            return new EmailMessage(to, subject, html, text);
        }
    }
}
