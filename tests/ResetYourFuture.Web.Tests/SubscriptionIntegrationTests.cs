using System.Net;
using System.Net.Http.Json;
using ResetYourFuture.Shared.DTOs;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection( "web" )]
public class SubscriptionIntegrationTests
{
    private readonly CustomWebAppFactory _factory;

    public SubscriptionIntegrationTests( CustomWebAppFactory factory ) => _factory = factory;

    [Fact]
    public async Task GetPlans_Anonymous_Returns200()
    {
        var client = _factory.CreateClient();

        ( await client.GetAsync( "/api/subscription/plans" ) ).StatusCode.ShouldBe( HttpStatusCode.OK );
    }

    [Fact]
    public async Task GetStatus_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        ( await client.GetAsync( "/api/subscription/status" ) ).StatusCode.ShouldBe( HttpStatusCode.Unauthorized );
    }

    [Fact]
    public async Task GetStatus_FreshUser_ReturnsFreeTier()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        var response = await client.GetAsync( "/api/subscription/status" );

        response.StatusCode.ShouldBe( HttpStatusCode.OK );
        var status = await response.Content.ReadFromJsonAsync<UserSubscriptionStatusDto>();
        status!.Tier.ShouldBe( SubscriptionTierEnum.Free );
    }

    [Fact]
    public async Task Checkout_UnknownPlan_Returns400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        var response = await client.PostAsJsonAsync( "/api/subscription/checkout", new CreateCheckoutRequest( Guid.NewGuid() ) );

        response.StatusCode.ShouldBe( HttpStatusCode.BadRequest );
    }

    [Fact]
    public async Task Cancel_FreeUser_Returns400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        var response = await client.PostAsync( "/api/subscription/cancel", content: null );

        response.StatusCode.ShouldBe( HttpStatusCode.BadRequest );
    }

    [Fact]
    public async Task Billing_Authenticated_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        ( await client.GetAsync( "/api/subscription/billing" ) ).StatusCode.ShouldBe( HttpStatusCode.OK );
    }
}
