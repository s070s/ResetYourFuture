using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ResetYourFuture.Web.ApiInterfaces;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Sends transactional email over SMTP using MailKit. Provider-agnostic: works against a local
/// Papercut/Mailhog catcher in development and any relay (SES, SendGrid SMTP, Mailgun, O365) in
/// production. Configured via <see cref="EmailOptions"/> (section "Email").
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService( IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger )
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync( string email, string confirmationLink, CancellationToken cancellationToken = default )
    {
        var safeLink = System.Net.WebUtility.HtmlEncode( confirmationLink );
        var html = $"<p>Welcome to Reset Your Future. Please confirm your email address:</p>" +
                   $"<p><a href=\"{safeLink}\">Confirm my email</a></p>" +
                   $"<p>If the link does not work, copy this URL into your browser:<br>{safeLink}</p>";
        var text = $"Welcome to Reset Your Future. Confirm your email: {confirmationLink}";
        return SendAsync( email, "Confirm your email", html, text, cancellationToken );
    }

    public Task SendPasswordResetAsync( string email, string resetLink, CancellationToken cancellationToken = default )
    {
        var safeLink = System.Net.WebUtility.HtmlEncode( resetLink );
        var html = $"<p>We received a request to reset your password.</p>" +
                   $"<p><a href=\"{safeLink}\">Reset my password</a></p>" +
                   $"<p>If you did not request this, you can ignore this email. " +
                   $"If the link does not work, copy this URL into your browser:<br>{safeLink}</p>";
        var text = $"Reset your Reset Your Future password: {resetLink}";
        return SendAsync( email, "Reset your password", html, text, cancellationToken );
    }

    private async Task SendAsync( string to, string subject, string htmlBody, string textBody, CancellationToken ct )
    {
        var message = BuildMessage( _options, to, subject, htmlBody, textBody );

        using var client = new SmtpClient();
        try
        {
            var socketOptions = _options.Smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync( _options.Smtp.Host, _options.Smtp.Port, socketOptions, ct );
            if ( !string.IsNullOrEmpty( _options.Smtp.Username ) )
                await client.AuthenticateAsync( _options.Smtp.Username, _options.Smtp.Password ?? string.Empty, ct );
            await client.SendAsync( message, ct );
        }
        finally
        {
            if ( client.IsConnected )
                await client.DisconnectAsync( true, ct );
        }

        _logger.LogInformation( "Email '{Subject}' sent to {To}.", subject, to );
    }

    /// <summary>Builds the MIME message. Extracted so message construction is unit-testable
    /// without a live SMTP server.</summary>
    public static MimeMessage BuildMessage( EmailOptions options, string to, string subject, string htmlBody, string textBody )
    {
        var message = new MimeMessage();
        message.From.Add( new MailboxAddress( options.Smtp.FromName, options.Smtp.FromAddress ) );
        message.To.Add( MailboxAddress.Parse( to ) );
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();
        return message;
    }
}
