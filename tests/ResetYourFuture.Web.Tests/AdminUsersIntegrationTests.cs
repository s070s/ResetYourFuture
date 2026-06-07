using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection( "web" )]
public class AdminUsersIntegrationTests
{
    private readonly CustomWebAppFactory _factory;

    public AdminUsersIntegrationTests( CustomWebAppFactory factory ) => _factory = factory;

    [Fact]
    public async Task GetUsers_Admin_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Admin" );

        ( await client.GetAsync( "/api/admin/users" ) ).StatusCode.ShouldBe( HttpStatusCode.OK );
    }

    [Fact]
    public async Task GetUsers_Student_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        ( await client.GetAsync( "/api/admin/users" ) ).StatusCode.ShouldBe( HttpStatusCode.Forbidden );
    }

    [Fact]
    public async Task GetUser_Unknown_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Admin" );

        ( await client.GetAsync( "/api/admin/users/does-not-exist" ) ).StatusCode.ShouldBe( HttpStatusCode.NotFound );
    }

    [Fact]
    public async Task GetRoles_Admin_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Admin" );

        var response = await client.GetAsync( "/api/admin/roles" );

        response.StatusCode.ShouldBe( HttpStatusCode.OK );
        ( await response.Content.ReadAsStringAsync() ).ShouldContain( "Student" );
    }

    [Fact]
    public async Task ToggleEnable_Unknown_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Admin" );

        var response = await client.PostAsync( "/api/admin/users/does-not-exist/toggle-enable", content: null );

        response.StatusCode.ShouldBe( HttpStatusCode.NotFound );
    }
}
