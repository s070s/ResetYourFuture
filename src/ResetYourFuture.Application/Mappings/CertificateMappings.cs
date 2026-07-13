using System.Linq.Expressions;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.Mappings;

/// <summary>
/// Shared certificate entity→DTO mappers (MAINT-1). CertificateDto's language-resolved
/// projection was duplicated between the list query and the issue endpoint, and the
/// verification mapping (with its revoked-state gating) lived inline in the controller.
/// Keep the projection/extension pair's field order in sync.
/// </summary>
public static class CertificateMappings
{
    /// <summary>Language-resolved certificate row, for IQueryable.Select.</summary>
    public static Expression<Func<Certificate, CertificateDto>> Projection(bool isEl) =>
        c => new CertificateDto(
            c.Id,
            c.VerificationId,
            c.RecipientName,
            isEl ? (c.CourseTitleEl ?? c.CourseTitleEn) : c.CourseTitleEn,
            c.IssuedAt,
            c.Status.ToString());

    public static CertificateDto ToDto(this Certificate c, bool isEl) =>
        new(c.Id, c.VerificationId, c.RecipientName,
            isEl ? (c.CourseTitleEl ?? c.CourseTitleEn) : c.CourseTitleEn,
            c.IssuedAt, c.Status.ToString());

    /// <summary>
    /// Public verification view: a revoked certificate discloses only its status, never the
    /// recipient or course.
    /// </summary>
    public static CertificateVerificationDto ToVerificationDto(this Certificate c, bool isEl)
    {
        var isRevoked = c.Status == CertificateStatus.Revoked;
        var courseTitle = isEl ? (c.CourseTitleEl ?? c.CourseTitleEn) : c.CourseTitleEn;

        return new CertificateVerificationDto(
            !isRevoked,
            isRevoked ? null : c.RecipientName,
            isRevoked ? null : courseTitle,
            isRevoked ? null : c.IssuedAt,
            c.Status.ToString(),
            isRevoked ? "This certificate has been revoked." : "Certificate is valid.");
    }
}
