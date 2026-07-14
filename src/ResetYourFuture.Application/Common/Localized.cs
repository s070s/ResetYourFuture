using System.Diagnostics.CodeAnalysis;

namespace ResetYourFuture.Application.Common;

/// <summary>
/// The bilingual (En/El) content-resolution rule used by every entity with an *En/*El pair
/// of fields: prefer Greek when the caller's language is Greek, falling back to English when
/// no Greek translation exists; otherwise use English. Centralizes what was previously a
/// hand-repeated <c>isEl ? (x.El ?? x.En) : x.En</c> ternary (CQ-1).
///
/// Not usable inside an <see cref="System.Linq.Expressions.Expression"/> tree passed to
/// <c>IQueryable.Select</c> — EF Core cannot translate an arbitrary method call to SQL, so the
/// few projections used there (<c>AssessmentMappings.StudentProjection</c>,
/// <c>CertificateMappings.Projection</c>) keep the inline ternary deliberately.
/// </summary>
public static class Localized
{
    public static bool IsEl(string? lang) =>
        string.Equals(lang, "el", StringComparison.OrdinalIgnoreCase);

    [return: NotNullIfNotNull(nameof(en))]
    public static string? Pick(bool isEl, string? en, string? el) =>
        isEl ? (el ?? en) : en;
}
