using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Chat history, conversations, and management queries.
/// </summary>
public interface IChatQueryService
{
    Task<PagedResult<ChatConversationDto>> GetConversationsAsync(string userId, int page, int pageSize, CancellationToken ct = default);
    Task<ServiceResult<PagedResult<ChatMessageDto>>> GetMessagesAsync(string userId, Guid conversationId, int page, int pageSize, CancellationToken ct = default);
    Task<ServiceResult<ChatConversationDto>> StartConversationAsync(string callerId, StartConversationRequest request);
    Task<List<ChatUserDto>> GetAvailableUsersAsync(string userId, string? search);
    Task<ServiceResult<bool>> DeleteConversationAsync(string userId, Guid conversationId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(string userId);
}
