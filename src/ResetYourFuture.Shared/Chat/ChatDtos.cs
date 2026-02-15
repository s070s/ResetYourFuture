namespace ResetYourFuture.Shared.Chat;

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
    int UnreadCount
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
    bool IsRead
);

/// <summary>
/// Request to send a new message.
/// </summary>
public record SendMessageRequest(
    Guid ConversationId,
    string Content
);

/// <summary>
/// Request to start a conversation with a specific user.
/// </summary>
public record StartConversationRequest(
    string TargetUserId,
    string? InitialMessage
);

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
    string Role
);
