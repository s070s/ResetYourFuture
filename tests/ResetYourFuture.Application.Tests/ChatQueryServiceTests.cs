using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class ChatQueryServiceTests
{
    private const string A = "user-a";
    private const string B = "user-b";

    private static (ChatQueryService svc, UserManager<ApplicationUser> um, ISubscriptionService subs) NewService(
        ApplicationDbContext db)
    {
        var um = IdentityMocks.MockUserManager();
        var subs = Substitute.For<ISubscriptionService>();
        return (new ChatQueryService(db, um, subs), um, subs);
    }

    private static ApplicationUser AppUser(string id, string first = "F", string last = "L", bool enabled = true) =>
        new() { Id = id, UserName = id, Email = $"{id}@x.com", FirstName = first, LastName = last, IsEnabled = enabled };

    private static UserSubscriptionStatusDto Status(bool priority) =>
        new(SubscriptionTier.Free, "n", DateTime.UtcNow, null, true, new PlanFeaturesDto { PrioritySupport = priority });

    private static ChatConversation Conversation(string creator, string participant) =>
        new() { Id = Guid.NewGuid(), CreatorId = creator, ParticipantId = participant };

    // ---- HasChatAccessAsync --------------------------------------------------

    [Fact]
    public async Task HasChatAccess_Admin_ReturnsTrue()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService(db);

        (await svc.HasChatAccessAsync(A, isAdmin: true)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasChatAccess_ProSubscriber_ReturnsTrue()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, subs) = NewService(db);
        subs.GetUserStatusAsync(A, Arg.Any<CancellationToken>()).Returns(Status(priority: true));

        (await svc.HasChatAccessAsync(A, isAdmin: false)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasChatAccess_FreeUser_ReturnsFalse()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, subs) = NewService(db);
        subs.GetUserStatusAsync(A, Arg.Any<CancellationToken>()).Returns(Status(priority: false));

        (await svc.HasChatAccessAsync(A, isAdmin: false)).ShouldBeFalse();
    }

    // ---- GetConversationsAsync ----------------------------------------------

    [Fact]
    public async Task GetConversations_MapsOtherUserAndUnreadCount()
    {
        // GetConversationsAsync runs a DB-side GroupBy the InMemory provider can't translate — use SQLite.
        await using var db = DbContextFactory.CreateSqlite();
        db.Users.AddRange(AppUser(A, "Caller", "One"), AppUser(B, "Other", "User"));
        var conv = Conversation(A, B);
        conv.LastMessageContent = "hi";
        conv.LastMessageAt = DateTime.UtcNow;
        db.ChatConversations.Add(conv);
        db.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = B, Content = "hi", IsRead = false });
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService(db);

        var result = await svc.GetConversationsAsync(A, 1, 10);

        var item = result.Items.ShouldHaveSingleItem();
        item.OtherUserId.ShouldBe(B);
        item.OtherUserName.ShouldBe("Other User");
        item.UnreadCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetConversations_None_ReturnsEmpty()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService(db);

        var result = await svc.GetConversationsAsync(A, 1, 10);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    // ---- GetMessagesAsync ----------------------------------------------------

    [Fact]
    public async Task GetMessages_MissingConversation_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService(db);

        (await svc.GetMessagesAsync(A, Guid.NewGuid(), 1, 10)).StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task GetMessages_CallerNotParticipant_ReturnsForbidden()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation("x", "y");
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService(db);

        (await svc.GetMessagesAsync(A, conv.Id, 1, 10)).StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task GetMessages_Participant_ReturnsOrderedBySentAt()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.AddRange(AppUser(A), AppUser(B));
        var conv = Conversation(A, B);
        db.ChatConversations.Add(conv);
        db.ChatMessages.AddRange(
            new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = A, Content = "first", SentAt = new DateTime(2022, 1, 1) },
            new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = B, Content = "second", SentAt = new DateTime(2022, 1, 2) });
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService(db);

        var result = await svc.GetMessagesAsync(A, conv.Id, 1, 10);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Items.Select(m => m.Content).ShouldBe(new[] { "first", "second" });
    }

    [Fact]
    public async Task GetMessages_CallEventRow_ProjectsCallEventAndDuration()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.AddRange(AppUser(A), AppUser(B));
        var conv = Conversation(A, B);
        db.ChatConversations.Add(conv);
        var connectedAt = new DateTime(2022, 1, 1, 12, 0, 0);
        var session = new CallSession
        {
            Id = Guid.NewGuid(),
            InitiatorId = A,
            ConversationId = conv.Id,
            ConnectedAt = connectedAt,
            EndedAt = connectedAt.AddSeconds(90),
            EndReason = CallEndReason.Completed
        };
        db.CallSessions.Add(session);
        db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conv.Id,
            SenderId = A,
            Content = "Video call ended · 1:30",
            SentAt = connectedAt.AddSeconds(90),
            CallSessionId = session.Id,
            CallEvent = CallEventKind.Ended
        });
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService(db);

        var result = await svc.GetMessagesAsync(A, conv.Id, 1, 10);

        var dto = result.Value!.Items.ShouldHaveSingleItem();
        dto.CallEvent.ShouldBe(CallEventKind.Ended);
        dto.CallDurationSeconds.ShouldBe(90);
    }

    [Fact]
    public async Task GetMessages_RegularMessage_HasNullCallEventAndDuration()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.AddRange(AppUser(A), AppUser(B));
        var conv = Conversation(A, B);
        db.ChatConversations.Add(conv);
        db.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = A, Content = "hi" });
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService(db);

        var result = await svc.GetMessagesAsync(A, conv.Id, 1, 10);

        var dto = result.Value!.Items.ShouldHaveSingleItem();
        dto.CallEvent.ShouldBeNull();
        dto.CallDurationSeconds.ShouldBeNull();
    }

    // ---- StartConversationAsync ---------------------------------------------

    [Fact]
    public async Task StartConversation_NoChatAccess_ReturnsForbidden()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, subs) = NewService(db);
        subs.GetUserStatusAsync(A, Arg.Any<CancellationToken>()).Returns(Status(priority: false));

        var result = await svc.StartConversationAsync(A, isAdmin: false, new StartConversationRequest(B, null));

        result.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task StartConversation_BlankTarget_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService(db);

        (await svc.StartConversationAsync(A, true, new StartConversationRequest("", null))).StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task StartConversation_WithSelf_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService(db);

        (await svc.StartConversationAsync(A, true, new StartConversationRequest(A, null))).StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task StartConversation_TargetMissing_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, um, _) = NewService(db);
        um.FindByIdAsync(B).Returns((ApplicationUser?)null);

        (await svc.StartConversationAsync(A, true, new StartConversationRequest(B, null))).StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task StartConversation_TargetDisabled_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, um, _) = NewService(db);
        um.FindByIdAsync(B).Returns(AppUser(B, enabled: false));

        (await svc.StartConversationAsync(A, true, new StartConversationRequest(B, null))).StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task StartConversation_New_PersistsConversationAndInitialMessage()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, um, _) = NewService(db);
        um.FindByIdAsync(B).Returns(AppUser(B, "Other", "User"));
        um.FindByIdAsync(A).Returns(AppUser(A, "Caller", "One"));
        um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string>());

        var result = await svc.StartConversationAsync(A, true, new StartConversationRequest(B, "hello"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.OtherUserId.ShouldBe(B);
        (await db.ChatConversations.CountAsync()).ShouldBe(1);
        (await db.ChatMessages.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task StartConversation_LongInitialMessage_IsTruncated()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, um, _) = NewService(db);
        um.FindByIdAsync(B).Returns(AppUser(B));
        um.FindByIdAsync(A).Returns(AppUser(A));
        um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string>());

        var result = await svc.StartConversationAsync(A, true, new StartConversationRequest(B, new string('x', 600)));

        result.Value!.LastMessageContent!.Length.ShouldBe(500);
        result.Value.LastMessageContent.ShouldEndWith("...");
    }

    [Fact]
    public async Task StartConversation_Existing_ReturnsExisting()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.AddRange(AppUser(A), AppUser(B));
        var existing = Conversation(A, B); // already normalized (A < B)
        db.ChatConversations.Add(existing);
        await db.SaveChangesAsync();
        var (svc, um, _) = NewService(db);
        um.FindByIdAsync(B).Returns(AppUser(B));
        um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string>());

        var result = await svc.StartConversationAsync(A, true, new StartConversationRequest(B, null));

        result.Value!.Id.ShouldBe(existing.Id);
        (await db.ChatConversations.CountAsync()).ShouldBe(1);
    }

    // ---- GetAvailableUsersAsync ---------------------------------------------

    [Fact]
    public async Task GetAvailableUsers_ExcludesSelfDisabledAndExistingPartners()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.AddRange(
            AppUser(A, "Caller", "One"),
            AppUser(B, "Available", "User"),
            AppUser("user-c", "Disabled", "User", enabled: false),
            AppUser("user-d", "Partner", "User"));
        db.ChatConversations.Add(Conversation(A, "user-d"));
        await db.SaveChangesAsync();
        var (svc, um, _) = NewService(db);
        um.Users.Returns(db.Users);

        var result = await svc.GetAvailableUsersAsync(A, search: null);

        result.Select(u => u.Id).ShouldBe(new[] { B });
    }

    // ---- DeleteConversationAsync --------------------------------------------

    [Fact]
    public async Task DeleteConversation_Missing_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService(db);

        (await svc.DeleteConversationAsync(A, Guid.NewGuid())).StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task DeleteConversation_NotParticipant_ReturnsForbidden()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation("x", "y");
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService(db);

        (await svc.DeleteConversationAsync(A, conv.Id)).StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task DeleteConversation_Participant_Removes()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation(A, B);
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService(db);

        (await svc.DeleteConversationAsync(A, conv.Id)).IsSuccess.ShouldBeTrue();
        (await db.ChatConversations.CountAsync()).ShouldBe(0);
    }

    // ---- GetUnreadCountAsync -------------------------------------------------

    [Fact]
    public async Task GetUnreadCount_CountsOthersUnreadMessages()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation(A, B);
        db.ChatConversations.Add(conv);
        db.ChatMessages.AddRange(
            new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = B, Content = "1", IsRead = false },
            new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = B, Content = "2", IsRead = false },
            new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = A, Content = "mine", IsRead = false });
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService(db);

        (await svc.GetUnreadCountAsync(A)).ShouldBe(2);
    }

    [Fact]
    public async Task GetUnreadCount_NoMessages_ReturnsZero()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService(db);

        (await svc.GetUnreadCountAsync(A)).ShouldBe(0);
    }
}
