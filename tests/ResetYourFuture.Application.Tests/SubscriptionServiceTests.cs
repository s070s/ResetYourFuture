using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Web.ApiServices;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Domain.Enums;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class SubscriptionServiceTests
{
    private const string UserId = "user-1";

    private static SubscriptionService NewService( ApplicationDbContext db, bool mockPayment = true )
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection( new Dictionary<string, string?>
            {
                ["Payment:MockEnabled"] = mockPayment ? "true" : "false"
            } )
            .Build();

        return new SubscriptionService(
            db, NullLogger<SubscriptionService>.Instance, new MemoryCache( new MemoryCacheOptions() ), config );
    }

    private static SubscriptionPlan Plan(
        string name, SubscriptionTierEnum tier, decimal price,
        BillingPeriod period = BillingPeriod.Monthly, bool active = true, string? featuresJson = null ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Tier = tier,
            Price = price,
            BillingPeriod = period,
            IsActive = active,
            FeaturesJson = featuresJson
        };

    private static UserSubscription ActiveSub( SubscriptionPlan plan ) =>
        new() { Id = Guid.NewGuid(), UserId = UserId, SubscriptionPlanId = plan.Id, IsActive = true };

    // ---- GetPlansAsync -------------------------------------------------------

    [Fact]
    public async Task GetPlans_ReturnsActiveOnly_OrderedByTierThenPrice()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.SubscriptionPlans.AddRange(
            Plan( "PlusDear", SubscriptionTierEnum.Plus, 5m ),
            Plan( "Free", SubscriptionTierEnum.Free, 0m ),
            Plan( "PlusCheap", SubscriptionTierEnum.Plus, 3m ),
            Plan( "InactivePro", SubscriptionTierEnum.Pro, 9m, active: false ) );
        await db.SaveChangesAsync();

        var plans = await NewService( db ).GetPlansAsync();

        plans.Select( p => p.Name ).ShouldBe( new[] { "Free", "PlusCheap", "PlusDear" } );
    }

    [Fact]
    public async Task GetPlans_DeserializesFeaturesJson()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.SubscriptionPlans.Add( Plan( "Plus", SubscriptionTierEnum.Plus, 5m,
            featuresJson: """{"MaxCourses":3,"AssessmentAccess":true,"CertificateAccess":true,"PrioritySupport":false}""" ) );
        await db.SaveChangesAsync();

        var plan = ( await NewService( db ).GetPlansAsync() ).Single();

        plan.Features!.MaxCourses.ShouldBe( 3 );
        plan.Features.AssessmentAccess.ShouldBeTrue();
        plan.Features.PrioritySupport.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPlans_MalformedFeaturesJson_ReturnsNullFeatures()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.SubscriptionPlans.Add( Plan( "Plus", SubscriptionTierEnum.Plus, 5m, featuresJson: "not-json" ) );
        await db.SaveChangesAsync();

        ( await NewService( db ).GetPlansAsync() ).Single().Features.ShouldBeNull();
    }

    // ---- GetUserStatusAsync --------------------------------------------------

    [Fact]
    public async Task GetUserStatus_NoSubscription_ReturnsFreeDefaults()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var status = await NewService( db ).GetUserStatusAsync( UserId );

        status.Tier.ShouldBe( SubscriptionTierEnum.Free );
        status.PlanName.ShouldBe( "Free" );
        status.Features!.MaxCourses.ShouldBe( 1 );
        status.Features.AssessmentAccess.ShouldBeFalse();
    }

    [Fact]
    public async Task GetUserStatus_ActiveSubscription_MapsPlan()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var plan = Plan( "Pro", SubscriptionTierEnum.Pro, 20m );
        db.SubscriptionPlans.Add( plan );
        db.UserSubscriptions.Add( ActiveSub( plan ) );
        await db.SaveChangesAsync();

        var status = await NewService( db ).GetUserStatusAsync( UserId );

        status.Tier.ShouldBe( SubscriptionTierEnum.Pro );
        status.PlanName.ShouldBe( "Pro" );
    }

    [Fact]
    public async Task GetUserStatus_SecondCall_ServedFromCache()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var plan = Plan( "Pro", SubscriptionTierEnum.Pro, 20m );
        var sub = ActiveSub( plan );
        db.SubscriptionPlans.Add( plan );
        db.UserSubscriptions.Add( sub );
        await db.SaveChangesAsync();
        var svc = NewService( db );

        var first = await svc.GetUserStatusAsync( UserId );
        // Mutate the underlying row WITHOUT going through a cache-evicting method.
        sub.IsActive = false;
        await db.SaveChangesAsync();
        var second = await svc.GetUserStatusAsync( UserId );

        second.Tier.ShouldBe( first.Tier );          // still Pro from cache
        second.Tier.ShouldBe( SubscriptionTierEnum.Pro );
    }

    // ---- GetUserTierAsync ----------------------------------------------------

    [Fact]
    public async Task GetUserTier_NoSubscription_ReturnsFree()
    {
        await using var db = DbContextFactory.CreateInMemory();

        ( await NewService( db ).GetUserTierAsync( UserId ) ).ShouldBe( SubscriptionTierEnum.Free );
    }

    [Fact]
    public async Task GetUserTier_ActiveSubscription_ReturnsTier()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var plan = Plan( "Plus", SubscriptionTierEnum.Plus, 5m );
        db.SubscriptionPlans.Add( plan );
        db.UserSubscriptions.Add( ActiveSub( plan ) );
        await db.SaveChangesAsync();

        ( await NewService( db ).GetUserTierAsync( UserId ) ).ShouldBe( SubscriptionTierEnum.Plus );
    }

    // ---- CreateCheckoutSessionAsync -----------------------------------------

    [Fact]
    public async Task CreateCheckout_PlanMissing_ReturnsError()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var result = await NewService( db ).CreateCheckoutSessionAsync( UserId, Guid.NewGuid() );

        result.Status.ShouldBe( "error: plan not found" );
    }

    [Fact]
    public async Task CreateCheckout_MockDisabled_ReturnsPendingWithoutPersisting()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var plan = Plan( "Pro", SubscriptionTierEnum.Pro, 20m );
        db.SubscriptionPlans.Add( plan );
        await db.SaveChangesAsync();

        var result = await NewService( db, mockPayment: false ).CreateCheckoutSessionAsync( UserId, plan.Id );

        result.Status.ShouldBe( "pending_payment" );
        ( await db.UserSubscriptions.CountAsync() ).ShouldBe( 0 );
        ( await db.BillingTransactions.CountAsync() ).ShouldBe( 0 );
    }

    [Fact]
    public async Task CreateCheckout_NoCurrentPlan_RecordsPurchase()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var plan = Plan( "Pro", SubscriptionTierEnum.Pro, 20m );
        db.SubscriptionPlans.Add( plan );
        await db.SaveChangesAsync();

        var result = await NewService( db ).CreateCheckoutSessionAsync( UserId, plan.Id );

        result.Status.ShouldBe( "complete" );
        result.CheckoutUrl.ShouldNotBeNull();
        ( await db.BillingTransactions.SingleAsync() ).Type.ShouldBe( BillingTransactionType.Purchase );
    }

    [Theory]
    [InlineData( SubscriptionTierEnum.Plus, SubscriptionTierEnum.Pro, BillingTransactionType.Upgrade )]
    [InlineData( SubscriptionTierEnum.Pro, SubscriptionTierEnum.Plus, BillingTransactionType.Downgrade )]
    public async Task CreateCheckout_TierChange_RecordsUpgradeOrDowngrade(
        SubscriptionTierEnum current, SubscriptionTierEnum target, BillingTransactionType expected )
    {
        await using var db = DbContextFactory.CreateInMemory();
        var currentPlan = Plan( "Current", current, 5m );
        var targetPlan = Plan( "Target", target, 10m );
        db.SubscriptionPlans.AddRange( currentPlan, targetPlan );
        db.UserSubscriptions.Add( ActiveSub( currentPlan ) );
        await db.SaveChangesAsync();

        await NewService( db ).CreateCheckoutSessionAsync( UserId, targetPlan.Id );

        ( await db.BillingTransactions.SingleAsync() ).Type.ShouldBe( expected );
    }

    [Fact]
    public async Task CreateCheckout_SamePlan_RecordsRenewal()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var plan = Plan( "Pro", SubscriptionTierEnum.Pro, 20m );
        db.SubscriptionPlans.Add( plan );
        db.UserSubscriptions.Add( ActiveSub( plan ) );
        await db.SaveChangesAsync();

        await NewService( db ).CreateCheckoutSessionAsync( UserId, plan.Id );

        ( await db.BillingTransactions.SingleAsync() ).Type.ShouldBe( BillingTransactionType.Renewal );
    }

    [Fact]
    public async Task CreateCheckout_SameTierDifferentPlan_RecordsPlanSwitch()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var monthly = Plan( "Plus Monthly", SubscriptionTierEnum.Plus, 5m, BillingPeriod.Monthly );
        var yearly = Plan( "Plus Yearly", SubscriptionTierEnum.Plus, 50m, BillingPeriod.Yearly );
        db.SubscriptionPlans.AddRange( monthly, yearly );
        db.UserSubscriptions.Add( ActiveSub( monthly ) );
        await db.SaveChangesAsync();

        await NewService( db ).CreateCheckoutSessionAsync( UserId, yearly.Id );

        ( await db.BillingTransactions.SingleAsync() ).Type.ShouldBe( BillingTransactionType.PlanSwitch );
    }

    // ---- AssignPlanAsync -----------------------------------------------------

    [Fact]
    public async Task AssignPlan_StagesWithoutPersisting()
    {
        const string dbName = "assign-no-save";
        var plan = Plan( "Pro", SubscriptionTierEnum.Pro, 20m );
        var existing = Plan( "Plus", SubscriptionTierEnum.Plus, 5m );
        await using ( var seed = DbContextFactory.CreateInMemory( dbName ) )
        {
            seed.SubscriptionPlans.AddRange( plan, existing );
            seed.UserSubscriptions.Add( ActiveSub( existing ) );
            await seed.SaveChangesAsync();
        }

        await using ( var work = DbContextFactory.CreateInMemory( dbName ) )
        {
            await NewService( work ).AssignPlanAsync( UserId, plan.Id );
            // intentionally no SaveChanges by the SUT
        }

        await using var verify = DbContextFactory.CreateInMemory( dbName );
        var subs = await verify.UserSubscriptions.ToListAsync();
        subs.Count.ShouldBe( 1 );                 // new sub was never persisted
        subs.Single().IsActive.ShouldBeTrue();    // existing deactivation was never persisted
    }

    [Fact]
    public async Task AssignPlan_UnknownPlan_Throws()
    {
        await using var db = DbContextFactory.CreateInMemory();

        await Should.ThrowAsync<InvalidOperationException>(
            () => NewService( db ).AssignPlanAsync( UserId, Guid.NewGuid() ) );
    }

    [Theory]
    [InlineData( BillingPeriod.Lifetime, false )]
    [InlineData( BillingPeriod.Monthly, true )]
    [InlineData( BillingPeriod.Quarterly, true )]
    [InlineData( BillingPeriod.Yearly, true )]
    public async Task AssignPlan_SetsExpiryPerBillingPeriod( BillingPeriod period, bool expectExpiry )
    {
        await using var db = DbContextFactory.CreateInMemory();
        var plan = Plan( "P", SubscriptionTierEnum.Pro, 1m, period );
        db.SubscriptionPlans.Add( plan );
        await db.SaveChangesAsync();

        await NewService( db ).AssignPlanAsync( UserId, plan.Id );
        await db.SaveChangesAsync();

        var sub = await db.UserSubscriptions.SingleAsync( s => s.IsActive );
        sub.ExpiresAt.HasValue.ShouldBe( expectExpiry );
    }

    // ---- AssignFreePlanAsync -------------------------------------------------

    [Fact]
    public async Task AssignFreePlan_AssignsAndRecordsTransaction()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var free = Plan( "Free", SubscriptionTierEnum.Free, 0m );
        db.SubscriptionPlans.Add( free );
        await db.SaveChangesAsync();

        await NewService( db ).AssignFreePlanAsync( UserId );

        ( await db.UserSubscriptions.CountAsync( s => s.IsActive && s.SubscriptionPlanId == free.Id ) ).ShouldBe( 1 );
        ( await db.BillingTransactions.SingleAsync() ).Type.ShouldBe( BillingTransactionType.FreePlanAssignment );
    }

    [Fact]
    public async Task AssignFreePlan_NoFreePlan_IsNoOp()
    {
        await using var db = DbContextFactory.CreateInMemory();

        await NewService( db ).AssignFreePlanAsync( UserId );

        ( await db.UserSubscriptions.CountAsync() ).ShouldBe( 0 );
    }

    // ---- CancelSubscriptionAsync --------------------------------------------

    [Fact]
    public async Task Cancel_NoActiveSubscription_ReturnsFailure()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var result = await NewService( db ).CancelSubscriptionAsync( UserId );

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancel_AlreadyFree_ReturnsFailure()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var free = Plan( "Free", SubscriptionTierEnum.Free, 0m );
        db.SubscriptionPlans.Add( free );
        db.UserSubscriptions.Add( ActiveSub( free ) );
        await db.SaveChangesAsync();

        ( await NewService( db ).CancelSubscriptionAsync( UserId ) ).Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancel_NoFreePlanAvailable_ReturnsFailure()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var pro = Plan( "Pro", SubscriptionTierEnum.Pro, 20m );
        db.SubscriptionPlans.Add( pro );
        db.UserSubscriptions.Add( ActiveSub( pro ) );
        await db.SaveChangesAsync();

        ( await NewService( db ).CancelSubscriptionAsync( UserId ) ).Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancel_ActivePaidPlan_DowngradesToFree()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var pro = Plan( "Pro", SubscriptionTierEnum.Pro, 20m );
        var free = Plan( "Free", SubscriptionTierEnum.Free, 0m );
        db.SubscriptionPlans.AddRange( pro, free );
        db.UserSubscriptions.Add( ActiveSub( pro ) );
        await db.SaveChangesAsync();

        var result = await NewService( db ).CancelSubscriptionAsync( UserId );

        result.Success.ShouldBeTrue();
        var active = await db.UserSubscriptions.SingleAsync( s => s.IsActive );
        active.SubscriptionPlanId.ShouldBe( free.Id );
        ( await db.BillingTransactions.SingleAsync() ).Type.ShouldBe( BillingTransactionType.Downgrade );
    }

    // ---- GetBillingOverviewAsync --------------------------------------------

    [Fact]
    public async Task GetBillingOverview_OrdersTransactionsNewestFirst()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var plan = Plan( "Pro", SubscriptionTierEnum.Pro, 20m );
        db.SubscriptionPlans.Add( plan );
        db.BillingTransactions.AddRange(
            Txn( plan, "2020", new DateTime( 2020, 1, 1 ) ),
            Txn( plan, "2022", new DateTime( 2022, 1, 1 ) ),
            Txn( plan, "2021", new DateTime( 2021, 1, 1 ) ) );
        await db.SaveChangesAsync();

        var overview = await NewService( db ).GetBillingOverviewAsync( UserId );

        overview.Transactions.Items.Select( t => t.Description ).ShouldBe( new[] { "2022", "2021", "2020" } );
        overview.CurrentSubscription.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetBillingOverview_ClampsPagingArguments()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var overview = await NewService( db ).GetBillingOverviewAsync( UserId, page: 0, pageSize: 999 );

        overview.Transactions.Page.ShouldBe( 1 );
        overview.Transactions.PageSize.ShouldBe( 10 );
    }

    private static BillingTransaction Txn( SubscriptionPlan plan, string description, DateTime createdAt ) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SubscriptionPlanId = plan.Id,
            Amount = plan.Price,
            Currency = "EUR",
            Type = BillingTransactionType.Purchase,
            Description = description,
            CreatedAt = createdAt
        };
}
