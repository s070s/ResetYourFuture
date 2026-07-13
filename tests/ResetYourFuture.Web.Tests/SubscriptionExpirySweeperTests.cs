using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Web.Services;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class SubscriptionExpirySweeperTests
{
    private const string UserId = "user-1";

    private static SubscriptionPlan Plan(string name, SubscriptionTier tier) =>
        new() { Id = Guid.NewGuid(), Name = name, Tier = tier, Price = 0m, IsActive = true };

    private static SubscriptionExpirySweeper Build(ApplicationDbContext db, INotificationDispatcher? notifications = null)
    {
        var services = new ServiceCollection();
        // Singleton (not Scoped): the sweeper creates+disposes its own scope internally, and a
        // Scoped registration of an externally-owned, shared `db` would get disposed along with
        // that scope — breaking assertions the test makes against `db` afterward.
        services.AddSingleton<IApplicationDbContext>(_ => db);
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton(_ => notifications ?? Substitute.For<INotificationDispatcher>());
        var provider = services.BuildServiceProvider();

        return new SubscriptionExpirySweeper(provider, NullLogger<SubscriptionExpirySweeper>.Instance);
    }

    // SweepExpiredSubscriptionsAsync is private on the sealed BackgroundService; invoke it
    // directly to test the sweep logic without waiting out the real poll interval.
    private static Task RunSweep(SubscriptionExpirySweeper sweeper, CancellationToken ct)
    {
        var method = typeof(SubscriptionExpirySweeper).GetMethod(
            "SweepExpiredSubscriptionsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(sweeper, [ct])!;
    }

    [Fact]
    public async Task Sweep_ExpiredSubscription_RevertsToFreeAndRecordsExpiredTransaction()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var pro = Plan("Pro", SubscriptionTier.Pro);
        var free = Plan("Free", SubscriptionTier.Free);
        db.SubscriptionPlans.AddRange(pro, free);
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SubscriptionPlanId = pro.Id,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        await RunSweep(Build(db), CancellationToken.None);

        var active = await db.UserSubscriptions.SingleAsync(s => s.IsActive);
        active.SubscriptionPlanId.ShouldBe(free.Id);
        (await db.UserSubscriptions.CountAsync(s => s.SubscriptionPlanId == pro.Id && !s.IsActive)).ShouldBe(1);
        var txn = await db.BillingTransactions.SingleAsync();
        txn.Type.ShouldBe(BillingTransactionType.Expired);
        txn.UserId.ShouldBe(UserId);
    }

    [Fact]
    public async Task Sweep_NoExpiredSubscriptions_IsNoOp()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var pro = Plan("Pro", SubscriptionTier.Pro);
        db.SubscriptionPlans.Add(pro);
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SubscriptionPlanId = pro.Id,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        await RunSweep(Build(db), CancellationToken.None);

        (await db.UserSubscriptions.SingleAsync()).SubscriptionPlanId.ShouldBe(pro.Id);
        (await db.BillingTransactions.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Sweep_LifetimeSubscription_NeverExpires()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var pro = Plan("Pro Lifetime", SubscriptionTier.Pro);
        db.SubscriptionPlans.Add(pro);
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SubscriptionPlanId = pro.Id,
            IsActive = true,
            ExpiresAt = null
        });
        await db.SaveChangesAsync();

        await RunSweep(Build(db), CancellationToken.None);

        (await db.UserSubscriptions.SingleAsync()).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Sweep_NoFreePlanConfigured_LeavesExpiredSubscriptionUntouched()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var pro = Plan("Pro", SubscriptionTier.Pro);
        db.SubscriptionPlans.Add(pro);
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SubscriptionPlanId = pro.Id,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        await RunSweep(Build(db), CancellationToken.None);

        // No Free plan exists to revert to — the sweeper logs and retries next cycle rather
        // than leaving the user in a planless state.
        (await db.UserSubscriptions.SingleAsync()).IsActive.ShouldBeTrue();
    }
}
