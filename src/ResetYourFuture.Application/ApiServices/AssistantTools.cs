using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.ApiServices;

/// <inheritdoc cref="IAssistantTools"/>
/// <remarks>
/// Every query is AsNoTracking, scoped to the captured user, published-only for content,
/// and size-capped so each tool result stays compact (≤ ~1 KB) — the model gets summaries,
/// never raw records. The public per-tool methods exist so unit tests can call the data
/// logic directly without invoking AIFunctions.
/// </remarks>
public class AssistantTools(IApplicationDbContext db) : IAssistantTools
{
    private const int MaxListItems = 10;
    private const int MaxSearchResults = 5;
    private const int MaxDescriptionLength = 200;

    public IReadOnlyList<AITool> GetToolsForUser(string userId, string language)
    {
        // Defense in depth: the controller is [Authorize], but an empty identity must
        // never get a data-reading tool surface.
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var isEl = string.Equals(language, "el", StringComparison.OrdinalIgnoreCase);

        return
        [
            AIFunctionFactory.Create(
                (CancellationToken ct) => GetMyEnrollmentsAsync(userId, isEl, ct),
                "get_my_enrollments",
                "Lists the courses the current user is enrolled in, with enrollment dates."),
            AIFunctionFactory.Create(
                (CancellationToken ct) => GetMyProgressAsync(userId, isEl, ct),
                "get_my_progress",
                "Shows the current user's progress per enrolled course: completed vs total lessons."),
            AIFunctionFactory.Create(
                (CancellationToken ct) => GetMyAssessmentResultsAsync(userId, isEl, ct),
                "get_my_assessment_results",
                "Lists the current user's most recent assessment submissions with dates."),
            AIFunctionFactory.Create(
                (CancellationToken ct) => GetSubscriptionStatusAsync(userId, ct),
                "get_subscription_status",
                "Returns the current user's subscription plan, tier and expiry."),
            AIFunctionFactory.Create(
                (string query, int? maxResults, CancellationToken ct) => SearchCoursesAsync(query, maxResults, isEl, ct),
                "search_courses",
                "Searches published courses by a keyword in title or description. maxResults is optional (default 5, max 5)."),
            AIFunctionFactory.Create(
                (CancellationToken ct) => RecommendCoursesAsync(userId, isEl, ct),
                "recommend_courses",
                "Recommends published courses the current user has not enrolled in yet, preferring the categories of their existing courses."),
        ];
    }

