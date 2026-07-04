using ResetYourFuture.Application.ApiInterfaces;

namespace ResetYourFuture.Infrastructure.ApiServices;

/// <summary>
/// Stub email service for development.
/// Logs emails instead of sending them — no message is ever delivered. This is the only
/// IEmailService registered, and only in Development; production registration fails fast in
/// Program.cs until a real provider (SendGrid/SMTP) is implemented and wired in.
/// </summary>
public class StubEmailService : IEmailService
{
    private readonly ILogger<StubEmailService> _logger;

    public StubEmailService(ILogger<StubEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync(string email, string confirmationLink, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "STUB EMAIL - Email Confirmation:\n" +
            "To: {Email}\n" +
            "Subject: Confirm your email\n" +
            "Link: {ConfirmationLink}",
            email, confirmationLink);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string resetLink, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "STUB EMAIL - Password Reset:\n" +
            "To: {Email}\n" +
            "Subject: Reset your password\n" +
            "Link: {ResetLink}",
            email, resetLink);

        return Task.CompletedTask;
    }
}
