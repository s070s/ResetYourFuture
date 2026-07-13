using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Public learning-path catalog. Anonymous — like <see cref="BlogController"/> — but reads the
/// caller's identity when present so progress (locked/next/done) can be projected per user.
/// </summary>
[ApiController]
[Route("api/paths")]
[Tags("Learning Paths")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class PathsController(ILearningPathService paths) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>Published learning paths for the catalog page.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LearningPathListItemDto>>> GetPaths(
        [FromQuery] string lang = "en", CancellationToken cancellationToken = default)
    {
        var result = await paths.GetPublishedAsync(UserId, lang, cancellationToken);
        return Ok(result);
    }

    /// <summary>A published learning path with its ordered steps and (for signed-in users) progress.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LearningPathDetailDto>> GetPath(
        Guid id, [FromQuery] string lang = "en", CancellationToken cancellationToken = default)
    {
        var result = await paths.GetByIdAsync(id, UserId, lang, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
