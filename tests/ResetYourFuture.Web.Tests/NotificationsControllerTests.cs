using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Enums;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class NotificationsControllerTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public NotificationsControllerTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task<Domain.Entities.Notification> SeedNotificationAsync(string userId, bool isRead = false)
    {
        using var scope = _factory.Services.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var created = await notifications.CreateAsync(userId, NotificationType.ChatMessage, "ChatMessageReceived", ["Alice"], "/chat");
        if (isRead)
            await notifications.MarkReadAsync(created.Id, userId);
        return created;
    }

    [Fact]
    public async Task GetNotifications_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/notifications")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNotifications_ReturnsOnlyCallersOwn()
    {
        var (client, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var (_, otherUserId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        await SeedNotificationAsync(userId);
        await SeedNotificationAsync(otherUserId);

        var page = await client.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/notifications");

        page.ShouldNotBeNull();
        page!.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetUnreadCount_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/notifications/unread-count")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUnreadCount_CountsOnlyCallersUnread()
    {
        var (client, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        await SeedNotificationAsync(userId, isRead: false);
        await SeedNotificationAsync(userId, isRead: true);

        var summary = await client.GetFromJsonAsync<NotificationSummaryDto>("/api/notifications/unread-count");

        summary.ShouldNotBeNull();
        summary!.UnreadCount.ShouldBe(1);
    }

    [Fact]
    public async Task MarkRead_OwnNotification_Returns204()
    {
        var (client, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var notification = await SeedNotificationAsync(userId);

        var response = await client.PostAsync($"/api/notifications/{notification.Id}/read", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MarkRead_OtherUsersNotification_Returns404()
    {
        var (client, _) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var (_, otherUserId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var notification = await SeedNotificationAsync(otherUserId);

        var response = await client.PostAsync($"/api/notifications/{notification.Id}/read", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkRead_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.PostAsync($"/api/notifications/{Guid.NewGuid()}/read", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkAllRead_FlipsOnlyCallersUnread()
    {
        var (client, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var (_, otherUserId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        await SeedNotificationAsync(userId);
        await SeedNotificationAsync(userId);
        await SeedNotificationAsync(otherUserId);

        var response = await client.PostAsync("/api/notifications/read-all", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var summary = await client.GetFromJsonAsync<NotificationSummaryDto>("/api/notifications/unread-count");
        summary!.UnreadCount.ShouldBe(0);

        using var scope = _factory.Services.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        (await notifications.GetUnreadCountAsync(otherUserId)).ShouldBe(1); // untouched
    }

    [Fact]
    public async Task MarkAllRead_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.PostAsync("/api/notifications/read-all", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
