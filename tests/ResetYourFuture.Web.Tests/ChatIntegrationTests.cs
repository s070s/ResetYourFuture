using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection("web")]
public class ChatIntegrationTests
{
    private readonly CustomWebAppFactory _factory;

    public ChatIntegrationTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Conversations_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/chat/conversations")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Conversations_FreeUser_Returns403()
    {
        // Chat requires PrioritySupport (Pro). A fresh Student is Free → 403.
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync("/api/chat/conversations")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnreadCount_Authenticated_Returns200()
    {
        // Unread-count is not gated by subscription.
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync("/api/chat/unread-count")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
