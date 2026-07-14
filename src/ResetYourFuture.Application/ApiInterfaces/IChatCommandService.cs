using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Validates and persists chat writes. Sibling of <see cref="IChatQueryService"/> (reads) —
/// split the same way the Calls feature splits <c>ICallEventService</c> from
/// <c>ICallQueryService</c>, so ChatHub keeps only connection/group management and broadcasting.
/// </summary>
public interface IChatCommandService
{
    Task<ServiceResult<ChatMessageSendResult>> SendMessageAsync(string senderId, Guid conversationId, string content, CancellationToken ct = default);
    Task MarkAsReadAsync(string userId, Guid conversationId, CancellationToken ct = default);
}
