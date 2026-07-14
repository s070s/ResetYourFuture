using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Web.Extensions;
using ResetYourFuture.Application.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// REST endpoints for chat history, conversations, and management.
/// SignalR handles real-time; this covers load-on-demand scenarios.
/// Available to every authenticated user.
/// </summary>
[ApiController]
[Route("api/chat")]
[Authorize]
[Tags("Chat")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class ChatController(IChatQueryService chatService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>
    /// Get conversations for the current user (server-side paginated).
    /// </summary>
    [HttpGet("conversations")]
    public async Task<ActionResult<PagedResult<ChatConversationDto>>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PagingParams.Normalize(page, pageSize);

        var result = await chatService.GetConversationsAsync(UserId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get messages for a conversation (server-side paginated, page 1 = oldest, last page = newest).
    /// </summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<PagedResult<ChatMessageDto>>> GetMessages(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PagingParams.Normalize(page, pageSize);

        var result = await chatService.GetMessagesAsync(UserId, conversationId, page, pageSize, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Start (or resume) a conversation with a specific user.
    /// Available to any authenticated user.
    /// </summary>
    [HttpPost("conversations/start")]
    public async Task<ActionResult<ChatConversationDto>> StartConversation(
        [FromBody] StartConversationRequest request)
    {
        var result = await chatService.StartConversationAsync(UserId, request);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// List users available for chat (excludes current user and users they already have conversations with).
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<List<ChatUserDto>>> GetAvailableUsers([FromQuery] string? search)
    {
        var result = await chatService.GetAvailableUsersAsync(UserId, search);
        return Ok(result);
    }

    /// <summary>
    /// Delete a conversation and all its messages. Only a participant may delete.
    /// </summary>
    [HttpDelete("conversations/{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var result = await chatService.DeleteConversationAsync(UserId, conversationId, cancellationToken);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode);
        return NoContent();
    }

    /// <summary>
    /// Get total unread count for the current user (for badge display).
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var count = await chatService.GetUnreadCountAsync(UserId);
        return Ok(count);
    }
}
