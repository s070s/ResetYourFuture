using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class CallQueryServiceTests
{
    private const string A = "user-a";
    private const string B = "user-b";

    private static (CallQueryService svc, UserManager<ApplicationUser> um, ISubscriptionService subs) NewService(
        ApplicationDbContext db)
    {
        var um = IdentityMocks.MockUserManager();
        var subs = Substitute.For<ISubscriptionService>();
        return (new CallQueryService(db, um, subs), um, subs);
    }

    private static ApplicationUser AppUser(string id, string first = "F", string last = "L", bool enabled = true) =>
        new() { Id = id, UserName = id, Email = $"{id}@x.com", FirstName = first, LastName = last, IsEnabled = enabled };

    private static UserSubscriptionStatusDto Status(bool priority) =>
        new(SubscriptionTier.Free, "n", DateTime.UtcNow, null, true, new PlanFeaturesDto { PrioritySupport = priority });

    // ---- HasCallAccessAsync --------------------------------------------------

    [Fact]
    public async Task HasCallAccess_Admin_ReturnsTrue()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService(db);

        (await svc.HasCallAccessAsync(A, isAdmin: true)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasCallAccess_ProSubscriber_ReturnsTrue()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, subs) = NewService(db);
        subs.GetUserStatusAsync(A, Arg.Any<CancellationToken>()).Returns(Status(priority: true));

        (await svc.HasCallAccessAsync(A, isAdmin: false)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasCallAccess_FreeUser_ReturnsFalse()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, subs) = NewService(db);
        subs.GetUserStatusAsync(A, Arg.Any<CancellationToken>()).Returns(Status(priority: false));

        (await svc.HasCallAccessAsync(A, isAdmin: false)).ShouldBeFalse();
    }

    // ---- GetCallableUsersAsync -------------------------------------------------

    [Fact]
    public async Task GetCallableUsers_ExcludesSelfAndDisabled_ButIncludesExistingChatPartners()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.AddRange(
            AppUser(A, "Caller", "One"),
            AppUser(B, "Available", "User"),
            AppUser("user-c", "Disabled", "User", enabled: false),
            AppUser("user-d", "Partner", "User"));
        db.ChatConversations.Add(new ChatConversation { Id = Guid.NewGuid(), CreatorId = A, ParticipantId = "user-d" });
        await db.SaveChangesAsync();
        var (svc, um, _) = NewService(db);
        um.Users.Returns(db.Users);

        var result = await svc.GetCallableUsersAsync(A, search: null);

        // Unlike chat's GetAvailableUsersAsync, an existing conversation partner (user-d) IS included.
        result.Select(u => u.Id).ShouldBe(new[] { B, "user-d" }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetCallableUsers_NoUsers_ReturnsEmpty()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, um, _) = NewService(db);
        um.Users.Returns(db.Users);

        var result = await svc.GetCallableUsersAsync(A, search: null);

        result.ShouldBeEmpty();
    }
}
