using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Api.Domain.Enums;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Text.Json;

namespace ResetYourFuture.Api.Services;

/// <summary>
/// Subscription management service with stub Stripe integration.
/// All Stripe operations are mocked for test/development mode.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService( ApplicationDbContext db , ILogger<SubscriptionService> logger )
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<SubscriptionPlanDto>> GetPlansAsync( CancellationToken cancellationToken = default )
    {
        var plans = await _db.SubscriptionPlans
            .Where( sp => sp.IsActive )
            .OrderBy( sp => sp.Tier )
            .ThenBy( sp => sp.Price )
            .Select( sp => new SubscriptionPlanDto(
                sp.Id ,
                sp.Name ,
                sp.Description ,
                sp.Price ,
                sp.BillingPeriod.ToString() ,
                sp.Tier ,
                DeserializeFeatures( sp.FeaturesJson ) ,
                sp.IsActive
            ) )
            .ToListAsync( cancellationToken );

        return plans;
    }

    public async Task<UserSubscriptionStatusDto> GetUserStatusAsync(
        string userId , CancellationToken cancellationToken = default )
    {
        var activeSub = await _db.UserSubscriptions
            .Include( us => us.SubscriptionPlan )
            .Where( us => us.UserId == userId && us.IsActive )
            .FirstOrDefaultAsync( cancellationToken );

        if ( activeSub is null )
        {
            return new UserSubscriptionStatusDto(
                SubscriptionTierEnum.Free ,
                "Free" ,
                DateTime.UtcNow ,
                null ,
                true ,
                GetDefaultFreeFeatures()
            );
        }

        return new UserSubscriptionStatusDto(
            activeSub.SubscriptionPlan.Tier ,
            activeSub.SubscriptionPlan.Name ,
            activeSub.StartedAt ,
            activeSub.ExpiresAt ,
            activeSub.IsActive ,
            DeserializeFeatures( activeSub.SubscriptionPlan.FeaturesJson )
        );
    }

    public async Task<SubscriptionTierEnum> GetUserTierAsync(
        string userId , CancellationToken cancellationToken = default )
    {
        var tier = await _db.UserSubscriptions
            .Include( us => us.SubscriptionPlan )
            .Where( us => us.UserId == userId && us.IsActive )
            .Select( us => ( SubscriptionTierEnum? ) us.SubscriptionPlan.Tier )
            .FirstOrDefaultAsync( cancellationToken );

        return tier ?? SubscriptionTierEnum.Free;
    }

    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(
        string userId , Guid planId , CancellationToken cancellationToken = default )
    {
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync( sp => sp.Id == planId && sp.IsActive , cancellationToken );

        if ( plan is null )
        {
            return new CheckoutSessionDto(
                string.Empty ,
                null ,
                "error: plan not found"
            );
        }

        // Determine transaction type based on current tier
        var currentTier = await GetUserTierAsync( userId , cancellationToken );
        var transactionType = plan.Tier > currentTier
            ? BillingTransactionType.Upgrade
            : plan.Tier < currentTier
                ? BillingTransactionType.Downgrade
                : BillingTransactionType.Purchase;

        // --- STUB: Mock Stripe checkout session ---
        // In production, this would create a real Stripe Checkout Session.
        var mockSessionId = $"cs_test_{Guid.NewGuid():N}";

        _logger.LogInformation(
            "Mock Stripe checkout session created: {SessionId} for user {UserId}, plan {PlanName}" ,
            mockSessionId , userId , plan.Name );

        // Auto-complete the subscription (simulating successful payment)
        await AssignPlanAsync( userId , planId , cancellationToken );

        // Record the billing transaction
        _db.BillingTransactions.Add( new BillingTransaction
        {
            Id = Guid.NewGuid() ,
            UserId = userId ,
            SubscriptionPlanId = planId ,
            Amount = plan.Price ,
            Currency = "EUR" ,
            Type = transactionType ,
            Description = $"{transactionType} to {plan.Name}" ,
            StripeSessionId = mockSessionId ,
            CreatedAt = DateTime.UtcNow
        } );
        await _db.SaveChangesAsync( cancellationToken );

        return new CheckoutSessionDto(
            mockSessionId ,
            $"/subscription/success?session_id={mockSessionId}" ,
            "complete"
        );
    }

    public async Task AssignPlanAsync(
        string userId , Guid planId , CancellationToken cancellationToken = default )
    {
        // Deactivate any existing active subscription
        var existingActive = await _db.UserSubscriptions
            .Where( us => us.UserId == userId && us.IsActive )
            .ToListAsync( cancellationToken );

        foreach ( var sub in existingActive )
        {
            sub.IsActive = false;
            sub.CancelledAt = DateTime.UtcNow;
        }

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync( sp => sp.Id == planId , cancellationToken )
            ?? throw new InvalidOperationException( $"Plan {planId} not found" );

        var expiresAt = plan.BillingPeriod switch
        {
            BillingPeriod.Monthly => DateTime.UtcNow.AddMonths( 1 ),
            BillingPeriod.Quarterly => DateTime.UtcNow.AddMonths( 3 ),
            BillingPeriod.Yearly => DateTime.UtcNow.AddYears( 1 ),
            BillingPeriod.Lifetime => ( DateTime? ) null,
            _ => DateTime.UtcNow.AddMonths( 1 )
        };

        var newSub = new UserSubscription
        {
            Id = Guid.NewGuid() ,
            UserId = userId ,
            SubscriptionPlanId = planId ,
            StartedAt = DateTime.UtcNow ,
            ExpiresAt = expiresAt ,
            IsActive = true
        };

        _db.UserSubscriptions.Add( newSub );
        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation(
            "Assigned plan {PlanName} (Tier: {Tier}) to user {UserId}" ,
            plan.Name , plan.Tier , userId );
    }

    public async Task AssignFreePlanAsync(
        string userId , CancellationToken cancellationToken = default )
    {
        var freePlan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync( sp => sp.Tier == SubscriptionTierEnum.Free && sp.IsActive , cancellationToken );

        if ( freePlan is null )
        {
            _logger.LogWarning( "Free plan not found in database. Skipping assignment for user {UserId}." , userId );
            return;
        }

        await AssignPlanAsync( userId , freePlan.Id , cancellationToken );

        // Record initial free plan assignment
        _db.BillingTransactions.Add( new BillingTransaction
        {
            Id = Guid.NewGuid() ,
            UserId = userId ,
            SubscriptionPlanId = freePlan.Id ,
            Amount = 0m ,
            Currency = "EUR" ,
            Type = BillingTransactionType.FreePlanAssignment ,
            Description = "Free plan assigned on registration" ,
            CreatedAt = DateTime.UtcNow
        } );
        await _db.SaveChangesAsync( cancellationToken );
    }

    public async Task<CancelSubscriptionResultDto> CancelSubscriptionAsync(
        string userId , CancellationToken cancellationToken = default )
    {
        var activeSub = await _db.UserSubscriptions
            .Include( us => us.SubscriptionPlan )
            .Where( us => us.UserId == userId && us.IsActive )
            .FirstOrDefaultAsync( cancellationToken );

        if ( activeSub is null || activeSub.SubscriptionPlan.Tier == SubscriptionTierEnum.Free )
        {
            return new CancelSubscriptionResultDto( false , "You are already on the Free plan." );
        }

        var previousPlanName = activeSub.SubscriptionPlan.Name;

        // Deactivate current subscription
        activeSub.IsActive = false;
        activeSub.CancelledAt = DateTime.UtcNow;

        // Assign Free plan
        var freePlan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync( sp => sp.Tier == SubscriptionTierEnum.Free && sp.IsActive , cancellationToken );

        if ( freePlan is null )
        {
            return new CancelSubscriptionResultDto( false , "Free plan not available. Please contact support." );
        }

        var newSub = new UserSubscription
        {
            Id = Guid.NewGuid() ,
            UserId = userId ,
            SubscriptionPlanId = freePlan.Id ,
            StartedAt = DateTime.UtcNow ,
            ExpiresAt = null ,
            IsActive = true
        };

        _db.UserSubscriptions.Add( newSub );

        // Record the downgrade transaction
        _db.BillingTransactions.Add( new BillingTransaction
        {
            Id = Guid.NewGuid() ,
            UserId = userId ,
            SubscriptionPlanId = freePlan.Id ,
            Amount = 0m ,
            Currency = "EUR" ,
            Type = BillingTransactionType.Downgrade ,
            Description = $"Cancelled {previousPlanName} — downgraded to Free" ,
            CreatedAt = DateTime.UtcNow
        } );

        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation(
            "User {UserId} cancelled {PreviousPlan} and downgraded to Free." ,
            userId , previousPlanName );

        return new CancelSubscriptionResultDto( true , $"Your {previousPlanName} plan has been cancelled. You are now on the Free plan." );
    }

    public async Task<BillingOverviewDto> GetBillingOverviewAsync(
        string userId , CancellationToken cancellationToken = default )
    {
        var status = await GetUserStatusAsync( userId , cancellationToken );

        var transactions = await _db.BillingTransactions
            .Include( bt => bt.SubscriptionPlan )
            .Where( bt => bt.UserId == userId )
            .OrderByDescending( bt => bt.CreatedAt )
            .Select( bt => new BillingTransactionDto(
                bt.Id ,
                bt.SubscriptionPlan.Name ,
                bt.Amount ,
                bt.Currency ,
                bt.Type.ToString() ,
                bt.Description ,
                bt.StripeSessionId ,
                bt.CreatedAt
            ) )
            .ToListAsync( cancellationToken );

        return new BillingOverviewDto
        {
            CurrentSubscription = status ,
            Transactions = transactions
        };
    }

    private static PlanFeaturesDto? DeserializeFeatures( string? json )
    {
        if ( string.IsNullOrWhiteSpace( json ) )
            return null;
        try
        {
            return JsonSerializer.Deserialize<PlanFeaturesDto>( json , new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            } );
        }
        catch
        {
            return null;
        }
    }

    private static PlanFeaturesDto GetDefaultFreeFeatures() => new()
    {
        MaxCourses = 1 ,
        AssessmentAccess = false ,
        CertificateAccess = false ,
        PrioritySupport = false
    };
}
