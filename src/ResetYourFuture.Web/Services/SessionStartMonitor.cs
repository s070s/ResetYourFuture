using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Polls <see cref="ScheduledSession"/> rows for status transitions and the 15-minutes-before
/// reminder. Does not create any <c>CallSession</c> itself — the first participant to open the
/// session from /sessions and start the call does that through the existing group-call flow;
/// this monitor only drives the schedule row's own lifecycle and notifications.
///
/// Uses a plain Task.Delay loop, matching <see cref="CallRingMonitor"/>'s convention.
/// </summary>
public sealed class SessionStartMonitor : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReminderWindow = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<SessionStartMonitor> _logger;

    public SessionStartMonitor(IServiceProvider services, ILogger<SessionStartMonitor> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SessionStartMonitor: Error during poll iteration.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // graceful shutdown (Ctrl+C) — exit the loop quietly
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var now = DateTimeOffset.UtcNow;

        await SendDueRemindersAsync(db, notifications, now, cancellationToken);
        await GoLiveAsync(db, now, cancellationToken);
        await EndExpiredAsync(db, now, cancellationToken);
    }

    private async Task SendDueRemindersAsync(
        IApplicationDbContext db, INotificationDispatcher notifications, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reminderCutoff = now.Add(ReminderWindow);

        var due = await db.ScheduledSessions
            .Where(s => s.Status == ScheduledSessionStatus.Scheduled
                && s.ReminderSentAt == null
                && s.StartsAtUtc <= reminderCutoff
                && s.StartsAtUtc > now)
            .Include(s => s.Registrations)
            .ToListAsync(cancellationToken);

        foreach (var session in due)
        {
            var recipientIds = new[] { session.HostUserId }
                .Concat(session.Registrations.Select(r => r.UserId))
                .Distinct();

            foreach (var userId in recipientIds)
            {
                try
                {
                    await notifications.DispatchAsync(
                        userId, NotificationType.SessionReminder, "SessionReminder",
                        [session.TitleEn], "sessions", cancellationToken);
                }
                catch (Exception ex)
                {
                    // Best-effort per-recipient — one failed dispatch must not block the others
                    // or leave ReminderSentAt unset (which would just retry-storm every poll).
                    _logger.LogWarning(ex, "SessionStartMonitor: Failed to dispatch reminder for session {SessionId} to user {UserId}.", session.Id, userId);
                }
            }

            session.ReminderSentAt = now;
        }

        if (due.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task GoLiveAsync(IApplicationDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var due = await db.ScheduledSessions
            .Where(s => s.Status == ScheduledSessionStatus.Scheduled && s.StartsAtUtc <= now)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
            return;

        foreach (var session in due)
            session.Status = ScheduledSessionStatus.Live;

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EndExpiredAsync(IApplicationDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var live = await db.ScheduledSessions
            .Where(s => s.Status == ScheduledSessionStatus.Live)
            .ToListAsync(cancellationToken);

        var expired = live.Where(s => s.StartsAtUtc.AddMinutes(s.DurationMinutes) <= now).ToList();
        if (expired.Count == 0)
            return;

        foreach (var session in expired)
            session.Status = ScheduledSessionStatus.Ended;

        await db.SaveChangesAsync(cancellationToken);
    }
}
