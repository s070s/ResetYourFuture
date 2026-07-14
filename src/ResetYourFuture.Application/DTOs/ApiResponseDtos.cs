namespace ResetYourFuture.Application.DTOs;

// Typed response DTOs replacing anonymous/object return shapes so the OpenAPI schema is
// accurate. Property names are chosen so System.Text.Json's camelCase output is identical
// to the previous anonymous objects (e.g. CoverImageUrl -> "coverImageUrl"), preserving the
// existing JSON contract for current clients.

/// <summary>Authenticated user's own identity summary returned by <c>GET /api/auth/me</c>.</summary>
public record CurrentUserDto(
    string Id,
    string? Email,
    string FirstName,
    string LastName,
    int? Age,
    string Status,
    IReadOnlyList<string> Roles);

/// <summary>Detailed user record returned by <c>GET /api/admin/users/{userId}</c>.</summary>
public record AdminUserDetailDto(
    string Id,
    string? Email,
    string FirstName,
    string LastName,
    int? Age,
    string Status,
    bool EmailConfirmed,
    bool IsEnabled,
    bool GdprConsentGiven,
    DateTime? GdprConsentDate,
    bool ParentalConsentGiven,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles);

/// <summary>Lightweight user match returned by <c>GET /api/admin/users/search</c>.</summary>
public record AdminUserSearchResultDto(
    string Id,
    string? Email,
    string FirstName,
    string LastName,
    string? DisplayName,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles);

/// <summary>Result of uploading a blog article cover image.</summary>
public record BlogCoverUploadResultDto(string CoverImageUrl);

/// <summary>Result of uploading an avatar image (profile or testimonial).</summary>
public record AvatarUploadResultDto(string AvatarPath);

/// <summary>Result of uploading the landing-page background image.</summary>
public record BackgroundImageUploadResultDto(string BackgroundImagePath);

/// <summary>Acknowledgement returned to Stripe after a webhook is received.</summary>
public record StripeWebhookAckDto(bool Received);

/// <summary>Result of uploading a lesson PDF asset.</summary>
public record LessonPdfUploadResultDto(string PdfPath);

/// <summary>Result of uploading a lesson video asset.</summary>
public record LessonVideoUploadResultDto(string VideoPath);
