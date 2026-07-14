using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class CoursesControllerReviewsTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public CoursesControllerReviewsTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task<Course> SeedPublishedCourseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = $"Course-{Guid.NewGuid():N}", IsPublished = true };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        return course;
    }

    private async Task EnrollAsync(string userId, Guid courseId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = userId, CourseId = courseId });
        await db.SaveChangesAsync();
    }

    private async Task SeedApprovedReviewAsync(string userId, Guid courseId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // The review's author must exist — GetApprovedForCourseAsync projects r.User.DisplayName
        // inline, and a review whose User navigation can't resolve is silently dropped.
        db.Users.Add(new Domain.Identity.ApplicationUser
        {
            Id = userId, UserName = $"{userId}@x.com", Email = $"{userId}@x.com", FirstName = "Author", LastName = "Name"
        });
        db.CourseReviews.Add(new CourseReview { CourseId = courseId, UserId = userId, Rating = 5, Body = "great", Status = ReviewStatus.Approved });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetReviews_Anonymous_Returns401()
    {
        var course = await SeedPublishedCourseAsync();
        var client = _factory.CreateClient();

        (await client.GetAsync($"/api/courses/{course.Id}/reviews")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetReviews_ReturnsApprovedReviewsAndSummary()
    {
        var course = await SeedPublishedCourseAsync();
        await SeedApprovedReviewAsync("author-1", course.Id);
        var (client, _) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");

        var result = await client.GetFromJsonAsync<CourseReviewsResponseDto>($"/api/courses/{course.Id}/reviews");

        result.ShouldNotBeNull();
        result!.Reviews.ShouldHaveSingleItem();
        result.Summary.ShouldNotBeNull();
        result.Summary!.AverageRating.ShouldBe(5.0);
    }

    [Fact]
    public async Task SaveReview_NotEnrolled_ReturnsForbidden()
    {
        var course = await SeedPublishedCourseAsync();
        var (client, _) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");

        var response = await client.PostAsJsonAsync($"/api/courses/{course.Id}/reviews", new SaveCourseReviewRequest(5, "Loved it"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SaveReview_Enrolled_CreatesPendingReview()
    {
        var course = await SeedPublishedCourseAsync();
        var (client, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        await EnrollAsync(userId, course.Id);

        var response = await client.PostAsJsonAsync($"/api/courses/{course.Id}/reviews", new SaveCourseReviewRequest(4, "Pretty good"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saved = await response.Content.ReadFromJsonAsync<MyCourseReviewDto>();
        saved!.Status.ShouldBe("Pending");
    }

    [Fact]
    public async Task GetReviews_MyPendingReview_VisibleOnlyToItsAuthor()
    {
        var course = await SeedPublishedCourseAsync();
        var (author, authorId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        await EnrollAsync(authorId, course.Id);
        await author.PostAsJsonAsync($"/api/courses/{course.Id}/reviews", new SaveCourseReviewRequest(3, "mine"));

        var mine = await author.GetFromJsonAsync<CourseReviewsResponseDto>($"/api/courses/{course.Id}/reviews");
        var (otherClient, _) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var theirs = await otherClient.GetFromJsonAsync<CourseReviewsResponseDto>($"/api/courses/{course.Id}/reviews");

        mine!.MyReview.ShouldNotBeNull();
        mine.Reviews.ShouldBeEmpty(); // pending — not publicly visible yet
        theirs!.MyReview.ShouldBeNull();
        theirs.Reviews.ShouldBeEmpty();
    }
}
