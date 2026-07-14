using System.Net;
using System.Net.Http.Json;
using ResetYourFuture.Application.DTOs;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class ProfileIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public ProfileIntegrationTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task GetProfile_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProfile_Authenticated_Returns200WithEmail()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        var response = await client.GetAsync("/api/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProfileDto>();
        dto!.Email.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateProfile_ChangesName()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        var response = await client.PutAsJsonAsync("/api/profile",
            new UpdateProfileRequest("UpdatedFirst", "UpdatedLast", "Nick", null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProfileDto>();
        dto!.FirstName.ShouldBe("UpdatedFirst");
        dto.DisplayName.ShouldBe("Nick");
    }

    [Fact]
    public async Task ChangePassword_ExceedsPerUserRateLimit_Returns429()
    {
        // SEC-3: change-password had no back-pressure at all. The limiter counts every request
        // regardless of body validity, so a wrong CurrentPassword still exhausts the budget.
        var client = await _factory.CreateAuthenticatedClientAsync("Student");
        var body = new ChangePasswordRequest("wrong-current-password", "New-Pass-1!");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 21; i++)
            last = await client.PostAsJsonAsync("/api/profile/change-password", body);

        last!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task DeleteAccount_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.DeleteAsync("/api/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAccount_AuthenticatedStudent_RemovesUser()
    {
        // COMP-3: self-service erasure — the endpoint reuses AdminUserService.DeleteUserAsync,
        // so this only needs to confirm the route/auth wiring, not the deletion logic itself
        // (covered by AdminUserServiceTests).
        var (client, _) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");

        var response = await client.DeleteAsync("/api/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAccount_Admin_Returns400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.DeleteAsync("/api/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
