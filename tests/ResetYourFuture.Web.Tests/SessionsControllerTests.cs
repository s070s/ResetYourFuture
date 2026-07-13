using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection("web")]
public class SessionsControllerTests
{
    private readonly CustomWebAppFactory _factory;

    public SessionsControllerTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task<ScheduledSession> SeedSessionAsync(string hostUserId, int maxParticipants = 6, ScheduledSessionStatus status = ScheduledSessionStatus.Scheduled)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = new ScheduledSession
        {
            Id = Guid.NewGuid(),
            HostUserId = hostUserId,
            TitleEn = $"Session-{Guid.NewGuid():N}",
            StartsAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            DurationMinutes = 30,
            MaxParticipants = maxParticipants,
            Status = status
        };
        db.ScheduledSessions.Add(session);
        // The host must exist — GetUpcomingAsync projects s.Host.DisplayName inline, and a
        // session whose Host navigation can't resolve is silently dropped from InMemory results.
        if (!await db.Users.AnyAsync(u => u.Id == hostUserId))
        {
            db.Users.Add(new Domain.Identity.ApplicationUser
            {
                Id = hostUserId, UserName = $"{hostUserId}@x.com", Email = $"{hostUserId}@x.com", FirstName = "Host", LastName = "Name"
            });
        }
        await db.SaveChangesAsync();
        return session;
    }

    [Fact]
    public async Task GetUpcoming_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/sessions")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUpcoming_Authenticated_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync("/api/sessions")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_Succeeds_ThenAppearsAsRegistered()
    {
        var session = await SeedSessionAsync(hostUserId: "some-host");
        var (client, _) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");

        var response = await client.PostAsync($"/api/sessions/{session.Id}/register", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var upcoming = await client.GetFromJsonAsync<List<ScheduledSessionListItemDto>>("/api/sessions");
        upcoming!.Single(s => s.Id == session.Id).IsRegistered.ShouldBeTrue();
    }

    [Fact]
    public async Task Register_Twice_ReturnsConflict()
    {
        var session = await SeedSessionAsync(hostUserId: "some-host");
        var client = await _factory.CreateAuthenticatedClientAsync("Student");
        await client.PostAsync($"/api/sessions/{session.Id}/register", null);

        var response = await client.PostAsync($"/api/sessions/{session.Id}/register", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unregister_AfterRegister_Succeeds()
    {
        var session = await SeedSessionAsync(hostUserId: "some-host");
        var client = await _factory.CreateAuthenticatedClientAsync("Student");
        await client.PostAsync($"/api/sessions/{session.Id}/register", null);

        var response = await client.PostAsync($"/api/sessions/{session.Id}/unregister", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkCall_NonParticipant_ReturnsForbidden()
    {
        var session = await SeedSessionAsync(hostUserId: "some-host");
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        var response = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/link-call", new LinkCallSessionRequest(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LinkCall_Host_Succeeds()
    {
        var (client, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var session = await SeedSessionAsync(hostUserId: userId);

        var response = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/link-call", new LinkCallSessionRequest(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
