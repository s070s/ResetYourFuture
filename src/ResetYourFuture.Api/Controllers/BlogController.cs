using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Public blog endpoints — no authentication required.
/// </summary>
[ApiController]
[Route( "api/blog" )]
public class BlogController : ControllerBase
{
    private readonly IBlogArticleService _blog;

    public BlogController( IBlogArticleService blog )
    {
        _blog = blog;
    }

    /// <summary>Returns the latest published article summaries for the landing page.</summary>
    [HttpGet( "summaries" )]
    public async Task<ActionResult<IReadOnlyList<BlogArticleSummaryDto>>> GetSummaries(
        [FromQuery] int count = 6,
        [FromQuery] string lang = "en",
        CancellationToken cancellationToken = default )
    {
        count = Math.Clamp( count, 1, 20 );
        var result = await _blog.GetPublishedSummariesAsync( count, lang, cancellationToken );
        return Ok( result );
    }

    /// <summary>Returns a single published article by slug.</summary>
    [HttpGet( "{slug}" )]
    public async Task<ActionResult<BlogArticleDto>> GetBySlug(
        string slug,
        [FromQuery] string lang = "en",
        CancellationToken cancellationToken = default )
    {
        var article = await _blog.GetPublishedBySlugAsync( slug, lang, cancellationToken );
        return article is null ? NotFound() : Ok( article );
    }
}
