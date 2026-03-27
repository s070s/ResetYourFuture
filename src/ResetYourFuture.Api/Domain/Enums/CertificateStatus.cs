namespace ResetYourFuture.Api.Domain.Enums;

/// <summary>
/// Represents the validity state of an issued certificate.
/// Stored as int; consistent with EnrollmentStatus convention.
/// </summary>
public enum CertificateStatus
{
    Active = 1,
    Revoked = 2
}
