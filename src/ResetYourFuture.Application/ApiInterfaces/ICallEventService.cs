using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Persists call session/participant lifecycle and the chat-history events that accompany 1:1 calls.
/// </summary>
public interface ICallEventService
{
    Task<CallSession> CreateSessionAsync(string initiatorId, Guid? conversationId, IEnumerable<string> inviteeIds);
    Task MarkParticipantAsync(Guid callId, string userId, CallParticipantStatus status);
    Task EndSessionAsync(Guid callId, CallEndReason reason);
    Task<ChatMessageDto?> RecordChatEventAsync(Guid callId, CallEventKind kind);
}
