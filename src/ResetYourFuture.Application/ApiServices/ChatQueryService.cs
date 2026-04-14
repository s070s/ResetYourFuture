using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Extensions;
using ResetYourFuture.Web.Identity;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Chat history, conversations, and management queries.
/// </summary>
public class ChatQueryService(
    IApplicationDbContext db ,
    UserManager<ApplicationUser> userManager ,
    ISubscriptionService subscriptionService ,
    ILogger<ChatQueryService> logger ) : IChatQueryService
{
    public async Task<bool> HasChatAccessAsync( string userId , bool isAdmin )
    {
        if ( isAdmin )
            return true;
        var status = await subscriptionService.GetUserStatusAsync( userId );
        return status.Features?.PrioritySupport == true;
    }

    public async Task<PagedResult<ChatConversationDto>> GetConversationsAsync(
        string userId , int page , int pageSize , CancellationToken ct = default )
    {
        var baseQuery = db.ChatConversations
            .AsNoTracking()
            .Where( c => c.CreatorId == userId || c.ParticipantId == userId );

        var totalCount = await baseQuery.CountAsync( ct );

        // Q1: paginated conversations with user names — no correlated subqueries
        var rows = await baseQuery
            .OrderByDescending( c => c.LastMessageAt ?? c.CreatedAt )
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .Select( c => new
            {
                c.Id ,
                c.CreatorId ,
                c.ParticipantId ,
                c.LastMessageContent ,
                c.LastMessageAt ,
                CreatorFirstName = c.Creator!.FirstName ,
                CreatorLastName = c.Creator.LastName ,
                ParticipantFirstName = c.Participant!.FirstName ,
                ParticipantLastName = c.Participant.LastName
            } )
            .ToListAsync( ct );

        if ( rows.Count == 0 )
            return new PagedResult<ChatConversationDto>( [] , totalCount , page , pageSize );

        var convIds = rows.Select( r => r.Id ).ToList();
        var userIds = rows.SelectMany( r => new[] { r.CreatorId , r.ParticipantId } ).Distinct().ToList();

        // Q2: unread counts — single GROUP BY across all conversation IDs
        var unreadMap = await db.ChatMessages
            .Where( m => convIds.Contains( m.ConversationId ) && m.SenderId != userId && !m.IsRead )
            .GroupBy( m => m.ConversationId )
            .Select( g => new { Id = g.Key , Count = g.Count() } )
            .ToDictionaryAsync( x => x.Id , x => x.Count , ct );

        // Q3: roles — single JOIN across all involved user IDs
        var roleMap = await db.UserRoles
            .Where( ur => userIds.Contains( ur.UserId ) )
            .Join( db.Roles , ur => ur.RoleId , r => r.Id , ( ur , r ) => new { ur.UserId , r.Name } )
            .GroupBy( x => x.UserId )
            .ToDictionaryAsync( g => g.Key , g => g.First().Name! , ct );

        // In-memory projection — no further DB calls
        var result = rows.Select( c =>
        {
            var isCreator = c.CreatorId == userId;
            var otherUserId = isCreator ? c.ParticipantId : c.CreatorId;
            var otherName = isCreator
                ? $"{c.ParticipantFirstName} {c.ParticipantLastName}"
                : $"{c.CreatorFirstName} {c.CreatorLastName}";
            var otherRole = roleMap.GetValueOrDefault( otherUserId , "User" );

            return new ChatConversationDto(
                c.Id ,
                otherUserId ,
                otherName ,
                otherRole ,
                c.LastMessageContent ,
                c.LastMessageAt ,
                unreadMap.GetValueOrDefault( c.Id , 0 ) );
        } ).ToList();

        return new PagedResult<ChatConversationDto>( result , totalCount , page , pageSize );
    }

    public async Task<ServiceResult<PagedResult<ChatMessageDto>>> GetMessagesAsync(
        string userId , Guid conversationId , int page , int pageSize , CancellationToken ct = default )
    {
        var conversation = await db.ChatConversations
            .FirstOrDefaultAsync( c => c.Id == conversationId , ct );

        if ( conversation is null )
            return ServiceResult<PagedResult<ChatMessageDto>>.NotFound();

        if ( conversation.CreatorId != userId && conversation.ParticipantId != userId )
            return ServiceResult<PagedResult<ChatMessageDto>>.Forbidden();

        var query = db.ChatMessages
            .Include( m => m.Sender )
            .Where( m => m.ConversationId == conversationId );

        var totalCount = await query.CountAsync( ct );

        var messages = await query
            .OrderBy( m => m.SentAt )
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .ToListAsync( ct );

        var senderIds = messages.Select( m => m.SenderId ).Distinct().ToList();

        var roleData = await db.UserRoles
            .Where( ur => senderIds.Contains( ur.UserId ) )
            .Join( db.Roles , ur => ur.RoleId , r => r.Id , ( ur , r ) => new { ur.UserId , r.Name } )
            .ToListAsync( ct );

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

        return ServiceResult<PagedResult<ChatMessageDto>>.Ok(
            new PagedResult<ChatMessageDto>( result , totalCount , page , pageSize ) );
    }

    public async Task<ServiceResult<ChatConversationDto>> StartConversationAsync(
        string callerId , bool isAdmin , StartConversationRequest request )
    {
        if ( !await HasChatAccessAsync( callerId , isAdmin ) )
            return ServiceResult<ChatConversationDto>.Forbidden( error: "Chat requires a Pro subscription." );

        if ( string.IsNullOrWhiteSpace( request.TargetUserId ) )
            return ServiceResult<ChatConversationDto>.BadRequest( error: "TargetUserId is required." );

        if ( request.TargetUserId == callerId )
            return ServiceResult<ChatConversationDto>.BadRequest( error: "Cannot start a conversation with yourself." );

        var targetUser = await userManager.FindByIdAsync( request.TargetUserId );
        if ( targetUser is null || !targetUser.IsEnabled )
            return ServiceResult<ChatConversationDto>.NotFound( error: "User not found." );

        var (user1Id , user2Id) = NormalizePair( callerId , request.TargetUserId );

        var existing = await db.ChatConversations
            .Include( c => c.Creator )
            .Include( c => c.Participant )
            .FirstOrDefaultAsync( c => c.CreatorId == user1Id && c.ParticipantId == user2Id );

        if ( existing is not null )
        {
            if ( !string.IsNullOrWhiteSpace( request.InitialMessage ) )
            {
                var hasMessages = await db.ChatMessages
                    .AnyAsync( m => m.ConversationId == existing.Id );

                if ( !hasMessages )
                {
                    await AddInitialMessage( existing , callerId , request.InitialMessage );
                }
            }

            return ServiceResult<ChatConversationDto>.Ok( await ToDtoForUser( existing , callerId ) );
        }

        var caller = await userManager.FindByIdAsync( callerId );

        var conversation = new ChatConversation
        {
            CreatorId = user1Id ,
            ParticipantId = user2Id
        };

        db.ChatConversations.Add( conversation );
        await db.SaveChangesAsync();

        conversation.Creator = user1Id == callerId ? caller : targetUser;
        conversation.Participant = user2Id == callerId ? caller : targetUser;

        if ( !string.IsNullOrWhiteSpace( request.InitialMessage ) )
        {
            await AddInitialMessage( conversation , callerId , request.InitialMessage );
        }

        return ServiceResult<ChatConversationDto>.Ok( await ToDtoForUser( conversation , callerId ) );
    }

    public async Task<List<ChatUserDto>> GetAvailableUsersAsync( string userId , string? search )
    {
        var existingPartnerIds = await db.ChatConversations
            .Where( c => c.CreatorId == userId || c.ParticipantId == userId )
            .Select( c => c.CreatorId == userId ? c.ParticipantId : c.CreatorId )
            .ToListAsync();

        var query = userManager.Users
            .Where( u => u.Id != userId && u.IsEnabled && !existingPartnerIds.Contains( u.Id ) );

        if ( !string.IsNullOrWhiteSpace( search ) )
        {
            query = query.ApplySearch( search.Trim() );
        }

        var users = await query
            .OrderBy( u => u.FirstName )
            .ThenBy( u => u.LastName )
            .Take( 20 )
            .ToListAsync();

        var userIds = users.Select( u => u.Id ).ToList();

        var roleData = await db.UserRoles
            .Where( ur => userIds.Contains( ur.UserId ) )
            .Join( db.Roles , ur => ur.RoleId , r => r.Id , ( ur , r ) => new { ur.UserId , r.Name } )
            .ToListAsync();

        var roleMap = roleData
            .GroupBy( x => x.UserId )
            .ToDictionary( g => g.Key , g => g.Select( x => x.Name! ).FirstOrDefault() ?? "User" );

        return users.Select( u => new ChatUserDto(
            u.Id ,
            $"{u.FirstName} {u.LastName}" ,
            roleMap.TryGetValue( u.Id , out var role ) ? role : "User" ) ).ToList();
    }

    public async Task<ServiceResult<bool>> DeleteConversationAsync(
        string userId , Guid conversationId , CancellationToken ct = default )
    {
        var conversation = await db.ChatConversations
            .FirstOrDefaultAsync( c => c.Id == conversationId , ct );

        if ( conversation is null )
            return ServiceResult<bool>.NotFound();

        if ( conversation.CreatorId != userId && conversation.ParticipantId != userId )
            return ServiceResult<bool>.Forbidden();

        db.ChatConversations.Remove( conversation );
        await db.SaveChangesAsync( ct );

        return ServiceResult<bool>.Ok( true );
    }

    public async Task<int> GetUnreadCountAsync( string userId )
    {
        return await (
            from m in db.ChatMessages
            join c in db.ChatConversations on m.ConversationId equals c.Id
            where !m.IsRead && m.SenderId != userId
               && ( c.CreatorId == userId || c.ParticipantId == userId )
            select m
        ).CountAsync();
    }

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

        db.ChatMessages.Add( message );

        conversation.LastMessageContent = message.Content.Length > 500
            ? message.Content [ ..497 ] + "..."
            : message.Content;
        conversation.LastMessageAt = message.SentAt;

        await db.SaveChangesAsync();
    }

    private async Task<ChatConversationDto> ToDtoForUser( ChatConversation c , string userId )
    {
        var otherUser = c.CreatorId == userId ? c.Participant : c.Creator;
        var otherUserId = c.CreatorId == userId ? c.ParticipantId : c.CreatorId;
        var otherRole = otherUser is not null
            ? ( await userManager.GetRolesAsync( otherUser ) ).FirstOrDefault() ?? "User"
            : "User";

        var unreadCount = await db.ChatMessages
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
