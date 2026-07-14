using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>Admin CRUD for scheduled sessions. The creating admin becomes the session host.</summary>
[ApiController]
[Route("api/admin/sessions")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin · Scheduled Sessions")]
[Produces("application/json", "application/problem+json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class AdminSessionsController(IScheduledSessionService sessions) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found in claims");

    /// <summary>Get a paged list of all scheduled sessions.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminScheduledSessionDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "startsatutc",
        [FromQuery] string sortDir = "asc",
        CancellationToken cancellationToken = default)
    {
        var result = await sessions.GetAllForAdminAsync(page, pageSize, sortBy, sortDir, cancellationToken);
        return Ok(result);
    }

    /// <summary>Create a new scheduled session, hosted by the calling admin.</summary>
    [HttpPost]
    public async Task<ActionResult<AdminScheduledSessionDto>> Create(
        [FromBody] SaveScheduledSessionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await sessions.CreateAsync(UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, result);
    }

    /// <summary>Update a scheduled session's details.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminScheduledSessionDto>> Update(
        Guid id, [FromBody] SaveScheduledSessionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await sessions.UpdateAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Cancel a scheduled session.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken = default)
        => await sessions.CancelAsync(id, cancellationToken) ? NoContent() : NotFound();
}
