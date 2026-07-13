using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.Mappings;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// Persists call session/participant lifecycle and the chat-history events that accompany 1:1 calls.
/// </summary>
public class CallEventService(
    IApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : ICallEventService
{
    public async Task<CallSession> CreateSessionAsync(string initiatorId, Guid? conversationId, IEnumerable<string> inviteeIds)
    {
        var session = new CallSession
        {
            InitiatorId = initiatorId,
            ConversationId = conversationId
        };

        session.Participants.Add(new CallParticipant
        {
            CallSessionId = session.Id,
            UserId = initiatorId,
            Status = CallParticipantStatus.Invited
        });

        foreach (var inviteeId in inviteeIds.Distinct())
        {
            session.Participants.Add(new CallParticipant
            {
                CallSessionId = session.Id,
                UserId = inviteeId,
                Status = CallParticipantStatus.Invited
            });
        }

        db.CallSessions.Add(session);
        await db.SaveChangesAsync();

        return session;
    }

    public async Task MarkParticipantAsync(Guid callId, string userId, CallParticipantStatus status)
    {
        var participant = await db.CallParticipants
            .FirstOrDefaultAsync(p => p.CallSessionId == callId && p.UserId == userId);

        if (participant is null)
            return;

        participant.Status = status;

        var now = DateTime.UtcNow;
        if (status == CallParticipantStatus.Joined)
        {
            participant.JoinedAt = now;

            var session = await db.CallSessions.FirstOrDefaultAsync(s => s.Id == callId);
            if (session is not null && session.ConnectedAt is null)
            {
                session.ConnectedAt = now;
            }
        }
        else if (status is CallParticipantStatus.Left or CallParticipantStatus.Declined or CallParticipantStatus.Missed)
        {
            participant.LeftAt = now;
        }

        await db.SaveChangesAsync();
    }

    public async Task EndSessionAsync(Guid callId, CallEndReason reason)
    {
        var session = await db.CallSessions.FirstOrDefaultAsync(s => s.Id == callId);
        if (session is null)
            return;

        session.EndedAt = DateTime.UtcNow;
        session.EndReason = reason;

        await db.SaveChangesAsync();
    }

    public async Task<ChatMessageDto?> RecordChatEventAsync(Guid callId, CallEventKind kind)
    {
        var session = await db.CallSessions.FirstOrDefaultAsync(s => s.Id == callId);
        if (session is null || session.ConversationId is null)
            return null;

        var conversation = await db.ChatConversations.FirstOrDefaultAsync(c => c.Id == session.ConversationId);
        if (conversation is null)
            return null;

        var content = BuildContent(kind, session);

        var message = new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderId = session.InitiatorId,
            Content = content,
            SentAt = DateTime.UtcNow,
            CallSessionId = session.Id,
            CallEvent = kind
        };

        db.ChatMessages.Add(message);

        conversation.LastMessageContent = content.Length > 500 ? content[..497] + "..." : content;
        conversation.LastMessageAt = message.SentAt;

        await db.SaveChangesAsync();

        var initiator = await userManager.FindByIdAsync(session.InitiatorId);
        var roles = initiator is not null ? await userManager.GetRolesAsync(initiator) : [];
        var senderRole = roles.FirstOrDefault() ?? "User";
        var senderName = initiator is not null ? $"{initiator.FirstName} {initiator.LastName}" : "Unknown";

        return message.ToDto(senderName, senderRole, ChatMappings.CallDurationSeconds(session));
    }

    private static string BuildContent(CallEventKind kind, CallSession session) => kind switch
    {
        CallEventKind.Started => "Video call started",
        CallEventKind.Missed => "Missed call",
        CallEventKind.Ended => $"Video call ended · {FormatDuration(session)}",
        _ => "Video call"
    };

    private static string FormatDuration(CallSession session)
    {
        if (session.ConnectedAt is null || session.EndedAt is null)
            return "0:00";

        var duration = session.EndedAt.Value - session.ConnectedAt.Value;
        return $"{(int)duration.TotalMinutes}:{duration.Seconds:D2}";
    }
}
