using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Category discovery for browse/filter chips (requires authentication).
/// </summary>
[ApiController]
[Route("api/categories")]
[Authorize]
[Tags("Categories")]
[Produces("application/json", "application/problem+json")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    /// <summary>
    /// Get categories that have at least one published item in the given scope, with counts.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories(
        [FromQuery] string scope = "courses",
        [FromQuery] string lang = "en",
        CancellationToken cancellationToken = default)
    {
        var result = await categoryService.GetCategoriesAsync(scope, lang, cancellationToken);
        return Ok(result);
    }
}
