using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Web.ApiInterfaces;
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
    public async Task Media_ServedFile_CarriesSandboxingCsp()
    {
        // Write a file through the same storage the controller reads from, into a public folder.
        string relPath;
        using ( var scope = _factory.Services.CreateScope() )
        {
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            using var ms = new MemoryStream( [ 0x89, 0x50, 0x4E, 0x47 ] ); // "‰PNG" header bytes — content is irrelevant
            relPath = await storage.SaveFileAsync( ms, "csp-probe.png", "blog/covers" );
        }

        try
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync( $"/api/media/{relPath}" );

            response.StatusCode.ShouldBe( HttpStatusCode.OK );

            var csp = response.Headers.TryGetValues( "Content-Security-Policy", out var v )
                ? string.Join( " ", v )
                : response.Content.Headers.TryGetValues( "Content-Security-Policy", out var cv )
                    ? string.Join( " ", cv )
                    : "";
            csp.ShouldContain( "sandbox" );
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            await storage.DeleteFileAsync( relPath );
        }
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
