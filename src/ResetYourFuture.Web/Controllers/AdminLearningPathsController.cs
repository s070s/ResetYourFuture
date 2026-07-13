using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Extensions;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Admin CRUD for learning paths, including ordered step management.
/// </summary>
[ApiController]
[Route("api/admin/paths")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin · Learning Paths")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class AdminLearningPathsController(ILearningPathService paths) : ControllerBase
{
    /// <summary>Get a paged list of all learning paths (published and unpublished).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminLearningPathDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "displayorder",
        [FromQuery] string sortDir = "asc",
        CancellationToken cancellationToken = default)
    {
        var result = await paths.GetAllForAdminAsync(page, pageSize, sortBy, sortDir, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get a single learning path (with its steps) by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminLearningPathDetailDto>> GetById(
        Guid id, CancellationToken cancellationToken = default)
    {
        var item = await paths.GetAdminByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Create a new learning path (unpublished, no steps).</summary>
    [HttpPost]
    public async Task<ActionResult<AdminLearningPathDetailDto>> Create(
        [FromBody] SaveLearningPathRequest request, CancellationToken cancellationToken = default)
    {
        var result = await paths.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update a learning path's details.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminLearningPathDetailDto>> Update(
        Guid id, [FromBody] SaveLearningPathRequest request, CancellationToken cancellationToken = default)
    {
        var result = await paths.UpdateAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Publish a learning path so it appears in the public catalog.</summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken = default)
        => await paths.PublishAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Unpublish a learning path, hiding it from the public catalog.</summary>
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken cancellationToken = default)
        => await paths.UnpublishAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Delete a learning path and its steps.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
        => await paths.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Append a course as the next step in the path.</summary>
    [HttpPost("{id:guid}/steps")]
    public async Task<ActionResult<AdminLearningPathDetailDto>> AddStep(
        Guid id, [FromBody] AddLearningPathStepRequest request, CancellationToken cancellationToken = default)
    {
        var result = await paths.AddStepAsync(id, request.CourseId, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>Remove a step; remaining steps are re-sequenced to stay contiguous.</summary>
    [HttpDelete("{id:guid}/steps/{stepId:guid}")]
    public async Task<IActionResult> RemoveStep(
        Guid id, Guid stepId, CancellationToken cancellationToken = default)
        => await paths.RemoveStepAsync(id, stepId, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Move a step one position earlier.</summary>
    [HttpPost("{id:guid}/steps/{stepId:guid}/move-up")]
    public async Task<IActionResult> MoveStepUp(
        Guid id, Guid stepId, CancellationToken cancellationToken = default)
        => await paths.MoveStepUpAsync(id, stepId, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Move a step one position later.</summary>
    [HttpPost("{id:guid}/steps/{stepId:guid}/move-down")]
    public async Task<IActionResult> MoveStepDown(
        Guid id, Guid stepId, CancellationToken cancellationToken = default)
        => await paths.MoveStepDownAsync(id, stepId, cancellationToken) ? NoContent() : NotFound();
}
