using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// REST endpoints for the notification inbox. SignalR (<c>NotificationHub</c>) handles live
/// push; this covers the paged history, unread count, and mark-read actions. Available to
/// every authenticated user.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
[Tags("Notifications")]
[Produces("application/json")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Get a paged, sortable list of the current user's notifications.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "createdat",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await notifications.GetPagedAsync(UserId, page, pageSize, sortBy, sortDir, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get the current user's unread notification count, for the bell badge.</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<NotificationSummaryDto>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var count = await notifications.GetUnreadCountAsync(UserId, cancellationToken);
        return Ok(new NotificationSummaryDto(count));
    }

    /// <summary>Mark one notification read.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken = default)
    {
        var found = await notifications.MarkReadAsync(id, UserId, cancellationToken);
        return found ? NoContent() : NotFound();
    }

    /// <summary>Mark every one of the current user's notifications read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken = default)
    {
        await notifications.MarkAllReadAsync(UserId, cancellationToken);
        return NoContent();
    }
}
