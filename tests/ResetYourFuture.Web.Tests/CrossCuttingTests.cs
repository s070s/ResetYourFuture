using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection( "web" )]
public class CrossCuttingTests
{
    private readonly CustomWebAppFactory _factory;

    public CrossCuttingTests( CustomWebAppFactory factory ) => _factory = factory;

    [Fact]
    public async Task ProtectedEndpoint_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync( "/api/courses" );

        response.StatusCode.ShouldBe( HttpStatusCode.Unauthorized );
    }

    [Fact]
    public async Task AdminEndpoint_Student_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        var response = await client.GetAsync( "/api/admin/courses" );

        response.StatusCode.ShouldBe( HttpStatusCode.Forbidden );
    }

    [Fact]
    public async Task AdminEndpoint_Admin_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Admin" );

        var response = await client.GetAsync( "/api/admin/courses" );

        response.StatusCode.ShouldBe( HttpStatusCode.OK );
    }

    [Fact]
    public async Task Responses_IncludeSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync( "/api/blog/summaries" );

        response.Headers.Contains( "X-Content-Type-Options" ).ShouldBeTrue();
        response.Headers.Contains( "X-Frame-Options" ).ShouldBeTrue();
        response.Headers.Contains( "Referrer-Policy" ).ShouldBeTrue();
        response.Headers.Contains( "Permissions-Policy" ).ShouldBeTrue();
    }
}
