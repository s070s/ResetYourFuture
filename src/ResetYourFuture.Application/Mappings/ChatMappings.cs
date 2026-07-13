using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Application.Mappings;

/// <summary>
/// Shared chat entity→DTO mappers (MAINT-1). ChatMessageDto used to be hand-built in three
/// files (ChatHub, CallEventService, ChatQueryService) and ChatUserDto in two — the copies
/// had already begun to drift. One definition per DTO keeps a new field a one-file edit.
/// </summary>
public static class ChatMappings
{
    /// <summary>
    /// Maps a message to its DTO. Sender name/role are passed in because resolving them
    /// differs per caller (fresh Identity lookup in the hub, batched role map in queries);
    /// everything else reads off the entity (fresh messages default IsRead=false and carry
    /// their CallEvent, so all three call sites agree).
    /// </summary>
    public static ChatMessageDto ToDto(this ChatMessage m, string senderName, string senderRole, int? callDurationSeconds = null) =>
        new(m.Id, m.ConversationId, m.SenderId, senderName, senderRole, m.Content, m.SentAt, m.IsRead, m.CallEvent, callDurationSeconds);

    /// <summary>Duration of a completed call, or null while ringing/unanswered.</summary>
    public static int? CallDurationSeconds(CallSession? session) =>
        session is { ConnectedAt: not null, EndedAt: not null }
            ? (int)(session.EndedAt.Value - session.ConnectedAt.Value).TotalSeconds
            : null;

    /// <summary>User-picker row; role comes from the caller's batched role map.</summary>
    public static ChatUserDto ToChatUserDto(this ApplicationUser u, string role) =>
        new(u.Id, $"{u.FirstName} {u.LastName}", role, u.LastSeenAt);
}
