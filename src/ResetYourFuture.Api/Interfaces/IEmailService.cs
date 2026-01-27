namespace ResetYourFuture.Api.Interfaces;

/// <summary>
/// Abstraction for sending transactional emails.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email confirmation link to a user.
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="confirmationLink">Email confirmation URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendEmailConfirmationAsync(string email, string confirmationLink, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a password reset link to a user.
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="resetLink">Password reset URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendPasswordResetAsync(string email, string resetLink, CancellationToken cancellationToken = default);
}
