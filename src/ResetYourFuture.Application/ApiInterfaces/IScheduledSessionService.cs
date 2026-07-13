using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Scheduled live group sessions (office hours, group coaching) layered on the existing WebRTC
/// call stack — a schedule row doesn't create a call by itself; the first participant to start
/// it from the /sessions page triggers the normal group-call flow, and <c>LinkCallSessionAsync</c>
/// records the resulting CallSessionId for everyone else's UI.
/// </summary>
public interface IScheduledSessionService
{
    /// <summary>Upcoming (Scheduled/Live) sessions, soonest first.</summary>
    Task<IReadOnlyList<ScheduledSessionListItemDto>> GetUpcomingAsync(
        string? userId, string lang, CancellationToken cancellationToken = default);

    /// <summary>Registers a user for a session. Rejects the host, duplicates, and a full session.</summary>
    Task<ServiceResult<string>> RegisterAsync(Guid sessionId, string userId, CancellationToken cancellationToken = default);

    Task<ServiceResult<string>> UnregisterAsync(Guid sessionId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Records which real CallSession this schedule row materialized into. First writer wins.</summary>
    Task<ServiceResult<string>> LinkCallSessionAsync(
        Guid sessionId, string userId, Guid callSessionId, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminScheduledSessionDto>> GetAllForAdminAsync(
        int page, int pageSize, string sortBy, string sortDir, CancellationToken cancellationToken = default);

    Task<AdminScheduledSessionDto> CreateAsync(
        string hostUserId, SaveScheduledSessionRequest request, CancellationToken cancellationToken = default);

    Task<AdminScheduledSessionDto?> UpdateAsync(
        Guid id, SaveScheduledSessionRequest request, CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
