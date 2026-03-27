using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Interfaces;

/// <summary>
/// Handles certificate issuance, revocation, and PDF regeneration.
/// </summary>
public interface ICertificateService
{
    /// <summary>
    /// Issues a certificate for a completed course enrollment.
    /// Idempotent: returns the existing certificate if one has already been issued.
    /// Throws <see cref="InvalidOperationException"/> when the enrollment is not found or not completed.
    /// </summary>
    Task<Certificate> GetOrGenerateAsync( string userId , Guid courseId , CancellationToken cancellationToken = default );

    /// <summary>
    /// Revokes an active certificate.
    /// Throws <see cref="KeyNotFoundException"/> when the certificate does not exist.
    /// </summary>
    Task RevokeAsync( Guid certificateId , CancellationToken cancellationToken = default );

    /// <summary>
    /// Deletes and regenerates the PDF for an existing certificate.
    /// Throws <see cref="KeyNotFoundException"/> when the certificate does not exist.
    /// </summary>
    Task RegenerateAsync( Guid certificateId , CancellationToken cancellationToken = default );
}
