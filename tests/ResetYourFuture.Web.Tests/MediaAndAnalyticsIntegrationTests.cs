using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection( "web" )]
public class MediaAndAnalyticsIntegrationTests
{
    private readonly CustomWebAppFactory _factory;

    public MediaAndAnalyticsIntegrationTests( CustomWebAppFactory factory ) => _factory = factory;

    [Fact]
    public async Task Media_DisallowedFolder_Returns404()
    {
        var client = _factory.CreateClient();

        ( await client.GetAsync( "/api/media/secret/file.png" ) ).StatusCode.ShouldBe( HttpStatusCode.NotFound );
    }

    [Fact]
    public async Task Media_AllowedFolderMissingFile_Returns404()
    {
        var client = _factory.CreateClient();

        ( await client.GetAsync( "/api/media/blog/covers/missing.png" ) ).StatusCode.ShouldBe( HttpStatusCode.NotFound );
    }

    [Fact]
    public async Task Media_DisallowedExtension_Returns404()
    {
        var client = _factory.CreateClient();

        ( await client.GetAsync( "/api/media/blog/covers/file.txt" ) ).StatusCode.ShouldBe( HttpStatusCode.NotFound );
    }

    [Fact]
    public async Task Analytics_Admin_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Admin" );

        ( await client.GetAsync( "/api/admin/analytics/summary" ) ).StatusCode.ShouldBe( HttpStatusCode.OK );
    }

    [Fact]
    public async Task Analytics_Student_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        ( await client.GetAsync( "/api/admin/analytics/summary" ) ).StatusCode.ShouldBe( HttpStatusCode.Forbidden );
    }
}
