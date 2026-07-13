using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class NotificationServiceTests
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    private static NotificationService NewService(ApplicationDbContext db) => new(db);

    [Fact]
    public async Task CreateAsync_PersistsWithSerializedBodyArgs()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var created = await NewService(db).CreateAsync(
            UserId, NotificationType.ChatMessage, "ChatMessageReceived", ["Alice"], "/chat");

        created.UserId.ShouldBe(UserId);
        created.Type.ShouldBe(NotificationType.ChatMessage);
        created.IsRead.ShouldBeFalse();
        created.BodyArgsJson.ShouldBe("[\"Alice\"]");
    }

    [Fact]
    public async Task CreateAsync_NoArgs_LeavesBodyArgsJsonNull()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var created = await NewService(db).CreateAsync(
            UserId, NotificationType.SubscriptionExpiring, "SubscriptionExpiring", null, null);

        created.BodyArgsJson.ShouldBeNull();
    }

    [Fact]
    public async Task GetPagedAsync_ScopesToUser_AndRoundTripsBodyArgs()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var service = NewService(db);
        await service.CreateAsync(UserId, NotificationType.ChatMessage, "ChatMessageReceived", ["Bob"], "/chat");
        await service.CreateAsync(OtherUserId, NotificationType.ChatMessage, "ChatMessageReceived", ["Eve"], "/chat");

        var page = await service.GetPagedAsync(UserId, 1, 10, "createdat", "desc");

        page.TotalCount.ShouldBe(1);
        var dto = page.Items.ShouldHaveSingleItem();
        dto.BodyArgs.ShouldBe(["Bob"]);
        dto.Type.ShouldBe("ChatMessage");
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyUnreadForThatUser()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var service = NewService(db);
        await service.CreateAsync(UserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);
        await service.CreateAsync(UserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);
        await service.CreateAsync(OtherUserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);

        (await service.GetUnreadCountAsync(UserId)).ShouldBe(2);
    }

    [Fact]
    public async Task MarkReadAsync_OwnNotification_ReturnsTrueAndFlipsIsRead()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var service = NewService(db);
        var created = await service.CreateAsync(UserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);

        var result = await service.MarkReadAsync(created.Id, UserId);

        result.ShouldBeTrue();
        (await service.GetUnreadCountAsync(UserId)).ShouldBe(0);
    }

    [Fact]
    public async Task MarkReadAsync_AlreadyRead_StillReturnsTrue()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var service = NewService(db);
        var created = await service.CreateAsync(UserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);
        await service.MarkReadAsync(created.Id, UserId);

        (await service.MarkReadAsync(created.Id, UserId)).ShouldBeTrue();
    }

    [Fact]
    public async Task MarkReadAsync_OtherUsersNotification_ReturnsFalse()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var service = NewService(db);
        var created = await service.CreateAsync(OtherUserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);

        var result = await service.MarkReadAsync(created.Id, UserId);

        result.ShouldBeFalse();
        (await service.GetUnreadCountAsync(OtherUserId)).ShouldBe(1); // untouched
    }

    [Fact]
    public async Task MarkReadAsync_NotFound_ReturnsFalse()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).MarkReadAsync(Guid.NewGuid(), UserId)).ShouldBeFalse();
    }

    [Fact]
    public async Task MarkAllReadAsync_FlipsOnlyThatUsersUnread_ReturnsCount()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var service = NewService(db);
        await service.CreateAsync(UserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);
        await service.CreateAsync(UserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);
        await service.CreateAsync(OtherUserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);

        var updated = await service.MarkAllReadAsync(UserId);

        updated.ShouldBe(2);
        (await service.GetUnreadCountAsync(UserId)).ShouldBe(0);
        (await service.GetUnreadCountAsync(OtherUserId)).ShouldBe(1);
    }

    [Fact]
    public async Task PruneOldAsync_DeletesOnlyReadNotificationsOlderThanCutoff()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var service = NewService(db);
        var oldRead = await service.CreateAsync(UserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);
        var oldUnread = await service.CreateAsync(UserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);
        var recentRead = await service.CreateAsync(UserId, NotificationType.ChatMessage, "ChatMessageReceived", null, null);
        await service.MarkReadAsync(oldRead.Id, UserId);
        await service.MarkReadAsync(recentRead.Id, UserId);

        oldRead.CreatedAt = DateTimeOffset.UtcNow.AddDays(-40);
        oldUnread.CreatedAt = DateTimeOffset.UtcNow.AddDays(-40);
        await db.SaveChangesAsync();

        var deleted = await service.PruneOldAsync(TimeSpan.FromDays(30));

        deleted.ShouldBe(1); // only oldRead: oldUnread is unread (kept), recentRead is too new (kept)
        (await db.Notifications.CountAsync()).ShouldBe(2);
    }
}
