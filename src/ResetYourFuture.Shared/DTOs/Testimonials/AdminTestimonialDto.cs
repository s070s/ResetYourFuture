namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Admin read DTO — full testimonial with raw AvatarPath and audit fields.
/// </summary>
public record AdminTestimonialDto(
    Guid Id,
    string FullName,
    string? RoleOrTitle,
    string? CompanyOrContext,
    string QuoteText,
    string? AvatarPath,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
