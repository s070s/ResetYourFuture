using System.Net;
using ResetYourFuture.Shared.DTOs;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection( "web" )]
public class CertificatesIntegrationTests
{
    private readonly CustomWebAppFactory _factory;

    public CertificatesIntegrationTests( CustomWebAppFactory factory ) => _factory = factory;

    [Fact]
    public async Task Verify_Unknown_Returns200WithInvalidResult()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync( $"/api/certificates/verify/{Guid.NewGuid()}" );

        response.StatusCode.ShouldBe( HttpStatusCode.OK );
        ( await response.Content.ReadAsStringAsync() ).ShouldContain( "not found" );
    }

    [Fact]
    public async Task My_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        ( await client.GetAsync( "/api/certificates/my" ) ).StatusCode.ShouldBe( HttpStatusCode.Unauthorized );
    }

    [Fact]
    public async Task My_Authenticated_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        ( await client.GetAsync( "/api/certificates/my" ) ).StatusCode.ShouldBe( HttpStatusCode.OK );
    }

    [Fact]
    public async Task Download_Unknown_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        ( await client.GetAsync( $"/api/certificates/{Guid.NewGuid()}/download" ) ).StatusCode.ShouldBe( HttpStatusCode.NotFound );
    }

    [Fact]
    public async Task Issue_FreePlan_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        var response = await client.PostAsync( $"/api/certificates/issue/{Guid.NewGuid()}", content: null );

        response.StatusCode.ShouldBe( HttpStatusCode.Forbidden );
    }

    [Fact]
    public async Task Issue_ProPlan_NoCompletedEnrollment_Returns400()
    {
        var client = await _factory.CreateAuthenticatedClientWithPlanAsync( "Student", SubscriptionTierEnum.Pro );

        var response = await client.PostAsync( $"/api/certificates/issue/{Guid.NewGuid()}", content: null );

        response.StatusCode.ShouldBe( HttpStatusCode.BadRequest );
    }
}
