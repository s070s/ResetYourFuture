using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class AssessmentSubmissionSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to an assessment-submission query.
    /// Shared by the admin per-assessment submissions table (user/email/submittedat)
    /// and the student assessment history (title/category/submittedat).
    /// Same pattern as <see cref="UserSearchExtensions"/>: a switch expression keeps
    /// EF Core SQL translation intact, and every branch ends with a stable
    /// .ThenBy(Id) tie-breaker so ordering is deterministic across pages.
    /// Unknown keys fall through to the pre-sorting default: SubmittedAt descending.
    /// </summary>
    public static IQueryable<AssessmentSubmission> ApplySort(
        this IQueryable<AssessmentSubmission> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("user", "desc") => query.OrderByDescending(s => s.User.LastName).ThenByDescending(s => s.User.FirstName),
            ("user", _) => query.OrderBy(s => s.User.LastName).ThenBy(s => s.User.FirstName),
            ("email", "desc") => query.OrderByDescending(s => s.User.Email),
            ("email", _) => query.OrderBy(s => s.User.Email),
            ("title", "desc") => query.OrderByDescending(s => s.AssessmentDefinition.TitleEn),
            ("title", _) => query.OrderBy(s => s.AssessmentDefinition.TitleEn),
            ("category", "desc") => query.OrderByDescending(s =>
                s.AssessmentDefinition.Category != null ? s.AssessmentDefinition.Category.NameEn : null),
            ("category", _) => query.OrderBy(s =>
                s.AssessmentDefinition.Category != null ? s.AssessmentDefinition.Category.NameEn : null),
            ("submittedat", "asc") => query.OrderBy(s => s.SubmittedAt),
            _ => query.OrderByDescending(s => s.SubmittedAt),
        };
        return ordered.ThenBy(s => s.Id);
    }
}
