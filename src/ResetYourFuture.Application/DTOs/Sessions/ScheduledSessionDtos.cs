namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// Upcoming-session list entry. <see cref="OtherParticipantUserIds"/> (host + registrants, minus
/// the caller) is populated only when the caller is the host or a registrant — it's what the
/// client passes to <c>ICallService.StartCallAsync</c> to ring everyone else into the session.
/// </summary>
public record ScheduledSessionListItemDto(
    Guid Id,
    string Title,
    string HostName,
    string? CourseTitle,
    DateTimeOffset StartsAtUtc,
    int DurationMinutes,
    int MaxParticipants,
    int RegisteredCount,
    string Status,
    bool IsHost,
    bool IsRegistered,
    Guid? CallSessionId,
    IReadOnlyList<string> OtherParticipantUserIds);

public record LinkCallSessionRequest(Guid CallSessionId);

// --- Admin ---

public record AdminScheduledSessionDto(
    Guid Id,
    string TitleEn,
    string? TitleEl,
    string HostName,
    Guid? CourseId,
    string? CourseTitle,
    DateTimeOffset StartsAtUtc,
    int DurationMinutes,
    int MaxParticipants,
    int RegisteredCount,
    string Status,
    DateTimeOffset CreatedAt);

public record SaveScheduledSessionRequest(
    string TitleEn,
    string? TitleEl,
    Guid? CourseId,
    DateTimeOffset StartsAtUtc,
    int DurationMinutes,
    int MaxParticipants);
