using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class ChatIntegrationTests : IClassFixture<CustomWebAppFactory>
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
    public async Task Conversations_FreeUser_Returns200()
    {
        // Chat is open to every authenticated user — a fresh Free-tier Student gets 200.
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync("/api/chat/conversations")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnreadCount_Authenticated_Returns200()
    {
        // Unread-count is not gated by subscription.
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync("/api/chat/unread-count")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
