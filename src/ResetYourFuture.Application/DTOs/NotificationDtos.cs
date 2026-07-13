namespace ResetYourFuture.Application.DTOs;

/// <summary>One notification for the current user. TitleKey + BodyArgs are resolved against
/// the client's NotificationRes so the same row renders correctly in either culture.</summary>
public record NotificationDto(
    Guid Id,
    string Type,
    string TitleKey,
    List<string> BodyArgs,
    string? LinkUrl,
    bool IsRead,
    DateTimeOffset CreatedAt
);

public record NotificationSummaryDto(int UnreadCount);
