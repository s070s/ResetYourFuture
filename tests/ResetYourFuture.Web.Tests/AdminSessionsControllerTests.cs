using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class AdminSessionsControllerTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AdminSessionsControllerTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task<ScheduledSession> SeedSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = new ScheduledSession
        {
            Id = Guid.NewGuid(),
            HostUserId = $"host-{Guid.NewGuid():N}",
            TitleEn = $"Session-{Guid.NewGuid():N}",
            StartsAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            DurationMinutes = 30,
            MaxParticipants = 6
        };
        db.ScheduledSessions.Add(session);
        // The host must exist — the admin list projects s.Host.DisplayName inline, and a session
        // whose Host navigation can't resolve is silently dropped from InMemory results.
        db.Users.Add(new Domain.Identity.ApplicationUser
        {
            Id = session.HostUserId, UserName = $"{session.HostUserId}@x.com", Email = $"{session.HostUserId}@x.com", FirstName = "Host", LastName = "Name"
        });
        await db.SaveChangesAsync();
        return session;
    }

    [Fact]
    public async Task GetAll_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/admin/sessions")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_StudentRole_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync("/api/admin/sessions")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_Admin_ReturnsSeededSession()
    {
        var session = await SeedSessionAsync();
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var result = await client.GetFromJsonAsync<PagedResult<AdminScheduledSessionDto>>("/api/admin/sessions?pageSize=100");

        result!.Items.ShouldContain(s => s.Id == session.Id);
    }

    [Fact]
    public async Task Create_Admin_BecomesHost()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.PostAsJsonAsync("/api/admin/sessions",
            new SaveScheduledSessionRequest("New Session", null, null, DateTimeOffset.UtcNow.AddHours(2), 45, 6));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<AdminScheduledSessionDto>();
        created!.DurationMinutes.ShouldBe(45);
    }

    [Fact]
    public async Task Cancel_Admin_FlipsStatus()
    {
        var session = await SeedSessionAsync();
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.PostAsync($"/api/admin/sessions/{session.Id}/cancel", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.ScheduledSessions.FindAsync(session.Id))!.Status.ShouldBe(Domain.Enums.ScheduledSessionStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_UnknownId_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        (await client.PostAsync($"/api/admin/sessions/{Guid.NewGuid()}/cancel", null))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
