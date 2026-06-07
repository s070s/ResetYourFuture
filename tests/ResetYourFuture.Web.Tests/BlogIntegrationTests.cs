using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection( "web" )]
public class BlogIntegrationTests
{
    private readonly CustomWebAppFactory _factory;

    public BlogIntegrationTests( CustomWebAppFactory factory ) => _factory = factory;

    [Fact]
    public async Task Summaries_Anonymous_Returns200()
    {
        var client = _factory.CreateClient();

        ( await client.GetAsync( "/api/blog/summaries" ) ).StatusCode.ShouldBe( HttpStatusCode.OK );
    }

    [Fact]
    public async Task GetBySlug_Unknown_Returns404()
    {
        var client = _factory.CreateClient();

        ( await client.GetAsync( "/api/blog/this-slug-does-not-exist" ) ).StatusCode.ShouldBe( HttpStatusCode.NotFound );
    }
}
