using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Options;

namespace Heardit.Services.Email
{
    public record EmailMessage(string To, string Subject, string Html, string Text);

    public interface IEmailSender
    {
        /// <summary>False when the message could not be handed off; callers tell the user, never the address.</summary>
        Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }

    /// <summary>Plain SMTP with STARTTLS, which is what Amazon SES's SMTP endpoint speaks.</summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(_options.From!, _options.FromName),
                Subject = message.Subject,
                Body = message.Text,
                IsBodyHtml = false
            };
            mail.To.Add(message.To);
            // Text first, HTML second: clients show the last alternative they understand.
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Html, null, MediaTypeNames.Text.Html));

            using var client = new SmtpClient(_options.Host!, _options.Port) { EnableSsl = true };
            if (!string.IsNullOrEmpty(_options.Username))
            {
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            try
            {
                await client.SendMailAsync(mail, cancellationToken);
                return true;
            }
            catch (SmtpException ex)
            {
                // The recipient stays out of the log; the exception says enough about what went wrong.
                _logger.LogError(ex, "Could not send the \"{Subject}\" email.", message.Subject);
                return false;
            }
        }
    }

    /// <summary>
    /// Development stand-in: writes the email's text (with its links) to the console instead of sending
    /// it, so every account flow can be clicked through locally. Never registered outside Development.
    /// </summary>
    public class ConsoleEmailSender : IEmailSender
    {
        private readonly ILogger<ConsoleEmailSender> _logger;

        public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) => _logger = logger;

        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("DEV EMAIL (not sent) \"{Subject}\"\n{Text}", message.Subject, message.Text);
            return Task.FromResult(true);
        }
    }

    /// <summary>Production without mail configured: nothing can be sent, and the pages say so.</summary>
    public class DisabledEmailSender : IEmailSender
    {
        private readonly ILogger<DisabledEmailSender> _logger;

        public DisabledEmailSender(ILogger<DisabledEmailSender> logger) => _logger = logger;

        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Email is not configured; the \"{Subject}\" email was not sent.", message.Subject);
            return Task.FromResult(false);
        }
    }
}
