using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// DTO for assessment definition (template).
/// </summary>
public record AssessmentDefinitionDto(
    Guid Id,
    string Key,
    string Title,
    string? Description,
    string SchemaJson,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt,
    Guid? CategoryId = null,
    string? CategoryName = null
);

/// <summary>
/// DTO for creating or updating an assessment definition. When <paramref name="NewCategoryName"/> is
/// non-blank it wins over <paramref name="CategoryId"/> and a category is get-or-created by
/// case-insensitive name match.
/// </summary>
public record SaveAssessmentDefinitionRequest(
    [Required, MaxLength(100)] string Key,
    [Required, MaxLength(300)] string TitleEn,
    [MaxLength(300)] string? TitleEl,
    [MaxLength(1000)] string? DescriptionEn,
    [MaxLength(1000)] string? DescriptionEl,
    [Required] string SchemaJson,
    Guid? CategoryId = null,
    [MaxLength(100)] string? NewCategoryName = null
);

/// <summary>
/// Admin DTO for assessment definition with dual-language fields.
/// </summary>
public record AdminAssessmentDefinitionDto(
    Guid Id,
    string Key,
    string TitleEn,
    string? TitleEl,
    string? DescriptionEn,
    string? DescriptionEl,
    string SchemaJson,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt,
    Guid? CategoryId = null,
    string? CategoryNameEn = null
);

/// <summary>
/// DTO for assessment submission by a student.
/// </summary>
public record AssessmentSubmissionDto(
    Guid Id,
    Guid AssessmentDefinitionId,
    string AssessmentTitle,
    string AnswersJson,
    string? SummaryJson,
    DateTimeOffset SubmittedAt
);

/// <summary>
/// Request to submit assessment answers.
/// </summary>
public record SubmitAssessmentRequest(
    [Required, MaxLength(50_000)] string AnswersJson,
    [MaxLength(20_000)] string? SummaryJson
);

/// <summary>
/// Assessment definition list item for admin view.
/// </summary>
public record AssessmentDefinitionListItemDto(
    Guid Id,
    string Key,
    string Title,
    bool IsPublished,
    int SubmissionCount,
    DateTimeOffset CreatedAt,
    string? CategoryNameEn = null
);

/// <summary>
/// Assessment submission list item for admin view.
/// </summary>
public record AssessmentSubmissionListItemDto(
    Guid Id,
    string UserId,
    string UserEmail,
    string UserDisplayName,
    string AnswersJson,
    string? SummaryJson,
    DateTimeOffset SubmittedAt
);
