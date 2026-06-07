using System.Net;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Web.Consumers;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// Unit tests for the typed API consumers (and the shared <c>ApiClientBase</c> helpers)
/// using a stubbed HttpMessageHandler — no server involved.
/// </summary>
public class ConsumerTests
{
    // ---- CourseConsumer ------------------------------------------------------

    [Fact]
    public async Task CourseConsumer_GetCourses_Success_MapsPagedResultAndUrl()
    {
        var body = new PagedResult<CourseListItemDto>(
            new List<CourseListItemDto> { new( Guid.NewGuid(), "Course A", null, false, 3, SubscriptionTierEnum.Free ) },
            1, 1, 10 );
        var (client, handler) = TestHttp.Json( HttpStatusCode.OK, body );
        var consumer = new CourseConsumer( client );

        var result = await consumer.GetCoursesAsync( page: 2, pageSize: 5, lang: "el" );

        result.Items.Count.ShouldBe( 1 );
        result.Items[0].Title.ShouldBe( "Course A" );
        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldContain( "api/courses" );
        handler.LastRequest.RequestUri.Query.ShouldContain( "page=2" );
    }

    [Fact]
    public async Task CourseConsumer_GetCourses_ServerError_ReturnsEmptyPagedResult()
    {
        var (client, _) = TestHttp.Json( HttpStatusCode.InternalServerError );
        var consumer = new CourseConsumer( client );

        var result = await consumer.GetCoursesAsync();

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe( 0 );
    }

    [Fact]
    public async Task CourseConsumer_GetCourse_NotFound_ReturnsNull()
    {
        var (client, _) = TestHttp.Json( HttpStatusCode.NotFound );
        var consumer = new CourseConsumer( client );

        ( await consumer.GetCourseAsync( Guid.NewGuid() ) ).ShouldBeNull();
    }

    [Fact]
    public async Task CourseConsumer_Enroll_Forbidden_StillReadsBody()
    {
        // EnrollAsync deliberately reads the body on 403 (to surface the upgrade message).
        var (client, _) = TestHttp.Json( HttpStatusCode.Forbidden,
            new EnrollmentResultDto( false, "Upgrade required", null ) );
        var consumer = new CourseConsumer( client );

        var result = await consumer.EnrollAsync( Guid.NewGuid() );

        result!.Success.ShouldBeFalse();
        result.Message.ShouldBe( "Upgrade required" );
    }

    [Fact]
    public async Task CourseConsumer_Enroll_ServerError_ReturnsNull()
    {
        var (client, _) = TestHttp.Json( HttpStatusCode.InternalServerError );
        var consumer = new CourseConsumer( client );

        ( await consumer.EnrollAsync( Guid.NewGuid() ) ).ShouldBeNull();
    }

    [Fact]
    public async Task CourseConsumer_CompleteLesson_Success_ReturnsResult()
    {
        var (client, _) = TestHttp.Json( HttpStatusCode.OK,
            new LessonCompletionResultDto( true, "done", 1, 2, 50.0, false ) );
        var consumer = new CourseConsumer( client );

        var result = await consumer.CompleteLessonAsync( Guid.NewGuid() );

        result!.CompletedLessons.ShouldBe( 1 );
        result.CourseCompleted.ShouldBeFalse();
    }

    // ---- BlogConsumer --------------------------------------------------------

    [Fact]
    public async Task BlogConsumer_GetSummaries_Success_ReturnsList()
    {
        var body = new List<BlogArticleSummaryDto>
        {
            new( Guid.NewGuid(), "Title", "slug", "Summary", null, "Author", Array.Empty<string>(), DateTimeOffset.UtcNow )
        };
        var (client, handler) = TestHttp.Json( HttpStatusCode.OK, body );
        var consumer = new BlogConsumer( client );

        var result = await consumer.GetSummariesAsync( count: 3, lang: "en" );

        result!.Count.ShouldBe( 1 );
        handler.LastRequest!.RequestUri!.Query.ShouldContain( "count=3" );
    }

    [Fact]
    public async Task BlogConsumer_GetBySlug_NotFound_ReturnsNull()
    {
        var (client, _) = TestHttp.Json( HttpStatusCode.NotFound );
        var consumer = new BlogConsumer( client );

        ( await consumer.GetBySlugAsync( "missing" ) ).ShouldBeNull();
    }
}
