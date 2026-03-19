using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Api.Identity;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// REST endpoints for chat history, conversations, and management.
/// SignalR handles real-time; this covers load-on-demand scenarios.
/// Any authenticated user can chat with any other user.
/// </summary>
[ApiController]
[Route( "api/[controller]" )]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ApplicationDbContext db ,
        UserManager<ApplicationUser> userManager ,
        ILogger<ChatController> logger )
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

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

        var query = _db.ChatConversations
            .Include( c => c.Creator )
            .Include( c => c.Participant )
            .Where( c => c.CreatorId == userId || c.ParticipantId == userId )
            .OrderByDescending( c => c.LastMessageAt ?? c.CreatedAt );

        var totalCount = await query.CountAsync( cancellationToken );

        var conversations = await query
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .ToListAsync( cancellationToken );

        var conversationIds = conversations.Select( c => c.Id ).ToList();

        var unreadCounts = await _db.ChatMessages
            .Where( m => conversationIds.Contains( m.ConversationId ) && m.SenderId != userId && !m.IsRead )
            .GroupBy( m => m.ConversationId )
            .Select( g => new { ConversationId = g.Key , Count = g.Count() } )
            .ToDictionaryAsync( x => x.ConversationId , x => x.Count , cancellationToken );

        var otherUserIds = conversations
            .Select( c => c.CreatorId == userId ? c.ParticipantId : c.CreatorId )
            .Distinct()
            .ToList();

        var roleData = await _db.UserRoles
            .Where( ur => otherUserIds.Contains( ur.UserId ) )
            .Join( _db.Roles , ur => ur.RoleId , r => r.Id , ( ur , r ) => new { ur.UserId , r.Name } )
            .ToListAsync( cancellationToken );

        var roleMap = roleData
            .GroupBy( x => x.UserId )
            .ToDictionary( g => g.Key , g => g.Select( x => x.Name! ).FirstOrDefault() ?? "User" );

        var result = conversations.Select( c =>
        {
            var otherUserId = c.CreatorId == userId ? c.ParticipantId : c.CreatorId;
            var otherUser = c.CreatorId == userId ? c.Participant : c.Creator;
            var otherRole = roleMap.TryGetValue( otherUserId , out var role ) ? role : "User";
            var unreadCount = unreadCounts.TryGetValue( c.Id , out var count ) ? count : 0;

            return new ChatConversationDto(
                c.Id ,
                otherUserId ,
                $"{otherUser?.FirstName} {otherUser?.LastName}" ,
                otherRole ,
                c.LastMessageContent ,
                c.LastMessageAt ,
                unreadCount );
        } ).ToList();

        return Ok( new PagedResult<ChatConversationDto>( result , totalCount , page , pageSize ) );
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

        var userId = UserId;

        var conversation = await _db.ChatConversations
            .FirstOrDefaultAsync( c => c.Id == conversationId , cancellationToken );

        if ( conversation is null )
            return NotFound();

        if ( conversation.CreatorId != userId && conversation.ParticipantId != userId )
            return Forbid();

        var query = _db.ChatMessages
            .Include( m => m.Sender )
            .Where( m => m.ConversationId == conversationId );

        var totalCount = await query.CountAsync( cancellationToken );

        var messages = await query
            .OrderBy( m => m.SentAt )
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .ToListAsync( cancellationToken );

        var senderIds = messages.Select( m => m.SenderId ).Distinct().ToList();

        var roleData = await _db.UserRoles
            .Where( ur => senderIds.Contains( ur.UserId ) )
            .Join( _db.Roles , ur => ur.RoleId , r => r.Id , ( ur , r ) => new { ur.UserId , r.Name } )
            .ToListAsync( cancellationToken );

        var roleMap = roleData
            .GroupBy( x => x.UserId )
            .ToDictionary( g => g.Key , g => g.Select( x => x.Name! ).FirstOrDefault() ?? "User" );

        var result = messages.Select( m => new ChatMessageDto(
            m.Id ,
            m.ConversationId ,
            m.SenderId ,
            $"{m.Sender?.FirstName} {m.Sender?.LastName}" ,
            roleMap.TryGetValue( m.SenderId , out var role ) ? role : "User" ,
            m.Content ,
            m.SentAt ,
            m.IsRead ) ).ToList();

        return Ok( new PagedResult<ChatMessageDto>( result , totalCount , page , pageSize ) );
    }

    /// <summary>
    /// Start (or resume) a conversation with a specific user.
    /// Available to any authenticated user.
    /// </summary>
    [HttpPost( "conversations/start" )]
    public async Task<ActionResult<ChatConversationDto>> StartConversation(
        [FromBody] StartConversationRequest request )
    {
        var callerId = UserId;

        if ( string.IsNullOrWhiteSpace( request.TargetUserId ) )
            return BadRequest( "TargetUserId is required." );

        if ( request.TargetUserId == callerId )
            return BadRequest( "Cannot start a conversation with yourself." );

        var targetUser = await _userManager.FindByIdAsync( request.TargetUserId );
        if ( targetUser is null || !targetUser.IsEnabled )
            return NotFound( "User not found." );

        // Normalize the pair so (A,B) and (B,A) always map to the same row.
        var (user1Id , user2Id) = NormalizePair( callerId , request.TargetUserId );

        // Check for existing conversation between these two users.
        var existing = await _db.ChatConversations
            .Include( c => c.Creator )
            .Include( c => c.Participant )
            .FirstOrDefaultAsync( c => c.CreatorId == user1Id && c.ParticipantId == user2Id );

        if ( existing is not null )
        {
            if ( !string.IsNullOrWhiteSpace( request.InitialMessage ) )
            {
                var hasMessages = await _db.ChatMessages
                    .AnyAsync( m => m.ConversationId == existing.Id );

                if ( !hasMessages )
                {
                    await AddInitialMessage( existing , callerId , request.InitialMessage );
                }
            }

            return Ok( await ToDtoForUser( existing , callerId ) );
        }

        var caller = await _userManager.FindByIdAsync( callerId );

        var conversation = new ChatConversation
        {
            CreatorId = user1Id ,
            ParticipantId = user2Id
        };

        _db.ChatConversations.Add( conversation );
        await _db.SaveChangesAsync();

        // Load navigation properties.
        conversation.Creator = user1Id == callerId ? caller : targetUser;
        conversation.Participant = user2Id == callerId ? caller : targetUser;

        if ( !string.IsNullOrWhiteSpace( request.InitialMessage ) )
        {
            await AddInitialMessage( conversation , callerId , request.InitialMessage );
        }

        return Ok( await ToDtoForUser( conversation , callerId ) );
    }

    /// <summary>
    /// List users available for chat (excludes current user and users they already have conversations with).
    /// </summary>
    [HttpGet( "users" )]
    public async Task<ActionResult<List<ChatUserDto>>> GetAvailableUsers( [FromQuery] string? search )
    {
        var userId = UserId;

        // Get IDs of users the caller already has conversations with.
        var existingPartnerIds = await _db.ChatConversations
            .Where( c => c.CreatorId == userId || c.ParticipantId == userId )
            .Select( c => c.CreatorId == userId ? c.ParticipantId : c.CreatorId )
            .ToListAsync();

        var query = _userManager.Users
            .Where( u => u.Id != userId && u.IsEnabled && !existingPartnerIds.Contains( u.Id ) );

        if ( !string.IsNullOrWhiteSpace( search ) )
        {
            var term = search.Trim().ToLower();
            query = query.Where( u =>
                u.FirstName.ToLower().Contains( term ) ||
                u.LastName.ToLower().Contains( term ) ||
                ( u.Email != null && u.Email.ToLower().Contains( term ) ) );
        }

        var users = await query
            .OrderBy( u => u.FirstName )
            .ThenBy( u => u.LastName )
            .Take( 20 )
            .ToListAsync();

        var userIds = users.Select( u => u.Id ).ToList();

        var roleData = await _db.UserRoles
            .Where( ur => userIds.Contains( ur.UserId ) )
            .Join( _db.Roles , ur => ur.RoleId , r => r.Id , ( ur , r ) => new { ur.UserId , r.Name } )
            .ToListAsync();

        var roleMap = roleData
            .GroupBy( x => x.UserId )
            .ToDictionary( g => g.Key , g => g.Select( x => x.Name! ).FirstOrDefault() ?? "User" );

        var result = users.Select( u => new ChatUserDto(
            u.Id ,
            $"{u.FirstName} {u.LastName}" ,
            roleMap.TryGetValue( u.Id , out var role ) ? role : "User" ) ).ToList();

        return Ok( result );
    }

    /// <summary>
    /// Get total unread count for the current user (for badge display).
    /// </summary>
    [HttpGet( "unread-count" )]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var userId = UserId;

        var count = await (
            from m in _db.ChatMessages
            join c in _db.ChatConversations on m.ConversationId equals c.Id
            where !m.IsRead && m.SenderId != userId
               && ( c.CreatorId == userId || c.ParticipantId == userId )
            select m
        ).CountAsync();

        return Ok( count );
    }

    /// <summary>
    /// Normalizes a user pair so the lexicographically smaller ID is always first.
    /// This ensures the unique index works regardless of who initiates.
    /// </summary>
    private static (string User1Id , string User2Id) NormalizePair( string a , string b ) =>
        string.CompareOrdinal( a , b ) < 0 ? (a , b) : (b , a);

    private async Task AddInitialMessage( ChatConversation conversation , string senderId , string content )
    {
        var message = new ChatMessage
        {
            ConversationId = conversation.Id ,
            SenderId = senderId ,
            Content = content.Trim() ,
            SentAt = DateTime.UtcNow
        };

        _db.ChatMessages.Add( message );

        conversation.LastMessageContent = message.Content.Length > 500
            ? message.Content [ ..497 ] + "..."
            : message.Content;
        conversation.LastMessageAt = message.SentAt;

        await _db.SaveChangesAsync();
    }

    private async Task<ChatConversationDto> ToDtoForUser( ChatConversation c , string userId )
    {
        var otherUser = c.CreatorId == userId ? c.Participant : c.Creator;
        var otherUserId = c.CreatorId == userId ? c.ParticipantId : c.CreatorId;
        var otherRole = otherUser is not null
            ? ( await _userManager.GetRolesAsync( otherUser ) ).FirstOrDefault() ?? "User"
            : "User";

        var unreadCount = await _db.ChatMessages
            .CountAsync( m => m.ConversationId == c.Id
                           && m.SenderId != userId
                           && !m.IsRead );

        return new ChatConversationDto(
            c.Id ,
            otherUserId ,
            $"{otherUser?.FirstName} {otherUser?.LastName}" ,
            otherRole ,
            c.LastMessageContent ,
            c.LastMessageAt ,
            unreadCount );
    }
}
