using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.Mappings;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Extensions;

namespace ResetYourFuture.Application.ApiServices;

/// <inheritdoc cref="IScheduledSessionService"/>
public class ScheduledSessionService(IApplicationDbContext db, ILogger<ScheduledSessionService> logger) : IScheduledSessionService
{
    public async Task<IReadOnlyList<ScheduledSessionListItemDto>> GetUpcomingAsync(
        string? userId, string lang, CancellationToken cancellationToken = default)
    {
        var isEl = Localized.IsEl(lang);

        var rows = await db.ScheduledSessions
            .AsNoTracking()
            .Where(s => s.Status == ScheduledSessionStatus.Scheduled || s.Status == ScheduledSessionStatus.Live)
            .OrderBy(s => s.StartsAtUtc).ThenBy(s => s.Id)
            // Inline ternary, not Localized.Pick: this Select is translated to SQL, and EF
            // Core cannot translate an arbitrary method call.
            .Select(s => new
            {
                s.Id,
                s.TitleEn,
                s.TitleEl,
                HostId = s.HostUserId,
                HostNameEn = !string.IsNullOrWhiteSpace(s.Host!.DisplayName) ? s.Host.DisplayName! : (s.Host.FirstName + " " + s.Host.LastName).Trim(),
                CourseTitle = s.Course != null ? (isEl ? (s.Course.TitleEl ?? s.Course.TitleEn) : s.Course.TitleEn) : null,
                s.StartsAtUtc,
                s.DurationMinutes,
                s.MaxParticipants,
                s.Status,
                s.CallSessionId,
                RegistrantIds = s.Registrations.Select(r => r.UserId).ToList()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(s =>
        {
            var isHost = userId is not null && s.HostId == userId;
            var isRegistered = userId is not null && s.RegistrantIds.Contains(userId);

            var otherIds = isHost || isRegistered
                ? new[] { s.HostId }.Concat(s.RegistrantIds).Where(id => id != userId).Distinct().ToList()
                : [];

            return new ScheduledSessionListItemDto(
                s.Id,
                Localized.Pick(isEl, s.TitleEn, s.TitleEl),
                s.HostNameEn,
                s.CourseTitle,
                s.StartsAtUtc,
                s.DurationMinutes,
                s.MaxParticipants,
                s.RegistrantIds.Count,
                s.Status.ToString(),
                isHost,
                isRegistered,
                s.CallSessionId,
                otherIds);
        }).ToList();
    }

    public async Task<ServiceResult<string>> RegisterAsync(Guid sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var session = await db.ScheduledSessions
            .Include(s => s.Registrations)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
            return ServiceResult<string>.NotFound(error: "Session not found.");

        if (session.Status is ScheduledSessionStatus.Ended or ScheduledSessionStatus.Cancelled)
            return ServiceResult<string>.BadRequest(error: "This session is no longer accepting registrations.");

        if (session.HostUserId == userId)
            return ServiceResult<string>.BadRequest(error: "You are already the host of this session.");

        if (session.Registrations.Any(r => r.UserId == userId))
            return ServiceResult<string>.Conflict(error: "You are already registered for this session.");

        if (session.Registrations.Count >= session.MaxParticipants)
            return ServiceResult<string>.Conflict(error: "This session is full.");

        db.SessionRegistrations.Add(new SessionRegistration { SessionId = sessionId, UserId = userId });
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<string>.Ok("Registered.");
    }

    public async Task<ServiceResult<string>> UnregisterAsync(Guid sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var registration = await db.SessionRegistrations
            .FirstOrDefaultAsync(r => r.SessionId == sessionId && r.UserId == userId, cancellationToken);

        if (registration is null)
            return ServiceResult<string>.NotFound(error: "You are not registered for this session.");

        db.SessionRegistrations.Remove(registration);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<string>.Ok("Unregistered.");
    }

    public async Task<ServiceResult<string>> LinkCallSessionAsync(
        Guid sessionId, string userId, Guid callSessionId, CancellationToken cancellationToken = default)
    {
        var session = await db.ScheduledSessions
            .Include(s => s.Registrations)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
            return ServiceResult<string>.NotFound(error: "Session not found.");

        var isParticipant = session.HostUserId == userId || session.Registrations.Any(r => r.UserId == userId);
        if (!isParticipant)
            return ServiceResult<string>.Forbidden(error: "Only the host or a registrant can link a call to this session.");

        // First writer wins — a second concurrent StartCallAsync would have created a distinct
        // CallSession, but we only ever want the earliest one linked for this schedule row.
        if (session.CallSessionId is null)
        {
            session.CallSessionId = callSessionId;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<string>.Ok("Linked.");
    }

    public async Task<PagedResult<AdminScheduledSessionDto>> GetAllForAdminAsync(
        int page, int pageSize, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ScheduledSessions.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(SessionMappings.AdminProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminScheduledSessionDto>(items, totalCount, page, pageSize, sortBy, sortDir);
    }

    public async Task<AdminScheduledSessionDto> CreateAsync(
        string hostUserId, SaveScheduledSessionRequest request, CancellationToken cancellationToken = default)
    {
        var session = new ScheduledSession
        {
            Id = Guid.NewGuid(),
            HostUserId = hostUserId,
            TitleEn = request.TitleEn,
            TitleEl = request.TitleEl,
            CourseId = request.CourseId,
            StartsAtUtc = request.StartsAtUtc,
            DurationMinutes = request.DurationMinutes,
            MaxParticipants = Math.Clamp(request.MaxParticipants, 1, 6)
        };

        db.ScheduledSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Scheduled session created: {Id} '{Title}' at {StartsAt}.", session.Id, session.TitleEn, session.StartsAtUtc);
        return await GetAdminByIdAsync(session.Id, cancellationToken);
    }

    public async Task<AdminScheduledSessionDto?> UpdateAsync(
        Guid id, SaveScheduledSessionRequest request, CancellationToken cancellationToken = default)
    {
        var session = await db.ScheduledSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
            return null;

        session.TitleEn = request.TitleEn;
        session.TitleEl = request.TitleEl;
        session.CourseId = request.CourseId;
        session.StartsAtUtc = request.StartsAtUtc;
        session.DurationMinutes = request.DurationMinutes;
        session.MaxParticipants = Math.Clamp(request.MaxParticipants, 1, 6);

        await db.SaveChangesAsync(cancellationToken);
        return await GetAdminByIdAsync(id, cancellationToken);
    }

    public async Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await db.ScheduledSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
            return false;

        session.Status = ScheduledSessionStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Scheduled session cancelled: {Id}.", id);
        return true;
    }

    private async Task<AdminScheduledSessionDto> GetAdminByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.ScheduledSessions
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(SessionMappings.AdminProjection)
            .SingleAsync(cancellationToken);
    }
}
