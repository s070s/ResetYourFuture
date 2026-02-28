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
    string Key,
    string Title,
    string? Description,
    string SchemaJson
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
    string AnswersJson,
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
