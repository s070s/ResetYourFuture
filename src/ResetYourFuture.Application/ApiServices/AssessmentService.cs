using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using System.Text.Json;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// Student-facing assessment discovery and submission.
/// </summary>
public class AssessmentService(
    IApplicationDbContext db,
    ISubscriptionService subscriptionService,
    IMemoryCache cache,
    ILogger<AssessmentService> logger) : IAssessmentService
{
    private const string ForbiddenMessage = "Assessment access requires a Plus subscription or higher.";

    public async Task<ServiceResult<PagedResult<AssessmentDefinitionDto>>> GetPublishedAssessmentsAsync(
        string userId, int page, int pageSize, string lang, CancellationToken cancellationToken = default)
    {
        var userStatus = await subscriptionService.GetUserStatusAsync(userId, cancellationToken);
        if (userStatus.Features?.AssessmentAccess != true)
            return ServiceResult<PagedResult<AssessmentDefinitionDto>>.Forbidden(error: ForbiddenMessage);

        var isEl = string.Equals(lang, "el", StringComparison.OrdinalIgnoreCase);

        var query = db.AssessmentDefinitions
            .AsNoTracking()
            .Where(a => a.IsPublished)
            .OrderBy(a => a.TitleEn);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AssessmentDefinitionDto(
                a.Id,
                a.Key,
                isEl ? (a.TitleEl ?? a.TitleEn) : a.TitleEn,
                isEl ? (a.DescriptionEl ?? a.DescriptionEn) : a.DescriptionEn,
                a.SchemaJson,
                a.IsPublished,
                a.CreatedAt,
                a.UpdatedAt,
                a.PublishedAt
            ))
            .ToListAsync(cancellationToken);

        // Resolve dual-language schema to single-language for student view.
        // Cached per (assessmentId, lang) — avoids re-allocating JsonDocument/MemoryStream on every page load.
        var resolved = items.Select(a => a with { SchemaJson = GetCachedResolvedSchema(a.Id, a.SchemaJson, isEl) }).ToList();

        return ServiceResult<PagedResult<AssessmentDefinitionDto>>.Ok(
            new PagedResult<AssessmentDefinitionDto>(resolved, totalCount, page, pageSize));
    }

    public async Task<ServiceResult<AssessmentDefinitionDto>> GetAssessmentAsync(
        string userId, Guid id, string lang, CancellationToken cancellationToken = default)
    {
        var userStatus = await subscriptionService.GetUserStatusAsync(userId, cancellationToken);
        if (userStatus.Features?.AssessmentAccess != true)
            return ServiceResult<AssessmentDefinitionDto>.Forbidden(error: ForbiddenMessage);

        var isEl = string.Equals(lang, "el", StringComparison.OrdinalIgnoreCase);

        var assessment = await db.AssessmentDefinitions
            .AsNoTracking()
            .Where(a => a.Id == id && a.IsPublished)
            .Select(a => new AssessmentDefinitionDto(
                a.Id,
                a.Key,
                isEl ? (a.TitleEl ?? a.TitleEn) : a.TitleEn,
                isEl ? (a.DescriptionEl ?? a.DescriptionEn) : a.DescriptionEn,
                a.SchemaJson,
                a.IsPublished,
                a.CreatedAt,
                a.UpdatedAt,
                a.PublishedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (assessment == null)
            return ServiceResult<AssessmentDefinitionDto>.NotFound();

        // Resolve dual-language schema to single-language for student view (cache hit likely after list page)
        var resolved = assessment with { SchemaJson = GetCachedResolvedSchema(assessment.Id, assessment.SchemaJson, isEl) };

        return ServiceResult<AssessmentDefinitionDto>.Ok(resolved);
    }

    public async Task<ServiceResult<AssessmentSubmissionDto>> SubmitAssessmentAsync(
        string userId, Guid id, SubmitAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        var assessment = await db.AssessmentDefinitions
            .Where(a => a.Id == id && a.IsPublished)
            .FirstOrDefaultAsync(cancellationToken);

        if (assessment == null)
            return ServiceResult<AssessmentSubmissionDto>.NotFound(error: "Assessment not found or not published");

        // Check subscription features and tier
        var userStatus = await subscriptionService.GetUserStatusAsync(userId, cancellationToken);
        if (userStatus.Features?.AssessmentAccess != true)
            return ServiceResult<AssessmentSubmissionDto>.Forbidden(error: ForbiddenMessage);
        if (userStatus.Tier < assessment.RequiredTier)
            return ServiceResult<AssessmentSubmissionDto>.Forbidden(error: $"This assessment requires a {assessment.RequiredTier} subscription or higher.");

        var submission = new AssessmentSubmission
        {
            Id = Guid.NewGuid(),
            AssessmentDefinitionId = id,
            UserId = userId,
            AnswersJson = request.AnswersJson,
            SummaryJson = request.SummaryJson,
            SubmittedAt = DateTimeOffset.UtcNow
        };

        db.AssessmentSubmissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);

        var dto = new AssessmentSubmissionDto(
            submission.Id,
            submission.AssessmentDefinitionId,
            assessment.TitleEn,
            submission.AnswersJson,
            submission.SummaryJson,
            submission.SubmittedAt
        );

        return ServiceResult<AssessmentSubmissionDto>.Ok(dto);
    }

    public async Task<List<AssessmentSubmissionDto>> GetMySubmissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await db.AssessmentSubmissions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Include(s => s.AssessmentDefinition)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new AssessmentSubmissionDto(
                s.Id,
                s.AssessmentDefinitionId,
                s.AssessmentDefinition.TitleEn,
                s.AnswersJson,
                s.SummaryJson,
                s.SubmittedAt
            ))
            .ToListAsync(cancellationToken);
    }

    /// Returns cached resolved schema JSON, or computes and caches it on miss.
    /// Key: (assessmentId, "en"|"el"). TTL: 30 minutes — short enough to pick up admin edits.
    private string GetCachedResolvedSchema(Guid assessmentId, string schemaJson, bool isEl)
    {
        var langKey = isEl ? "el" : "en";
        var cacheKey = $"schema:{assessmentId}:{langKey}";

        return cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return ResolveSchemaJsonByLang(schemaJson, isEl);
        })!;
    }

    /// <summary>
    /// Resolves dual-language schema JSON to a flat single-language format for the student view.
    /// Handles both flat {"questions":[...]} and sectioned {"sections":[{"questions":[...]}]} schemas.
    /// Maps labelEn/labelEl or label/labelEl → label, and optionsEn/optionsEl or options/optionsEl → options.
    /// </summary>
    private string ResolveSchemaJsonByLang(string schemaJson, bool isEl)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;

            // Collect all question elements from either flat or sectioned schema formats.
            var allQuestions = new List<JsonElement>();
            if (root.TryGetProperty("questions", out var flatQ) && flatQ.ValueKind == JsonValueKind.Array)
            {
                foreach (var q in flatQ.EnumerateArray())
                    allQuestions.Add(q);
            }
            else if (root.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array)
            {
                foreach (var section in sections.EnumerateArray())
                {
                    if (section.TryGetProperty("questions", out var sectionQ) && sectionQ.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var q in sectionQ.EnumerateArray())
                            allQuestions.Add(q);
                    }
                }
            }

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false });
            writer.WriteStartObject();

            // Copy non-questions/sections root properties (id, title, version, etc.)
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name is "questions" or "sections")
                    continue;
                prop.WriteTo(writer);
            }

            // Write resolved flat questions array
            writer.WritePropertyName("questions");
            writer.WriteStartArray();
            foreach (var q in allQuestions)
            {
                writer.WriteStartObject();
                bool labelEmitted = false;
                bool optionsEmitted = false;

                foreach (var qProp in q.EnumerateObject())
                {
                    // Skip all Greek-only and secondary locale keys — handled below
                    if (qProp.Name is "labelEl" or "optionsEl" or "titleEl" or "minLabelEl" or "maxLabelEl")
                        continue;

                    // Resolve label: supports both "labelEn" (admin-edit) and "label" (seed) keys
                    if (qProp.Name is "labelEn" or "label")
                    {
                        if (!labelEmitted)
                        {
                            var enLabel = qProp.Value.GetString() ?? "";
                            var elLabel = isEl && q.TryGetProperty("labelEl", out var elV) ? elV.GetString() : null;
                            writer.WriteString("label", elLabel ?? enLabel);
                            labelEmitted = true;
                        }
                        continue;
                    }

                    // Resolve options: supports both "optionsEn" (admin-edit) and "options" (seed) keys
                    if (qProp.Name is "optionsEn" or "options")
                    {
                        if (!optionsEmitted)
                        {
                            var useEl = isEl && q.TryGetProperty("optionsEl", out var elOpts) && elOpts.GetArrayLength() > 0;
                            writer.WritePropertyName("options");
                            if (useEl)
                                q.GetProperty("optionsEl").WriteTo(writer);
                            else
                                qProp.Value.WriteTo(writer);
                            optionsEmitted = true;
                        }
                        continue;
                    }

                    qProp.WriteTo(writer);
                }

                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
            writer.Flush();
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve schema JSON by language; returning original.");
            return schemaJson;
        }
    }
}
