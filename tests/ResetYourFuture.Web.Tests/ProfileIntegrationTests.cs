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
}
