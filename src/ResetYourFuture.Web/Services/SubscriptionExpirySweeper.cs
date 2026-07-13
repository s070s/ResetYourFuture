using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Polls for <see cref="UserSubscription"/> rows whose <c>ExpiresAt</c> has passed while still
/// <c>IsActive</c>, reverts each to the Free plan, and records an <see cref="BillingTransactionType.Expired"/>
/// transaction (BIZ-1). <c>SubscriptionService</c>'s status/tier reads already exclude
/// expired-but-unswept rows, so this sweep is about correcting the stored state and notifying
/// the user — not a prerequisite for correct access control.
///
/// Uses a plain Task.Delay loop, matching <see cref="CallRingMonitor"/>'s convention. A billing
/// period is measured in months, so a coarse poll interval is appropriate — unlike the
/// sub-minute monitors elsewhere in this file, being off by a few minutes here is immaterial.
/// </summary>
public sealed class SubscriptionExpirySweeper : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<SubscriptionExpirySweeper> _logger;

    public SubscriptionExpirySweeper(IServiceProvider services, ILogger<SubscriptionExpirySweeper> logger)
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
                await SweepExpiredSubscriptionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubscriptionExpirySweeper: Error during sweep.");
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

    private async Task SweepExpiredSubscriptionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var now = DateTime.UtcNow;
        var expired = await db.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
            .Where(us => us.IsActive && us.ExpiresAt != null && us.ExpiresAt <= now)
            .ToListAsync(stoppingToken);

        if (expired.Count == 0)
            return;

        var freePlan = await db.SubscriptionPlans
            .FirstOrDefaultAsync(sp => sp.Tier == SubscriptionTier.Free && sp.IsActive, stoppingToken);

        if (freePlan is null)
        {
            _logger.LogWarning(
                "SubscriptionExpirySweeper: Free plan not found — cannot revert {Count} expired subscription(s) this pass.",
                expired.Count);
            return;
        }

        foreach (var sub in expired)
        {
            var planName = sub.SubscriptionPlan.Name;
            sub.IsActive = false;

            db.UserSubscriptions.Add(new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = sub.UserId,
                SubscriptionPlanId = freePlan.Id,
                StartedAt = now,
                ExpiresAt = null,
                IsActive = true
            });

            db.BillingTransactions.Add(new BillingTransaction
            {
                Id = Guid.NewGuid(),
                UserId = sub.UserId,
                SubscriptionPlanId = freePlan.Id,
                Amount = 0m,
                Currency = "EUR",
                Type = BillingTransactionType.Expired,
                Description = $"{planName} expired — downgraded to Free",
                CreatedAt = now
            });

            // Matches SubscriptionService's private StatusCacheKey format (30s TTL) — expiry
            // now takes effect immediately for this user instead of waiting out the cache window.
            cache.Remove($"sub_status:{sub.UserId}");
        }

        await db.SaveChangesAsync(stoppingToken);

        foreach (var sub in expired)
        {
            try
            {
                await notifications.DispatchAsync(
                    sub.UserId, NotificationType.SubscriptionExpiring, "SubscriptionExpired",
                    [sub.SubscriptionPlan.Name], "/pricing", stoppingToken);
            }
            catch (Exception ex)
            {
                // Best-effort per-recipient — one failed notification must not block the others
                // or re-run the whole sweep next cycle (the DB state is already correct).
                _logger.LogWarning(ex, "SubscriptionExpirySweeper: Failed to notify user {UserId} of expiry.", sub.UserId);
            }
        }

        _logger.LogInformation(
            "SubscriptionExpirySweeper: Reverted {Count} expired subscription(s) to Free.", expired.Count);
    }
}
