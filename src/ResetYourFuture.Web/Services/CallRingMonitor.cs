using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Web.Hubs;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Polls <see cref="CallRegistry"/> for ring timeouts and disconnect-grace expiries, persists the
/// resulting state via scoped services, and notifies clients over <see cref="CallHub"/>/<see cref="ChatHub"/>.
/// Also sweeps dangling <c>CallSession</c> rows left open by a previous process at startup, since
/// <see cref="CallRegistry"/> is an in-memory singleton and loses all state on restart.
///
/// Uses a plain Task.Delay loop (rather than PeriodicTimer) to match this repo's existing
/// BackgroundService convention (see BulkStudentSeedingService).
/// </summary>
public sealed class CallRingMonitor : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int DisconnectGraceSeconds = 15;

    private readonly IServiceProvider _services;
    private readonly CallRegistry _registry;
    private readonly WebRtcOptions _options;
    private readonly IHubContext<CallHub> _callHub;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly ILogger<CallRingMonitor> _logger;

    public CallRingMonitor(
        IServiceProvider services,
        CallRegistry registry,
        IOptions<WebRtcOptions> options,
        IHubContext<CallHub> callHub,
        IHubContext<ChatHub> chatHub,
        ILogger<CallRingMonitor> logger)
    {
        _services = services;
        _registry = registry;
        _options = options.Value;
        _callHub = callHub;
        _chatHub = chatHub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SweepDanglingSessionsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CallRingMonitor: Error during poll iteration.");
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

    private async Task PollOnceAsync()
    {
        using var scope = _services.CreateScope();
        var callEventService = scope.ServiceProvider.GetRequiredService<ICallEventService>();

        var expiredInvites = _registry.TakeExpiredInvites(_options.RingTimeoutSeconds);
        foreach (var (callId, userId) in expiredInvites)
        {
            await callEventService.MarkParticipantAsync(callId, userId, CallParticipantStatus.Missed);
            await _callHub.Clients.Group($"call_{callId}").SendAsync("CallUnavailable", callId, userId);
            await _callHub.Clients.Group($"user_{userId}").SendAsync("IncomingCallHandled", callId);

            await EndCallIfNeededAsync(callId, callEventService);
        }

        var expiredDisconnects = _registry.TakeExpiredDisconnects(DisconnectGraceSeconds);
        foreach (var (callId, userId) in expiredDisconnects)
        {
            await callEventService.MarkParticipantAsync(callId, userId, CallParticipantStatus.Left);
            await _callHub.Clients.Group($"call_{callId}").SendAsync("ParticipantLeft", callId, userId);

            await EndCallIfNeededAsync(callId, callEventService);
        }
    }

    private async Task EndCallIfNeededAsync(Guid callId, ICallEventService callEventService)
    {
        if (!_registry.ShouldEndCall(callId))
            return;

        var call = _registry.GetCall(callId);
        if (call is null)
            return;

        var reason = call.HasConnected ? CallEndReason.Completed : CallEndReason.Missed;

        await callEventService.EndSessionAsync(callId, reason);

        if (!call.IsGroup)
        {
            var kind = reason == CallEndReason.Missed ? CallEventKind.Missed : CallEventKind.Ended;
            var chatMessage = await callEventService.RecordChatEventAsync(callId, kind);
            if (chatMessage is not null)
            {
                // Include InitiatorId explicitly: by the time a call ends, the initiator may
                // already have been removed from Participants (e.g. they left/cancelled earlier),
                // but they should still see the live chat update in their own conversation pane.
                var recipientIds = call.Participants.Keys.Append(call.InitiatorId).Distinct();
                foreach (var participantId in recipientIds)
                {
                    await _chatHub.Clients.Group($"user_{participantId}").SendAsync("ReceiveMessage", chatMessage);
                }
            }
        }

        await _callHub.Clients.Group($"call_{callId}").SendAsync("CallEnded", callId, reason);
        _registry.RemoveCall(callId);
    }

    private async Task SweepDanglingSessionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var dangling = await db.CallSessions
            .Where(s => s.EndedAt == null)
            .ToListAsync(stoppingToken);

        if (dangling.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var session in dangling)
        {
            session.EndedAt = now;
            session.EndReason = CallEndReason.Cancelled;
        }

        await db.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("CallRingMonitor: Swept {Count} dangling call session(s) from a previous process.", dangling.Count);
    }
}