    public async Task<List<EnrollmentInfo>> GetMyEnrollmentsAsync(string userId, bool isEl, CancellationToken cancellationToken = default)
    {
        return await db.Enrollments
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.EnrolledAt)
            .Take(MaxListItems)
            .Select(e => new EnrollmentInfo(
                isEl ? (e.Course.TitleEl ?? e.Course.TitleEn) : e.Course.TitleEn,
                e.EnrolledAt.ToString("yyyy-MM-dd")))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProgressInfo>> GetMyProgressAsync(string userId, bool isEl, CancellationToken cancellationToken = default)
    {
        return await db.Enrollments
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.EnrolledAt)
            .Take(MaxListItems)
            .Select(e => new ProgressInfo(
                isEl ? (e.Course.TitleEl ?? e.Course.TitleEn) : e.Course.TitleEn,
                e.Course.Modules.SelectMany(m => m.Lessons)
                    .Count(l => db.LessonCompletions.Any(lc => lc.UserId == userId && lc.LessonId == l.Id)),
                e.Course.Modules.SelectMany(m => m.Lessons).Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AssessmentResultInfo>> GetMyAssessmentResultsAsync(string userId, bool isEl, CancellationToken cancellationToken = default)
    {
        // Titles and dates only — raw answers never reach the model.
        return await db.AssessmentSubmissions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SubmittedAt)
            .Take(MaxSearchResults)
            .Select(s => new AssessmentResultInfo(
                isEl ? (s.AssessmentDefinition.TitleEl ?? s.AssessmentDefinition.TitleEn) : s.AssessmentDefinition.TitleEn,
                s.SubmittedAt.ToString("yyyy-MM-dd")))
            .ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionInfo> GetSubscriptionStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        var active = await db.UserSubscriptions
            .AsNoTracking()
            .Where(us => us.UserId == userId && us.IsActive)
            .Select(us => new
            {
                us.SubscriptionPlan.Name,
                us.SubscriptionPlan.Tier,
                us.ExpiresAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return active is null
            ? new SubscriptionInfo("Free", nameof(SubscriptionTier.Free), null)
            : new SubscriptionInfo(active.Name, active.Tier.ToString(), active.ExpiresAt?.ToString("yyyy-MM-dd"));
    }

    public async Task<List<CourseHit>> SearchCoursesAsync(string query, int? maxResults, bool isEl, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(maxResults ?? MaxSearchResults, 1, MaxSearchResults);
        var term = (query ?? string.Empty).Trim();
        if (term.Length == 0)
            return [];

        return await db.Courses
            .AsNoTracking()
            .Where(c => c.IsPublished)
            .Where(c => c.TitleEn.Contains(term)
                || (c.TitleEl != null && c.TitleEl.Contains(term))
                || (c.DescriptionEn != null && c.DescriptionEn.Contains(term))
                || (c.DescriptionEl != null && c.DescriptionEl.Contains(term)))
            .OrderBy(c => c.TitleEn)
            .Take(take)
            .Select(c => new CourseHit(
                isEl ? (c.TitleEl ?? c.TitleEn) : c.TitleEn,
                Truncate(isEl ? (c.DescriptionEl ?? c.DescriptionEn) : c.DescriptionEn),
                c.RequiredTier.ToString()))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CourseHit>> RecommendCoursesAsync(string userId, bool isEl, CancellationToken cancellationToken = default)
    {
        var enrolledCourseIds = db.Enrollments
            .Where(e => e.UserId == userId)
            .Select(e => e.CourseId);

        var enrolledCategoryIds = db.Enrollments
            .Where(e => e.UserId == userId && e.Course.CategoryId != null)
            .Select(e => e.Course.CategoryId);

        var candidates = db.Courses
            .AsNoTracking()
            .Where(c => c.IsPublished && !enrolledCourseIds.Contains(c.Id));

        var inMyCategories = await candidates
            .Where(c => c.CategoryId != null && enrolledCategoryIds.Contains(c.CategoryId))
            .OrderBy(c => c.TitleEn)
            .Take(MaxSearchResults)
            .Select(c => new CourseHit(
                isEl ? (c.TitleEl ?? c.TitleEn) : c.TitleEn,
                Truncate(isEl ? (c.DescriptionEl ?? c.DescriptionEn) : c.DescriptionEn),
                c.RequiredTier.ToString()))
            .ToListAsync(cancellationToken);

        if (inMyCategories.Count > 0)
            return inMyCategories;

        // New user (or exhausted categories): any published course they don't have yet.
        return await candidates
            .OrderBy(c => c.TitleEn)
            .Take(MaxSearchResults)
            .Select(c => new CourseHit(
                isEl ? (c.TitleEl ?? c.TitleEn) : c.TitleEn,
                Truncate(isEl ? (c.DescriptionEl ?? c.DescriptionEn) : c.DescriptionEn),
                c.RequiredTier.ToString()))
            .ToListAsync(cancellationToken);
    }

    private static string? Truncate(string? text) =>
        text is null || text.Length <= MaxDescriptionLength ? text : text[..MaxDescriptionLength] + "…";

    public sealed record EnrollmentInfo(string Course, string EnrolledOn);
    public sealed record ProgressInfo(string Course, int CompletedLessons, int TotalLessons);
    public sealed record AssessmentResultInfo(string Assessment, string SubmittedOn);
    public sealed record SubscriptionInfo(string Plan, string Tier, string? ExpiresOn);
    public sealed record CourseHit(string Title, string? Description, string RequiredTier);
}
