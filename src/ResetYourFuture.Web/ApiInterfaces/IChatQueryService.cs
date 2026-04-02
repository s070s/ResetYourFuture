using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.ApiInterfaces;

/// <summary>
/// Chat history, conversations, and management queries.
/// </summary>
public interface IChatQueryService
{
    Task<bool> HasChatAccessAsync( string userId , bool isAdmin );
    Task<PagedResult<ChatConversationDto>> GetConversationsAsync( string userId , int page , int pageSize , CancellationToken ct = default );
    Task<ServiceResult<PagedResult<ChatMessageDto>>> GetMessagesAsync( string userId , Guid conversationId , int page , int pageSize , CancellationToken ct = default );
    Task<ServiceResult<ChatConversationDto>> StartConversationAsync( string callerId , bool isAdmin , StartConversationRequest request );
    Task<List<ChatUserDto>> GetAvailableUsersAsync( string userId , string? search );
    Task<ServiceResult<bool>> DeleteConversationAsync( string userId , Guid conversationId , CancellationToken ct = default );
    Task<int> GetUnreadCountAsync( string userId );
}
