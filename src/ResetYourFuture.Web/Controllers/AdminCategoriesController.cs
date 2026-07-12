using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Extensions;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Admin endpoints for managing categories shared by courses and assessments.
/// </summary>
[ApiController]
[Route("api/admin/categories")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin · Categories")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class AdminCategoriesController(IAdminCategoryService adminCategoryService) : ControllerBase
{
    /// <summary>
    /// Get all categories with usage counts, server-side paginated.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminCategoryDto>>> GetCategories(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "nameen",
        [FromQuery] string sortDir = "asc",
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await adminCategoryService.GetCategoriesAsync(page, pageSize, sortBy, sortDir, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get all categories without pagination, for course/assessment editor dropdowns.
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<List<CategoryOptionDto>>> GetAllCategories(CancellationToken cancellationToken = default)
    {
        var result = await adminCategoryService.GetAllCategoriesAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new category.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<AdminCategoryDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminCategoryDto>> CreateCategory([FromBody] SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var result = await adminCategoryService.CreateCategoryAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Rename an existing category.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminCategoryDto>> UpdateCategory(Guid id, [FromBody] SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var result = await adminCategoryService.UpdateCategoryAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Delete a category. Courses/assessments referencing it become uncategorized rather than hidden.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken = default)
    {
        return await adminCategoryService.DeleteCategoryAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
