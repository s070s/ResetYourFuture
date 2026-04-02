using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Request record for both create and update of a testimonial.
/// FullName and QuoteText are required.
/// </summary>
public record SaveTestimonialRequest(
    [Required, MaxLength(200)] string FullName,
    [MaxLength(200)] string? RoleOrTitle,
    [MaxLength(200)] string? CompanyOrContext,
    [Required, MaxLength(1000)] string QuoteText,
    int DisplayOrder,
    bool IsActive
);
