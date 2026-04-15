using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.DTOs;

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
    DateTimeOffset? PublishedAt
);

/// <summary>
/// DTO for creating or updating an assessment definition.
/// </summary>
public record SaveAssessmentDefinitionRequest(
    [Required, MaxLength(100)] string Key,
    [Required, MaxLength(300)] string TitleEn,
    [MaxLength(300)] string? TitleEl,
    [MaxLength(1000)] string? DescriptionEn,
    [MaxLength(1000)] string? DescriptionEl,
    [Required] string SchemaJson
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
    DateTimeOffset? PublishedAt
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
    [Required] string AnswersJson,
    string? SummaryJson
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
    DateTimeOffset CreatedAt
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
