namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// Response for enrollment and completion operations.
/// </summary>
public record EnrollmentResultDto(
    bool Success,
    string? Message,
    Guid? EnrollmentId
);

/// <summary>
/// Response for lesson completion. <c>CertificatePending</c> is true when the course just
/// completed and certificate auto-generation (REL-5) failed — the completion itself still
/// succeeded, but the UI should tell the user their certificate is pending rather than staying
/// silent; <c>IssueCertificate</c> retries generation on demand.
/// </summary>
public record LessonCompletionResultDto(
    bool Success,
    string? Message,
    int CompletedLessons,
    int TotalLessons,
    double ProgressPercent,
    bool CourseCompleted,
    bool CertificatePending = false
);
