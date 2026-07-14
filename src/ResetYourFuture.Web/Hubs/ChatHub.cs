using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Services;

namespace ResetYourFuture.Web.Hubs;

/// <summary>
/// SignalR hub for real-time user-to-user chat: connection/group management and broadcasting
/// only. Validation and persistence live in <see cref="IChatCommandService"/> (Application).
/// Available to every authenticated, enabled user.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IChatCommandService _chatCommandService;
    private readonly INotificationDispatcher _notifications;
    private readonly NotificationConnectionTracker _notificationTracker;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        UserManager<ApplicationUser> userManager,
        IChatCommandService chatCommandService,
        INotificationDispatcher notifications,
        NotificationConnectionTracker notificationTracker,
        ILogger<ChatHub> logger)
    {
        _userManager = userManager;
        _chatCommandService = chatCommandService;
        _notifications = notifications;
        _notificationTracker = notificationTracker;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || !user.IsEnabled)
            {
                _logger.LogWarning("Chat: Rejected connection for disabled/unknown user {UserId}.", userId);
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("Chat: User {UserId} connected.", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Send a message in an existing conversation.
    /// </summary>
    public async Task SendMessage(Guid conversationId, string content)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || string.IsNullOrWhiteSpace(content))
            return;

        // Build the sender's display name and role from the connection's claims (minted at
        // sign-in) instead of re-querying the identity store per message (PERF-7).
        var user = Context.User;
        var senderName = $"{user?.FindFirst("firstName")?.Value} {user?.FindFirst("lastName")?.Value}".Trim();
        var senderRole = user?.FindFirst(ClaimTypes.Role)?.Value ?? "User";

        var result = await _chatCommandService.SendMessageAsync(userId, senderName, senderRole, conversationId, content);
        if (!result.IsSuccess)
        {
            if (result.StatusCode == 400)
            {
                // Hard cap: 4 000 chars max per message. Prevents storage abuse and abnormally large payloads.
                await Clients.Caller.SendAsync("ChatError", result.ErrorMessage ?? "Message exceeds the 4,000 character limit.");
            }
            // NotFound/Forbidden (unknown conversation or caller isn't a participant): silent no-op.
            return;
        }

        var (dto, recipientId) = result.Value!;

        // Send to both participants.
        await Clients.Group($"user_{userId}").SendAsync("ReceiveMessage", dto);
        await Clients.Group($"user_{recipientId}").SendAsync("ReceiveMessage", dto);

        // Send notification to recipient.
        var notification = new ChatNotificationDto(
            conversationId,
            dto.SenderName,
            dto.Content.Length > 80 ? dto.Content[..77] + "..." : dto.Content,
            dto.SentAt);

        await Clients.Group($"user_{recipientId}").SendAsync("ChatNotification", notification);

        // Only persist to the notification inbox when the recipient has no app tab open at all —
        // an active session already gets the live toast above, so this avoids flooding the inbox
        // during a back-and-forth conversation while still catching "you got a message while away".
        if (!_notificationTracker.IsOnline(recipientId))
        {
            await _notifications.DispatchAsync(
                recipientId,
                NotificationType.ChatMessage,
                "ChatMessageReceived",
                [dto.SenderName],
                "/chat");
        }
    }

    /// <summary>
    /// Mark all messages in a conversation as read for the current user.
    /// </summary>
    public async Task MarkAsRead(Guid conversationId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return;

        await _chatCommandService.MarkAsReadAsync(userId, conversationId);
    }
}
