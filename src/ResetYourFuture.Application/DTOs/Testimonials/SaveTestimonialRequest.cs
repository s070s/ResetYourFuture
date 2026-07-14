using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// Request record for both create and update of a testimonial.
/// FullName and QuoteText are required.
/// </summary>
public record SaveTestimonialRequest(
    // DQ-3: capped at 150 to match TestimonialConfiguration's column length — a longer value
    // would pass this check and then throw a SQL truncation error on SaveChanges.
    [Required, MaxLength(150)] string FullName,
    [MaxLength(150)] string? RoleOrTitle,
    [MaxLength(150)] string? CompanyOrContext,
    [Required, MaxLength(1000)] string QuoteText,
    int DisplayOrder,
    bool IsActive
);
