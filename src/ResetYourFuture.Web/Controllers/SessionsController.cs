using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Extensions;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Upcoming scheduled sessions, registration, and call linkage. Authenticated (unlike the
/// anonymous catalog controllers) since registering and joining are account-scoped actions.
/// </summary>
[ApiController]
[Route("api/sessions")]
[Authorize]
[Tags("Scheduled Sessions")]
[Produces("application/json", "application/problem+json")]
public class SessionsController(IScheduledSessionService sessions) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found in claims");

    /// <summary>Upcoming (Scheduled/Live) sessions, soonest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduledSessionListItemDto>>> GetUpcoming(
        [FromQuery] string lang = "en", CancellationToken cancellationToken = default)
    {
        var result = await sessions.GetUpcomingAsync(UserId, lang, cancellationToken);
        return Ok(result);
    }

    /// <summary>Register the current user for a session.</summary>
    [HttpPost("{id:guid}/register")]
    public async Task<ActionResult<string>> Register(Guid id, CancellationToken cancellationToken = default)
        => (await sessions.RegisterAsync(id, UserId, cancellationToken)).ToActionResult(this);

    /// <summary>Unregister the current user from a session.</summary>
    [HttpPost("{id:guid}/unregister")]
    public async Task<ActionResult<string>> Unregister(Guid id, CancellationToken cancellationToken = default)
        => (await sessions.UnregisterAsync(id, UserId, cancellationToken)).ToActionResult(this);

    /// <summary>Records the CallSession a participant just started for this scheduled session.</summary>
    [HttpPost("{id:guid}/link-call")]
    public async Task<ActionResult<string>> LinkCall(
        Guid id, [FromBody] LinkCallSessionRequest request, CancellationToken cancellationToken = default)
        => (await sessions.LinkCallSessionAsync(id, UserId, request.CallSessionId, cancellationToken)).ToActionResult(this);
}
