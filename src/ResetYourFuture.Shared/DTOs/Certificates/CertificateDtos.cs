namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Student-facing certificate summary. Course title is pre-resolved to the requested language.
/// </summary>
public record CertificateDto(
    Guid Id,
    Guid VerificationId,
    string RecipientName,
    string CourseTitle,
    DateTime IssuedAt,
    string Status,
    string VerificationUrl
);

/// <summary>
/// Response for the public certificate verification endpoint. No authentication required.
/// Personal details are omitted when the certificate is revoked.
/// </summary>
public record CertificateVerificationDto(
    bool IsValid,
    string? RecipientName,
    string? CourseTitle,
    DateTime? IssuedAt,
    string? Status,
    string? Message
);

/// <summary>
/// Admin list item. Includes user email and all fields needed for the management table.
/// </summary>
public record AdminCertificateListItemDto(
    Guid Id,
    Guid VerificationId,
    string RecipientName,
    string UserEmail,
    string CourseTitle,
    DateTime IssuedAt,
    string Status
);
