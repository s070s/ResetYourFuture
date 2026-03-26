namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Analytics summary for admin dashboard.
/// </summary>
public record AnalyticsSummaryDto(
    int TotalUsers,
    int ActiveUsers,
    int TotalEnrollments,
    int CompletedCourses,
    List<CourseStatDto> CourseStats,
    List<AssessmentStatDto> AssessmentStats
);

/// <summary>
/// Per-course enrollment and completion stats.
/// </summary>
public record CourseStatDto(
    string CourseTitle,
    int EnrollmentCount,
    int CompletionCount
);

/// <summary>
/// Per-assessment submission stats.
/// </summary>
public record AssessmentStatDto(
    string AssessmentTitle,
    int SubmissionCount
);

/// <summary>
/// Admin course DTO with full details.
/// </summary>
public record AdminCourseDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int ModuleCount,
    int TotalLessons,
    int EnrollmentCount
);

/// <summary>
/// Request to create/update a course.
/// </summary>
public record SaveCourseRequest(
    string Title,
    string? Description
);

/// <summary>
/// Admin module DTO.
/// </summary>
public record AdminModuleDto(
    Guid Id,
    string Title,
    string? Description,
    int SortOrder,
    Guid CourseId,
    int LessonCount
);

/// <summary>
/// Request to create/update a module.
/// </summary>
public record SaveModuleRequest(
    string Title,
    string? Description,
    int SortOrder,
    Guid CourseId
);

/// <summary>
/// Admin lesson DTO.
/// </summary>
public record AdminLessonDto(
    Guid Id,
    string Title,
    string? Content,
    string? PdfPath,
    string? VideoPath,
    int? DurationMinutes,
    int SortOrder,
    Guid ModuleId,
    bool IsPublished
);

/// <summary>
/// Request to create/update a lesson.
/// </summary>
public record SaveLessonRequest(
    string Title,
    string? Content,
    string? VideoUrl,
    int? DurationMinutes,
    int SortOrder,
    Guid ModuleId
);

/// <summary>
/// User management DTO for admin.
/// </summary>
public record AdminUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? DisplayName,
    bool EmailConfirmed,
    bool IsEnabled,
    string Status,
    List<string> Roles,
    DateTime CreatedAt
);

/// <summary>
/// Request to force password reset.
/// </summary>
public record ForcePasswordResetRequest(
    string UserId
);
