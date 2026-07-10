using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class CallQueryServiceTests
{
    private const string A = "user-a";
    private const string B = "user-b";

    private static (CallQueryService svc, UserManager<ApplicationUser> um) NewService(
        ApplicationDbContext db)
    {
        var um = IdentityMocks.MockUserManager();
        return (new CallQueryService(db, um), um);
    }

    private static ApplicationUser AppUser(string id, string first = "F", string last = "L", bool enabled = true) =>
        new() { Id = id, UserName = id, Email = $"{id}@x.com", FirstName = first, LastName = last, IsEnabled = enabled };

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
        var (svc, um) = NewService(db);
        um.Users.Returns(db.Users);

        var result = await svc.GetCallableUsersAsync(A, search: null);

        // Unlike chat's GetAvailableUsersAsync, an existing conversation partner (user-d) IS included.
        result.Select(u => u.Id).ShouldBe(new[] { B, "user-d" }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetCallableUsers_NoUsers_ReturnsEmpty()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, um) = NewService(db);
        um.Users.Returns(db.Users);

        var result = await svc.GetCallableUsersAsync(A, search: null);

        result.ShouldBeEmpty();
    }
}
