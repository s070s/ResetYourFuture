using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// Public category chip: language-resolved name plus the count of published items in scope.
/// </summary>
public record CategoryDto(
    Guid Id,
    string Name,
    int Count
);

/// <summary>
/// Admin category DTO with both languages and usage counts.
/// </summary>
public record AdminCategoryDto(
    Guid Id,
    string NameEn,
    string? NameEl,
    int CourseCount,
    int AssessmentCount,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Request to create or rename a category.
/// </summary>
public record SaveCategoryRequest(
    [Required, MaxLength(100)] string NameEn,
    [MaxLength(100)] string? NameEl
);

/// <summary>
/// Lightweight category option for editor dropdowns (no usage counts).
/// </summary>
public record CategoryOptionDto(
    Guid Id,
    string NameEn,
    string? NameEl
);
