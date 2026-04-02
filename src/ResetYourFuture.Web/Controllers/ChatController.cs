using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// REST endpoints for chat history, conversations, and management.
/// SignalR handles real-time; this covers load-on-demand scenarios.
/// Chat requires a Pro subscription (PrioritySupport feature).
/// </summary>
[ApiController]
[Route( "api/[controller]" )]
[Authorize]
public class ChatController( IChatQueryService chatService ) : ControllerBase
{
    private string UserId => User.FindFirstValue( ClaimTypes.NameIdentifier )!;

    /// <summary>
    /// Get conversations for the current user (server-side paginated).
    /// </summary>
    [HttpGet( "conversations" )]
    public async Task<ActionResult<PagedResult<ChatConversationDto>>> GetConversations(
        [FromQuery] int page = 1 ,
        [FromQuery] int pageSize = 10 ,
        CancellationToken cancellationToken = default )
    {
        page = Math.Max( 1 , page );
        pageSize = Math.Clamp( pageSize , 1 , 100 );

        var userId = UserId;
        if ( !await chatService.HasChatAccessAsync( userId , User.IsInRole( "Admin" ) ) )
            return StatusCode( 403 , "Chat requires a Pro subscription." );

        var result = await chatService.GetConversationsAsync( userId , page , pageSize , cancellationToken );
        return Ok( result );
    }

    /// <summary>
    /// Get messages for a conversation (server-side paginated, page 1 = oldest, last page = newest).
    /// </summary>
    [HttpGet( "conversations/{conversationId:guid}/messages" )]
    public async Task<ActionResult<PagedResult<ChatMessageDto>>> GetMessages(
        Guid conversationId ,
        [FromQuery] int page = 1 ,
        [FromQuery] int pageSize = 20 ,
        CancellationToken cancellationToken = default )
    {
        page = Math.Max( 1 , page );
        pageSize = Math.Clamp( pageSize , 1 , 100 );

        var result = await chatService.GetMessagesAsync( UserId , conversationId , page , pageSize , cancellationToken );
        if ( !result.IsSuccess )
            return StatusCode( result.StatusCode );
        return Ok( result.Value );
    }

    /// <summary>
    /// Start (or resume) a conversation with a specific user.
    /// Available to any authenticated user.
    /// </summary>
    [HttpPost( "conversations/start" )]
    public async Task<ActionResult<ChatConversationDto>> StartConversation(
        [FromBody] StartConversationRequest request )
    {
        var result = await chatService.StartConversationAsync( UserId , User.IsInRole( "Admin" ) , request );
        if ( !result.IsSuccess )
            return StatusCode( result.StatusCode , result.ErrorMessage );
        return Ok( result.Value );
    }

    /// <summary>
    /// List users available for chat (excludes current user and users they already have conversations with).
    /// </summary>
    [HttpGet( "users" )]
    public async Task<ActionResult<List<ChatUserDto>>> GetAvailableUsers( [FromQuery] string? search )
    {
        var userId = UserId;
        if ( !await chatService.HasChatAccessAsync( userId , User.IsInRole( "Admin" ) ) )
            return StatusCode( 403 , "Chat requires a Pro subscription." );

        var result = await chatService.GetAvailableUsersAsync( userId , search );
        return Ok( result );
    }

    /// <summary>
    /// Delete a conversation and all its messages. Only a participant may delete.
    /// </summary>
    [HttpDelete( "conversations/{conversationId:guid}" )]
    public async Task<IActionResult> DeleteConversation(
        Guid conversationId ,
        CancellationToken cancellationToken = default )
    {
        var result = await chatService.DeleteConversationAsync( UserId , conversationId , cancellationToken );
        if ( !result.IsSuccess )
            return StatusCode( result.StatusCode );
        return NoContent();
    }

    /// <summary>
    /// Get total unread count for the current user (for badge display).
    /// </summary>
    [HttpGet( "unread-count" )]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var count = await chatService.GetUnreadCountAsync( UserId );
        return Ok( count );
    }
}
