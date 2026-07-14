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
    /// <summary>
    /// Persists a chat message. <paramref name="senderName"/> and <paramref name="senderRole"/>
    /// are supplied by the caller from the authenticated principal's claims, so the send path
    /// no longer re-queries the identity store for the sender's display name and role (PERF-7).
    /// </summary>
    Task<ServiceResult<ChatMessageSendResult>> SendMessageAsync(string senderId, string senderName, string senderRole, Guid conversationId, string content, CancellationToken ct = default);
    Task MarkAsReadAsync(string userId, Guid conversationId, CancellationToken ct = default);
}
