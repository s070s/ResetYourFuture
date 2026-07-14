using System.ComponentModel.DataAnnotations;
using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// Represents a chat conversation between two users.
/// </summary>
public record ChatConversationDto(
    Guid Id,
    string OtherUserId,
    string OtherUserName,
    string OtherUserRole,
    string? LastMessageContent,
    DateTime? LastMessageAt,
    int UnreadCount,
    DateTime? OtherUserLastSeenAt = null
);

/// <summary>
/// A single chat message.
/// </summary>
public record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    string SenderId,
    string SenderName,
    string SenderRole,
    string Content,
    DateTime SentAt,
    bool IsRead,
    CallEventKind? CallEvent = null,
    int? CallDurationSeconds = null
);

/// <summary>
/// Request to send a new message.
/// </summary>
public record SendMessageRequest(
    Guid ConversationId,
    [Required, MaxLength(4000)] string Content
);

/// <summary>
/// Request to start a conversation with a specific user.
/// </summary>
public record StartConversationRequest(
    [Required] string TargetUserId,
    [MaxLength(4000)] string? InitialMessage
);

/// <summary>
/// Result of persisting a chat message: the message itself plus the other participant's user ID,
/// so the caller (the hub) knows who to broadcast/notify without a second conversation lookup.
/// </summary>
public record ChatMessageSendResult(ChatMessageDto Message, string RecipientId);

/// <summary>
/// Real-time notification pushed via SignalR.
/// </summary>
public record ChatNotificationDto(
    Guid ConversationId,
    string SenderName,
    string ContentPreview,
    DateTime SentAt
);

/// <summary>
/// Lightweight user DTO for the user picker (who can I chat with?).
/// </summary>
public record ChatUserDto(
    string Id,
    string FullName,
    string Role,
    DateTime? LastSeenAt = null
);
