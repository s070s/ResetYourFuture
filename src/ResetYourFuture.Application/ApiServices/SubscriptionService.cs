using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Extensions;
using System.Text.Json;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// Subscription management service with stub Stripe integration.
/// All Stripe operations are mocked for test/development mode.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly IMemoryCache _cache;
    private readonly INotificationDispatcher _notifications;
    private readonly bool _mockPaymentEnabled;

    private static string StatusCacheKey(string userId) => $"sub_status:{userId}";

    public SubscriptionService(
        IApplicationDbContext db, ILogger<SubscriptionService> logger, IMemoryCache cache,
        INotificationDispatcher notifications, IConfiguration configuration, IHostEnvironment environment)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
        _notifications = notifications;
        // BIZ-4: the mock-payment grant path must never run outside Development, regardless of
        // how Payment:MockEnabled is set — a config flag alone is one accidental copy-paste away
        // from granting free upgrades in a real environment.
        _mockPaymentEnabled = environment.IsDevelopment() && configuration.GetValue<bool>("Payment:MockEnabled");
    }

    public async Task<List<SubscriptionPlanDto>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var raw = await _db.SubscriptionPlans
            .AsNoTracking()
            .Where(sp => sp.IsActive)
            .OrderBy(sp => sp.Tier)
            .ThenBy(sp => sp.Price)
            .Select(sp => new
            {
                sp.Id,
                sp.Name,
                sp.Description,
                sp.Price,
                sp.BillingPeriod,
                sp.Tier,
                sp.FeaturesJson,
                sp.IsActive
            })
            .ToListAsync(cancellationToken);

        var plans = raw.Select(sp => new SubscriptionPlanDto(
            sp.Id,
            sp.Name,
            sp.Description,
            sp.Price,
            sp.BillingPeriod.ToString(),
            sp.Tier,
            DeserializeFeatures(sp.FeaturesJson, sp.Id, sp.Name),
            sp.IsActive
        )).ToList();

        return plans;
    }

    public async Task<UserSubscriptionStatusDto> GetUserStatusAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        // Cache the status for 30 s. Explicit invalidation occurs on plan change / cancellation.
        if (_cache.TryGetValue(StatusCacheKey(userId), out UserSubscriptionStatusDto? cached) && cached is not null)
            return cached;

        // BIZ-1: IsActive alone isn't enough — a subscription past its ExpiresAt hasn't been
        // swept yet (SubscriptionExpirySweeper runs periodically, not instantly) and must not
        // grant paid access in the meantime.
        var now = DateTime.UtcNow;
        var activeSub = await _db.UserSubscriptions
            .AsNoTracking()
            .Include(us => us.SubscriptionPlan)
            .Where(us => us.UserId == userId && us.IsActive && (us.ExpiresAt == null || us.ExpiresAt > now))
            .FirstOrDefaultAsync(cancellationToken);

        UserSubscriptionStatusDto status;

        if (activeSub is null)
        {
            status = new UserSubscriptionStatusDto(
                SubscriptionTier.Free,
                "Free",
                DateTime.UtcNow,
                null,
                true,
                GetDefaultFreeFeatures()
            );
        }
        else
        {
            status = new UserSubscriptionStatusDto(
                activeSub.SubscriptionPlan.Tier,
                activeSub.SubscriptionPlan.Name,
                activeSub.StartedAt,
                activeSub.ExpiresAt,
                activeSub.IsActive,
                DeserializeFeatures(activeSub.SubscriptionPlan.FeaturesJson, activeSub.SubscriptionPlanId, activeSub.SubscriptionPlan.Name)
            );
        }

        _cache.Set(StatusCacheKey(userId), status, TimeSpan.FromSeconds(30));
        return status;
    }

    public async Task<SubscriptionTier> GetUserTierAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tier = await _db.UserSubscriptions
            .AsNoTracking()
            .Include(us => us.SubscriptionPlan)
            .Where(us => us.UserId == userId && us.IsActive && (us.ExpiresAt == null || us.ExpiresAt > now))
            .Select(us => (SubscriptionTier?)us.SubscriptionPlan.Tier)
            .FirstOrDefaultAsync(cancellationToken);

        return tier ?? SubscriptionTier.Free;
    }

    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(
        string userId, Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(sp => sp.Id == planId && sp.IsActive, cancellationToken);

        if (plan is null)
        {
            return new CheckoutSessionDto(
                string.Empty,
                null,
                "error: plan not found"
            );
        }

        var mockSessionId = $"cs_test_{Guid.NewGuid():N}";

        // NOTE
        // No real payment provider is integrated.
        // With MockEnabled off (production default) checkout cannot proceed and returns
        // "pending_payment". With MockEnabled on (Development) the code below assigns the plan
        // immediately without any charge. Replace with real Stripe Checkout + webhook for production.
        if (!_mockPaymentEnabled)
        {
            _logger.LogWarning(
                "Checkout attempted for user {UserId}, plan {PlanName} — payment not yet available in production.",
                userId, plan.Name);

            return new CheckoutSessionDto(
                mockSessionId,
                null,
                "pending_payment"
            );
        }

        // Fetch the current active plan (not just tier) so we can detect same-tier switches (e.g. monthly→yearly).
        var currentPlan = await _db.UserSubscriptions
            .AsNoTracking()
            .Include(us => us.SubscriptionPlan)
            .Where(us => us.UserId == userId && us.IsActive)
            .Select(us => us.SubscriptionPlan)
            .FirstOrDefaultAsync(cancellationToken);

        var transactionType = currentPlan is null
            ? BillingTransactionType.Purchase
            : plan.Tier > currentPlan.Tier
                ? BillingTransactionType.Upgrade
                : plan.Tier < currentPlan.Tier
                    ? BillingTransactionType.Downgrade
                    : plan.Id == currentPlan.Id
                        ? BillingTransactionType.Renewal
                        : BillingTransactionType.PlanSwitch;

        _logger.LogInformation(
            "Mock checkout session created: {SessionId} for user {UserId}, plan {PlanName}",
            mockSessionId, userId, plan.Name);

        await AssignPlanAsync(userId, planId, cancellationToken);

        _db.BillingTransactions.Add(new BillingTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlanId = planId,
            Amount = plan.Price,
            Currency = "EUR",
            Type = transactionType,
            // BIZ-4: prefixed so a mock (unpaid) grant is never mistaken for a real charge in reporting.
            Description = $"[MOCK] {transactionType} to {plan.Name}",
            StripeSessionId = mockSessionId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _cache.Remove(StatusCacheKey(userId));

        await _notifications.DispatchAsync(
            userId,
            NotificationType.SubscriptionActivated,
            "SubscriptionActivated",
            [plan.Name],
            "/billing",
            cancellationToken);

        return new CheckoutSessionDto(
            mockSessionId,
            $"/subscription/success?session_id={mockSessionId}",
            "complete"
        );
    }

    public async Task AssignPlanAsync(
        string userId, Guid planId, CancellationToken cancellationToken = default)
    {
        // Deactivate any existing active subscription
        var existingActive = await _db.UserSubscriptions
            .Where(us => us.UserId == userId && us.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var sub in existingActive)
        {
            sub.IsActive = false;
            sub.CancelledAt = DateTime.UtcNow;
        }

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(sp => sp.Id == planId, cancellationToken)
            ?? throw new InvalidOperationException($"Plan {planId} not found");

        var expiresAt = plan.BillingPeriod switch
        {
            BillingPeriod.Monthly => DateTime.UtcNow.AddMonths(1),
            BillingPeriod.Quarterly => DateTime.UtcNow.AddMonths(3),
            BillingPeriod.Yearly => DateTime.UtcNow.AddYears(1),
            BillingPeriod.Lifetime => (DateTime?)null,
            _ => DateTime.UtcNow.AddMonths(1)
        };

        var newSub = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlanId = planId,
            StartedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsActive = true
        };

        _db.UserSubscriptions.Add(newSub);
        // NOTE: SaveChangesAsync is intentionally NOT called here.
        // Callers are responsible for persisting — this allows them to add billing transactions
        // and commit everything atomically in a single SaveChangesAsync call.
        // Cache eviction is also the caller's responsibility (after SaveChangesAsync) so a
        // concurrent GetUserStatusAsync cannot re-cache stale data in the window before the commit.

        _logger.LogInformation(
            "Staged plan assignment {PlanName} (Tier: {Tier}) for user {UserId}",
            plan.Name, plan.Tier, userId);
    }

    public async Task AssignFreePlanAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var freePlan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(sp => sp.Tier == SubscriptionTier.Free && sp.IsActive, cancellationToken);

        if (freePlan is null)
        {
            _logger.LogWarning("Free plan not found in database. Skipping assignment for user {UserId}.", userId);
            return;
        }

        await AssignPlanAsync(userId, freePlan.Id, cancellationToken);

        // Record initial free plan assignment
        _db.BillingTransactions.Add(new BillingTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlanId = freePlan.Id,
            Amount = 0m,
            Currency = "EUR",
            Type = BillingTransactionType.FreePlanAssignment,
            Description = "Free plan assigned on registration",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _cache.Remove(StatusCacheKey(userId));
    }

    public async Task<CancelSubscriptionResultDto> CancelSubscriptionAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var activeSub = await _db.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
            .Where(us => us.UserId == userId && us.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeSub is null || activeSub.SubscriptionPlan.Tier == SubscriptionTier.Free)
        {
            return new CancelSubscriptionResultDto(false, "You are already on the Free plan.");
        }

        var previousPlanName = activeSub.SubscriptionPlan.Name;

        // BIZ-2: cancellation stops renewal, it does not forfeit the period already paid for.
        // Lifetime plans have no ExpiresAt to wait out, so they still downgrade immediately;
        // every other plan just gets stamped CancelledAt and keeps IsActive/tier intact —
        // SubscriptionExpirySweeper reverts it to Free at ExpiresAt like any natural expiry.
        if (activeSub.ExpiresAt is null)
        {
            activeSub.IsActive = false;
            activeSub.CancelledAt = DateTime.UtcNow;

            var freePlan = await _db.SubscriptionPlans
                .FirstOrDefaultAsync(sp => sp.Tier == SubscriptionTier.Free && sp.IsActive, cancellationToken);

            if (freePlan is null)
            {
                return new CancelSubscriptionResultDto(false, "Free plan not available. Please contact support.");
            }

            var newSub = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubscriptionPlanId = freePlan.Id,
                StartedAt = DateTime.UtcNow,
                ExpiresAt = null,
                IsActive = true
            };

            _db.UserSubscriptions.Add(newSub);

            // Record the downgrade transaction
            _db.BillingTransactions.Add(new BillingTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubscriptionPlanId = freePlan.Id,
                Amount = 0m,
                Currency = "EUR",
                Type = BillingTransactionType.Downgrade,
                Description = $"Cancelled {previousPlanName} — downgraded to Free",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);

            _cache.Remove(StatusCacheKey(userId));

            _logger.LogInformation(
                "User {UserId} cancelled lifetime plan {PreviousPlan} and downgraded to Free immediately.",
                userId, previousPlanName);

            return new CancelSubscriptionResultDto(true, $"Your {previousPlanName} plan has been cancelled. You are now on the Free plan.");
        }

        activeSub.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _cache.Remove(StatusCacheKey(userId));

        _logger.LogInformation(
            "User {UserId} cancelled {PreviousPlan}; access remains until {ExpiresAt}.",
            userId, previousPlanName, activeSub.ExpiresAt);

        return new CancelSubscriptionResultDto(
            true,
            $"Your {previousPlanName} plan has been cancelled. You'll keep access until {activeSub.ExpiresAt:dd MMM yyyy}.");
    }

    public async Task<BillingOverviewDto> GetBillingOverviewAsync(
        string userId, int page = 1, int pageSize = 10, string sortBy = "createdat", string sortDir = "desc", CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PagingParams.Normalize(page, pageSize);

        var status = await GetUserStatusAsync(userId, cancellationToken);

        var query = _db.BillingTransactions
            .AsNoTracking()
            .Include(bt => bt.SubscriptionPlan)
            .Where(bt => bt.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(bt => new BillingTransactionDto(
                bt.Id,
                bt.SubscriptionPlan.Name,
                bt.Amount,
                bt.Currency,
                bt.Type.ToString(),
                bt.Description,
                bt.StripeSessionId,
                bt.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new BillingOverviewDto
        {
            CurrentSubscription = status,
            Transactions = new PagedResult<BillingTransactionDto>(transactions, totalCount, page, pageSize, sortBy, sortDir)
        };
    }

    private PlanFeaturesDto? DeserializeFeatures(string? json, Guid? planId = null, string? planName = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<PlanFeaturesDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            // DQ-4: include which plan is corrupt — every subscriber on it silently drops to
            // null Features (GetUserStatusAsync callers then treat that as no-features/Free-like)
            // until the row is repaired, so an operator needs to know which plan to fix.
            _logger.LogError(ex, "Failed to deserialize plan features JSON for plan {PlanId} ({PlanName}).", planId, planName);
            return null;
        }
    }

    private static PlanFeaturesDto GetDefaultFreeFeatures() => new()
    {
        MaxCourses = 1,
        AssessmentAccess = false,
        CertificateAccess = false,
        PrioritySupport = false
    };
}
