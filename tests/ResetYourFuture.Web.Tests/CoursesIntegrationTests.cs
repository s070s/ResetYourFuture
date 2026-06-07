using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection( "web" )]
public class CoursesIntegrationTests
{
    private readonly CustomWebAppFactory _factory;

    public CoursesIntegrationTests( CustomWebAppFactory factory ) => _factory = factory;

    [Fact]
    public async Task GetCourses_Student_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Student" );

        ( await client.GetAsync( "/api/courses" ) ).StatusCode.ShouldBe( HttpStatusCode.OK );
    }

    [Fact]
    public async Task Enroll_AsAdmin_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync( "Admin" );

        var response = await client.PostAsync( $"/api/courses/{Guid.NewGuid()}/enroll", content: null );

        response.StatusCode.ShouldBe( HttpStatusCode.Forbidden );
    }
}
