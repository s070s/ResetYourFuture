using System.ComponentModel.DataAnnotations;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Shared.Resources.Messages;

namespace ResetYourFuture.Application.DTOs;
/// <summary>
/// User profile information.
/// </summary>
public record ProfileDto(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? DisplayName,
    string? AvatarPath,
    DateOnly? DateOfBirth,
    UserStatus Status
);

/// <summary>
/// Request to update user profile.
/// </summary>
public record UpdateProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [MaxLength(100)] string? DisplayName,
    DateOnly? DateOfBirth,
    UserStatus Status
);

/// <summary>
/// Request to change password.
/// </summary>
public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_PasswordRequired))]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,128}$", ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_PasswordComplexity))]
    string NewPassword
);

/// <summary>
/// COMP-4: full personal-data export for the "download my data" GDPR access/portability
/// endpoint. Chat messages are limited to ones the user themselves sent — the other party's
/// messages in a shared conversation are their own personal data, not exportable through this
/// user's request.
/// </summary>
public record MyDataExportDto(
    ProfileExportDto Profile,
    List<EnrollmentExportDto> Enrollments,
    List<AssessmentSubmissionExportDto> AssessmentSubmissions,
    List<CertificateExportDto> Certificates,
    List<BillingTransactionExportDto> BillingTransactions,
    List<ChatMessageExportDto> ChatMessages
);

public record ProfileExportDto(
    string Email,
    string FirstName,
    string LastName,
    string? DisplayName,
    DateOnly? DateOfBirth,
    string Status,
    DateTime AccountCreatedAt,
    bool GdprConsentGiven,
    DateTime? GdprConsentDate,
    bool ParentalConsentGiven
);

public record EnrollmentExportDto(
    string CourseTitle,
    DateTime EnrolledAt,
    string Status,
    DateTime? CompletedAt
);

public record AssessmentSubmissionExportDto(
    string AssessmentTitle,
    string AnswersJson,
    string? SummaryJson,
    DateTimeOffset SubmittedAt
);

public record CertificateExportDto(
    string CourseTitle,
    Guid VerificationId,
    DateTime IssuedAt,
    string Status
);

public record BillingTransactionExportDto(
    string PlanName,
    decimal Amount,
    string Currency,
    string Type,
    string Description,
    DateTime CreatedAt
);

public record ChatMessageExportDto(
    Guid ConversationId,
    string Content,
    DateTime SentAt
);
