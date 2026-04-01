using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Identity;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Hubs;

/// <summary>
/// SignalR hub for real-time user-to-user chat.
/// All methods require authentication and a Pro subscription (PrioritySupport feature).
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        ApplicationDbContext db ,
        UserManager<ApplicationUser> userManager ,
        ISubscriptionService subscriptionService ,
        ILogger<ChatHub> logger )
    {
        _db = db;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if ( userId is not null )
        {
            // Join a personal group so we can target messages to this user.
            await Groups.AddToGroupAsync( Context.ConnectionId , $"user_{userId}" );
            _logger.LogInformation( "Chat: User {UserId} connected." , userId );
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync( Exception? exception )
    {
        var userId = Context.UserIdentifier;
        if ( userId is not null )
        {
            await Groups.RemoveFromGroupAsync( Context.ConnectionId , $"user_{userId}" );
        }

        await base.OnDisconnectedAsync( exception );
    }

    /// <summary>
    /// Send a message in an existing conversation.
    /// </summary>
    public async Task SendMessage( Guid conversationId , string content )
    {
        var userId = Context.UserIdentifier;
        if ( string.IsNullOrEmpty( userId ) || string.IsNullOrWhiteSpace( content ) )
            return;

        var isAdmin = Context.User?.IsInRole( "Admin" ) == true;
        if ( !isAdmin )
        {
            var userStatus = await _subscriptionService.GetUserStatusAsync( userId );
            if ( userStatus.Features?.PrioritySupport != true )
            {
                await Clients.Caller.SendAsync( "ChatError" , "Chat requires a Pro subscription." );
                return;
            }
        }

        var conversation = await _db.ChatConversations
            .FirstOrDefaultAsync( c => c.Id == conversationId );

        if ( conversation is null )
            return;

        // Verify the caller is a participant.
        if ( conversation.CreatorId != userId && conversation.ParticipantId != userId )
            return;

        var sender = await _userManager.FindByIdAsync( userId );
        if ( sender is null )
            return;

        var roles = await _userManager.GetRolesAsync( sender );
        var senderRole = roles.FirstOrDefault() ?? "User";

        var message = new ChatMessage
        {
            ConversationId = conversationId ,
            SenderId = userId ,
            Content = content.Trim() ,
            SentAt = DateTime.UtcNow
        };

        _db.ChatMessages.Add( message );

        // Update conversation's last-message cache.
        conversation.LastMessageContent = message.Content.Length > 500
            ? message.Content [ ..497 ] + "..."
            : message.Content;
        conversation.LastMessageAt = message.SentAt;

        await _db.SaveChangesAsync();

        var dto = new ChatMessageDto(
            message.Id ,
            message.ConversationId ,
            userId ,
            $"{sender.FirstName} {sender.LastName}" ,
            senderRole ,
            message.Content ,
            message.SentAt ,
            false );

        // Send to both participants.
        var recipientId = conversation.CreatorId == userId
            ? conversation.ParticipantId
            : conversation.CreatorId;

        await Clients.Group( $"user_{userId}" ).SendAsync( "ReceiveMessage" , dto );
        await Clients.Group( $"user_{recipientId}" ).SendAsync( "ReceiveMessage" , dto );

        // Send notification to recipient.
        var notification = new ChatNotificationDto(
            conversationId ,
            $"{sender.FirstName} {sender.LastName}" ,
            message.Content.Length > 80 ? message.Content [ ..77 ] + "..." : message.Content ,
            message.SentAt );

        await Clients.Group( $"user_{recipientId}" ).SendAsync( "ChatNotification" , notification );
    }

    /// <summary>
    /// Mark all messages in a conversation as read for the current user.
    /// </summary>
    public async Task MarkAsRead( Guid conversationId )
    {
        var userId = Context.UserIdentifier;
        if ( string.IsNullOrEmpty( userId ) )
            return;

        await _db.ChatMessages
            .Where( m => m.ConversationId == conversationId
                      && m.SenderId != userId
                      && !m.IsRead )
            .ExecuteUpdateAsync( s => s.SetProperty( m => m.IsRead , true ) );
    }
}
