using System.Linq.Expressions;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Application.Mappings;

/// <summary>
/// Shared assessment entity→DTO mappers (MAINT-1). The student-facing definition projection
/// existed as two byte-identical 11-field copies in AssessmentService, the admin DTO three
/// times in AdminAssessmentsController, and the submission DTO twice — one home each now.
/// Expression-returning members stay EF-translatable for in-query use; the plain extensions
/// serve post-materialization call sites. Keep each pair's field order in sync.
/// </summary>
public static class AssessmentMappings
{
    /// <summary>Student view of a definition, language-resolved. For IQueryable.Select.</summary>
    public static Expression<Func<AssessmentDefinition, AssessmentDefinitionDto>> StudentProjection(bool isEl) =>
        a => new AssessmentDefinitionDto(
            a.Id,
            a.Key,
            isEl ? (a.TitleEl ?? a.TitleEn) : a.TitleEn,
            isEl ? (a.DescriptionEl ?? a.DescriptionEn) : a.DescriptionEn,
            a.SchemaJson,
            a.IsPublished,
            a.CreatedAt,
            a.UpdatedAt,
            a.PublishedAt,
            a.CategoryId,
            a.CategoryId == null ? null : (isEl ? (a.Category!.NameEl ?? a.Category.NameEn) : a.Category!.NameEn));

    /// <summary>Admin view (both languages). Category name is passed in because the three
    /// call sites resolve it differently (navigation vs a separate lookup after save).</summary>
    public static AdminAssessmentDefinitionDto ToAdminDto(this AssessmentDefinition a, string? categoryNameEn) =>
        new(a.Id, a.Key, a.TitleEn, a.TitleEl, a.DescriptionEn, a.DescriptionEl, a.SchemaJson,
            a.IsPublished, a.CreatedAt, a.UpdatedAt, a.PublishedAt, a.CategoryId, categoryNameEn);

    /// <summary>Submission list row (with category), for IQueryable.Select.</summary>
    public static readonly Expression<Func<AssessmentSubmission, AssessmentSubmissionDto>> SubmissionProjection =
        s => new AssessmentSubmissionDto(
            s.Id,
            s.AssessmentDefinitionId,
            s.AssessmentDefinition.TitleEn,
            s.AnswersJson,
            s.SummaryJson,
            s.SubmittedAt,
            s.AssessmentDefinition.Category != null ? s.AssessmentDefinition.Category.NameEn : null);

    /// <summary>Post-materialization variant (e.g. right after submit, where the definition
    /// entity is already in hand and no category is needed).</summary>
    public static AssessmentSubmissionDto ToDto(this AssessmentSubmission s, string assessmentTitle, string? categoryName = null) =>
        new(s.Id, s.AssessmentDefinitionId, assessmentTitle, s.AnswersJson, s.SummaryJson, s.SubmittedAt, categoryName);
}
