using System.Net;
using ResetYourFuture.Web.Domain.Entities;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection("web")]
public class LessonAssetsIntegrationTests
{
    private readonly CustomWebAppFactory _factory;

    public LessonAssetsIntegrationTests(CustomWebAppFactory factory) => _factory = factory;

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
}
