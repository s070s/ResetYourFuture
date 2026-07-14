using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.Mappings;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// Validates and persists chat writes (message length cap, participant membership) — the
/// business rules ChatHub used to enforce inline. Sibling of <see cref="ChatQueryService"/>.
/// Sender enablement is enforced live at connection time by <c>ChatHub.OnConnectedAsync</c>;
/// the send path trusts the established connection and no longer re-queries the identity store.
/// </summary>
public class ChatCommandService(IApplicationDbContext db) : IChatCommandService
{
    private const int MaxMessageLength = 4_000;

    public async Task<ServiceResult<ChatMessageSendResult>> SendMessageAsync(
        string senderId, string senderName, string senderRole, Guid conversationId, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ServiceResult<ChatMessageSendResult>.BadRequest(error: "Message content is required.");

        if (content.Length > MaxMessageLength)
            return ServiceResult<ChatMessageSendResult>.BadRequest(
                error: $"Message exceeds the {MaxMessageLength:N0} character limit.");

        var conversation = await db.ChatConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
            return ServiceResult<ChatMessageSendResult>.NotFound();

        if (conversation.CreatorId != senderId && conversation.ParticipantId != senderId)
            return ServiceResult<ChatMessageSendResult>.Forbidden();

        // Sender display name and role come from the caller's claims (PERF-7), so the send path
        // no longer runs FindByIdAsync + GetRolesAsync per message purely to build the DTO.
        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content.Trim(),
            SentAt = DateTime.UtcNow
        };

        db.ChatMessages.Add(message);

        conversation.LastMessageContent = message.Content.Length > 500
            ? message.Content[..497] + "..."
            : message.Content;
        conversation.LastMessageAt = message.SentAt;

        await db.SaveChangesAsync(ct);

        var dto = message.ToDto(senderName, senderRole);
        var recipientId = conversation.CreatorId == senderId ? conversation.ParticipantId : conversation.CreatorId;

        return ServiceResult<ChatMessageSendResult>.Ok(new ChatMessageSendResult(dto, recipientId));
    }

    public async Task MarkAsReadAsync(string userId, Guid conversationId, CancellationToken ct = default)
    {
        await db.ChatMessages
            .Where(m => m.ConversationId == conversationId
                      && m.SenderId != userId
                      && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true), ct);
    }
}
