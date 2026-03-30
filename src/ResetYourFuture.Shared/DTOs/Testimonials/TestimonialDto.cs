namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Public display DTO for a testimonial shown on the landing page.
/// AvatarUrl is a fully-qualified URL ready to use as an img src, or null if no avatar is set.
/// </summary>
public record TestimonialDto(
    Guid Id,
    string FullName,
    string? RoleOrTitle,
    string? CompanyOrContext,
    string QuoteText,
    string? AvatarUrl,
    int DisplayOrder
);
