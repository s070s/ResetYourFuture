using System.Net;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Domain.Entities;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class LessonAssetsIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public LessonAssetsIntegrationTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task<string> MintAssetTokenAsync(string userId, Guid lessonId)
    {
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
        return await authService.CreateLessonAssetTokenAsync(principal, lessonId);
    }

    private static (Course course, Lesson lesson) BuildCourseWithLesson(string? pdfPath = null)
    {
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
        var module = new Module { Id = Guid.NewGuid(), TitleEn = "M", CourseId = course.Id };
        var lesson = new Lesson { Id = Guid.NewGuid(), TitleEn = "L", ModuleId = module.Id, PdfPath = pdfPath };
        module.Lessons.Add(lesson);
        course.Modules.Add(module);
        return (course, lesson);
    }

    [Fact]
    public async Task Asset_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync($"/api/lessons/{Guid.NewGuid()}/asset?type=pdf"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Asset_LessonMissing_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync($"/api/lessons/{Guid.NewGuid()}/asset?type=pdf"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Asset_NotEnrolled_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");
        var (course, lesson) = BuildCourseWithLesson(pdfPath: "lessons/pdf/x.pdf");
        await _factory.SeedAsync(async db =>
        {
            db.Courses.Add(course);
            await db.SaveChangesAsync();
        });

        (await client.GetAsync($"/api/lessons/{lesson.Id}/asset?type=pdf"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Asset_EnrolledButNoPdf_Returns404()
    {
        var (client, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var (course, lesson) = BuildCourseWithLesson(pdfPath: null);
        await _factory.SeedAsync(async db =>
        {
            db.Courses.Add(course);
            db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = userId, CourseId = course.Id });
            await db.SaveChangesAsync();
        });

        (await client.GetAsync($"/api/lessons/{lesson.Id}/asset?type=pdf"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Asset_EnrolledInvalidType_Returns404()
    {
        var (client, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var (course, lesson) = BuildCourseWithLesson(pdfPath: "lessons/pdf/x.pdf");
        await _factory.SeedAsync(async db =>
        {
            db.Courses.Add(course);
            db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = userId, CourseId = course.Id });
            await db.SaveChangesAsync();
        });

        (await client.GetAsync($"/api/lessons/{lesson.Id}/asset?type=bogus"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Asset_ValidAssetToken_NoAuthHeader_PassesAuthAndEnrollmentChecks()
    {
        // SEC-2: the browser <video>/<iframe> case — no Authorization header at all, just the
        // short-lived, single-lesson-scoped assetToken LessonViewer mints via IAuthService.
        // Asserts it clears authorization/enrollment (not 401/403) rather than a full 200,
        // since LocalFileStorage is real-filesystem-backed and no fixture file exists on disk;
        // the file-not-found path is already covered via the header-auth route below.
        var userId = Guid.NewGuid().ToString("N");
        var (course, lesson) = BuildCourseWithLesson(pdfPath: "lessons/pdf/x.pdf");
        await _factory.SeedAsync(async db =>
        {
            db.Courses.Add(course);
            db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = userId, CourseId = course.Id });
            await db.SaveChangesAsync();
        });
        var token = await MintAssetTokenAsync(userId, lesson.Id);

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/lessons/{lesson.Id}/asset?type=pdf&assetToken={Uri.EscapeDataString(token)}");

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Asset_AssetTokenForDifferentLesson_Returns401()
    {
        var userId = Guid.NewGuid().ToString("N");
        var (course, lesson) = BuildCourseWithLesson(pdfPath: "lessons/pdf/x.pdf");
        await _factory.SeedAsync(async db =>
        {
            db.Courses.Add(course);
            db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = userId, CourseId = course.Id });
            await db.SaveChangesAsync();
        });
        var tokenForOtherLesson = await MintAssetTokenAsync(userId, Guid.NewGuid());

        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/lessons/{lesson.Id}/asset?type=pdf&assetToken={Uri.EscapeDataString(tokenForOtherLesson)}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Asset_GarbageAssetToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/lessons/{Guid.NewGuid()}/asset?type=pdf&assetToken=garbage");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
