using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Interfaces;

/// <summary>
/// Client service interface for chat operations (REST + SignalR).
/// </summary>
public interface IChatService : IAsyncDisposable
{
    /// <summary>
    /// Fired when a new message arrives via SignalR.
    /// </summary>
    event Action<ChatMessageDto>? OnMessageReceived;

    /// <summary>
    /// Fired when a chat notification arrives.
    /// </summary>
    event Action<ChatNotificationDto>? OnNotificationReceived;

    /// <summary>
    /// True when the SignalR connection is active.
    /// </summary>
    bool IsConnected
    {
        get;
    }

    Task StartAsync();
    Task StopAsync();

    Task<PagedResult<ChatConversationDto>> GetConversationsAsync( int page = 1 , int pageSize = 10 );
    Task<PagedResult<ChatMessageDto>> GetMessagesAsync( Guid conversationId , int page = 1 , int pageSize = 20 );
    Task<ChatConversationDto?> StartConversationWithAsync( string targetUserId , string? initialMessage = null );
    Task<List<ChatUserDto>> GetAvailableUsersAsync( string? search = null );
    Task SendMessageAsync( Guid conversationId , string content );
    Task MarkAsReadAsync( Guid conversationId );
    Task<int> GetUnreadCountAsync();
    Task<bool> DeleteConversationAsync( Guid conversationId );
}
