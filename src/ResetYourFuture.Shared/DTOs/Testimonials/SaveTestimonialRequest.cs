namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Request record for both create and update of a testimonial.
/// FullName and QuoteText are required.
/// </summary>
public record SaveTestimonialRequest(
    string FullName,
    string? RoleOrTitle,
    string? CompanyOrContext,
    string QuoteText,
    int DisplayOrder,
    bool IsActive
);
