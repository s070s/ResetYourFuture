using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Site-wide search over published content (courses, assessments, blog articles). Anonymous —
/// like <see cref="BlogController"/>/<see cref="TestimonialsController"/> — since it's a
/// discovery surface for public content, not account data.
/// </summary>
[ApiController]
[Route("api/search")]
[Tags("Search")]
[Produces("application/json")]
public class SearchController(ISiteSearchService search) : ControllerBase
{
    private const int MaxLimit = 20;

    /// <summary>Search published courses, assessments, and blog articles by keyword.</summary>
    [HttpGet]
    public async Task<ActionResult<SiteSearchResultDto>> Search(
        [FromQuery] string q = "", [FromQuery] int limit = 8, [FromQuery] string lang = "en",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new SiteSearchResultDto([], SemanticSearchUsed: false));

        limit = Math.Clamp(limit, 1, MaxLimit);

        var result = await search.SearchAsync(q, lang, limit, cancellationToken);
        return Ok(result);
    }
}
