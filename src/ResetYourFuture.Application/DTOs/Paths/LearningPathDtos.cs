namespace ResetYourFuture.Application.DTOs;

/// <summary>Catalog list entry — <see cref="CompletedSteps"/> is 0 for anonymous visitors.</summary>
public record LearningPathListItemDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? CategoryId,
    string? CategoryName,
    int StepCount,
    int CompletedSteps);

/// <summary>One course within a path, with per-user progression state.</summary>
public record LearningPathStepDto(
    Guid CourseId,
    string CourseTitle,
    int StepOrder,
    bool IsCompleted,
    bool IsLocked,
    bool IsNext);

public record LearningPathDetailDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? CategoryId,
    string? CategoryName,
    IReadOnlyList<LearningPathStepDto> Steps);

// --- Admin ---

public record AdminLearningPathDto(
    Guid Id,
    string TitleEn,
    string? TitleEl,
    Guid? CategoryId,
    string? CategoryName,
    int DisplayOrder,
    bool IsPublished,
    int StepCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record AdminLearningPathStepDto(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    int StepOrder);

public record AdminLearningPathDetailDto(
    Guid Id,
    string TitleEn,
    string? TitleEl,
    string? DescriptionEn,
    string? DescriptionEl,
    Guid? CategoryId,
    string? CategoryName,
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyList<AdminLearningPathStepDto> Steps);

public record SaveLearningPathRequest(
    string TitleEn,
    string? TitleEl,
    string? DescriptionEn,
    string? DescriptionEl,
    Guid? CategoryId,
    int DisplayOrder);

public record AddLearningPathStepRequest(Guid CourseId);
