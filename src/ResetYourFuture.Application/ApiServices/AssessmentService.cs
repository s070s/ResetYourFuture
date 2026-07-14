using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.Mappings;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;
using ResetYourFuture.Shared.Resources.Messages;
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
    public async Task<ServiceResult<PagedResult<AssessmentDefinitionDto>>> GetPublishedAssessmentsAsync(
        string userId, int page, int pageSize, string lang, Guid? categoryId = null, string? search = null,
        CancellationToken cancellationToken = default)
    {
        var userStatus = await subscriptionService.GetUserStatusAsync(userId, cancellationToken);
        if (userStatus.Features?.AssessmentAccess != true)
            return ServiceResult<PagedResult<AssessmentDefinitionDto>>.Forbidden(error: ErrorMessagesRes.AssessmentAccessRequiresPlus);

        var isEl = Localized.IsEl(lang);

        var query = db.AssessmentDefinitions
            .AsNoTracking()
            .Where(a => a.IsPublished);

        if (categoryId is { } catId)
            query = query.Where(a => a.CategoryId == catId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // EF.Functions.Like translates to a sargable LIKE predicate.
            // Explicit ToLower() is dropped — SQL Server's default CI_AS collation
            // makes LIKE case-insensitive without defeating the index.
            var term = $"%{search.Trim()}%";
            query = query.Where(a =>
                EF.Functions.Like(a.TitleEn, term) || (a.TitleEl != null && EF.Functions.Like(a.TitleEl, term)) ||
                (a.DescriptionEn != null && EF.Functions.Like(a.DescriptionEn, term)) ||
                (a.DescriptionEl != null && EF.Functions.Like(a.DescriptionEl, term)));
        }

        query = query.OrderBy(a => a.TitleEn);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(AssessmentMappings.StudentProjection(isEl))
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
            return ServiceResult<AssessmentDefinitionDto>.Forbidden(error: ErrorMessagesRes.AssessmentAccessRequiresPlus);

        var isEl = Localized.IsEl(lang);

        var assessment = await db.AssessmentDefinitions
            .AsNoTracking()
            .Where(a => a.Id == id && a.IsPublished)
            .Select(AssessmentMappings.StudentProjection(isEl))
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
            return ServiceResult<AssessmentSubmissionDto>.NotFound(error: ErrorMessagesRes.AssessmentNotFoundOrUnpublished);

        // Check subscription features and tier
        var userStatus = await subscriptionService.GetUserStatusAsync(userId, cancellationToken);
        if (userStatus.Features?.AssessmentAccess != true)
            return ServiceResult<AssessmentSubmissionDto>.Forbidden(error: ErrorMessagesRes.AssessmentAccessRequiresPlus);
        if (userStatus.Tier < assessment.RequiredTier)
            return ServiceResult<AssessmentSubmissionDto>.Forbidden(error: string.Format(ErrorMessagesRes.AssessmentRequiresTierFormat, assessment.RequiredTier));

        // DQ-2: reject answers that aren't well-formed JSON, reference a question id that
        // doesn't exist on this assessment, or leave a required question unanswered — instead of
        // persisting whatever was posted and only discovering the mismatch when rendering history.
        if (!AnswersMatchSchema(request.AnswersJson, assessment.SchemaJson))
            return ServiceResult<AssessmentSubmissionDto>.BadRequest(error: ErrorMessagesRes.AssessmentAnswersInvalid);

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

        return ServiceResult<AssessmentSubmissionDto>.Ok(submission.ToDto(assessment.TitleEn));
    }

    public async Task<PagedResult<AssessmentSubmissionDto>> GetMySubmissionsAsync(
        string userId, int page, int pageSize, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        var query = db.AssessmentSubmissions
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(AssessmentMappings.SubmissionProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<AssessmentSubmissionDto>(items, totalCount, page, pageSize, sortBy, sortDir);
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
            return ResolveSchemaJsonByLang(assessmentId, schemaJson, isEl);
        })!;
    }

    /// <summary>
    /// Resolves dual-language schema JSON to a flat single-language format for the student view.
    /// Handles both flat {"questions":[...]} and sectioned {"sections":[{"questions":[...]}]} schemas.
    /// Maps labelEn/labelEl or label/labelEl → label, and optionsEn/optionsEl or options/optionsEl → options.
    /// </summary>
    private string ResolveSchemaJsonByLang(Guid assessmentId, string schemaJson, bool isEl)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;

            var allQuestions = CollectQuestions(root);

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
            // DQ-4: include the assessment id — this is the only signal an operator gets that a
            // specific assessment's schema is corrupt (students still see the raw original JSON).
            logger.LogError(ex, "Failed to resolve schema JSON by language for assessment {AssessmentId}; returning original.", assessmentId);
            return schemaJson;
        }
    }

    /// Collects question elements from either a flat {"questions":[...]} or sectioned
    /// {"sections":[{"questions":[...]}]} schema.
    private static List<JsonElement> CollectQuestions(JsonElement schemaRoot)
    {
        var questions = new List<JsonElement>();

        if (schemaRoot.TryGetProperty("questions", out var flatQ) && flatQ.ValueKind == JsonValueKind.Array)
        {
            foreach (var q in flatQ.EnumerateArray())
                questions.Add(q);
        }
        else if (schemaRoot.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array)
        {
            foreach (var section in sections.EnumerateArray())
            {
                if (section.TryGetProperty("questions", out var sectionQ) && sectionQ.ValueKind == JsonValueKind.Array)
                {
                    foreach (var q in sectionQ.EnumerateArray())
                        questions.Add(q);
                }
            }
        }

        return questions;
    }

    /// <summary>
    /// DQ-2: structural validation only — confirms <paramref name="answersJson"/> is a JSON
    /// object whose keys are all real question ids on this assessment and that every
    /// <c>required</c> question has a non-blank answer. Does not check answer value/type against
    /// the question's declared type or options — those can legitimately differ between the En/El
    /// option sets a submission may have been rendered against, so per-value checks are left to
    /// client-side validation (AssessmentForm.razor.cs) to avoid false-rejecting a valid Greek
    /// (or English) submission.
    /// </summary>
    private static bool AnswersMatchSchema(string answersJson, string schemaJson)
    {
        JsonElement answersRoot;
        try
        {
            using var answersDoc = JsonDocument.Parse(answersJson);
            if (answersDoc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            answersRoot = answersDoc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return false;
        }

        // Extracted as plain strings (not JsonElements) while schemaDoc is still alive — a
        // JsonElement from CollectQuestions would become invalid once schemaDoc is disposed.
        var questionIds = new HashSet<string>();
        var requiredIds = new HashSet<string>();
        try
        {
            using var schemaDoc = JsonDocument.Parse(schemaJson);
            foreach (var q in CollectQuestions(schemaDoc.RootElement))
            {
                if (!q.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
                    continue;
                var qid = idProp.GetString()!;
                questionIds.Add(qid);
                if (q.TryGetProperty("required", out var reqProp) && reqProp.ValueKind == JsonValueKind.True)
                    requiredIds.Add(qid);
            }
        }
        catch (JsonException)
        {
            // A corrupt stored schema (DQ-4) can't be validated against — don't compound the
            // failure by also rejecting every submission for it.
            return true;
        }

        foreach (var answer in answersRoot.EnumerateObject())
        {
            if (!questionIds.Contains(answer.Name))
                return false;
        }

        foreach (var requiredId in requiredIds)
        {
            if (!answersRoot.TryGetProperty(requiredId, out var value) ||
                value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
                return false;
        }

        return true;
    }
}
