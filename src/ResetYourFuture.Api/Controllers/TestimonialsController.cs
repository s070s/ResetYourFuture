using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Public testimonials endpoint — no authentication required.
/// Returns active testimonials ordered by DisplayOrder for the landing page.
/// </summary>
[ApiController]
[Route( "api/testimonials" )]
public class TestimonialsController : ControllerBase
{
    private readonly ITestimonialService _testimonials;

    public TestimonialsController( ITestimonialService testimonials )
    {
        _testimonials = testimonials;
    }

    /// <summary>Returns all active testimonials ordered by DisplayOrder.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminTestimonialDto>>> GetActive(
        CancellationToken cancellationToken = default )
    {
        var result = await _testimonials.GetActiveAsync( cancellationToken );
        return Ok( result );
    }
}
