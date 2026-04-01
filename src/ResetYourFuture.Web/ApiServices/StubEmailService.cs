using ResetYourFuture.Web.ApiInterfaces;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Stub email service for development.
/// Logs emails instead of sending them.
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
