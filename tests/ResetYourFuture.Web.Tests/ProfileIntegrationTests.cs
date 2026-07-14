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

public class ProfileIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public ProfileIntegrationTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task GetProfile_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProfile_Authenticated_Returns200WithEmail()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        var response = await client.GetAsync("/api/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProfileDto>();
        dto!.Email.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateProfile_ChangesName()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        var response = await client.PutAsJsonAsync("/api/profile",
            new UpdateProfileRequest("UpdatedFirst", "UpdatedLast", "Nick", null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProfileDto>();
        dto!.FirstName.ShouldBe("UpdatedFirst");
        dto.DisplayName.ShouldBe("Nick");
    }

    [Fact]
    public async Task ChangePassword_ExceedsPerUserRateLimit_Returns429()
    {
        // SEC-3: change-password had no back-pressure at all. The limiter counts every request
        // regardless of body validity, so a wrong CurrentPassword still exhausts the budget.
        var client = await _factory.CreateAuthenticatedClientAsync("Student");
        var body = new ChangePasswordRequest("wrong-current-password", "New-Pass-1!");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 21; i++)
            last = await client.PostAsJsonAsync("/api/profile/change-password", body);

        last!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task DeleteAccount_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.DeleteAsync("/api/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAccount_AuthenticatedStudent_RemovesUser()
    {
        // COMP-3: self-service erasure — the endpoint reuses AdminUserService.DeleteUserAsync,
        // so this only needs to confirm the route/auth wiring, not the deletion logic itself
        // (covered by AdminUserServiceTests).
        var (client, _) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");

        var response = await client.DeleteAsync("/api/profile");

        // API-8: DELETE returns 204, matching every other DELETE in the codebase.
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAccount_Admin_Returns400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.DeleteAsync("/api/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExportMyData_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/profile/export")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExportMyData_Authenticated_ReturnsDownloadableJsonWithAllSections()
    {
        // COMP-4: GDPR access/portability — one JSON file aggregating every personal-data
        // category the recommendation calls out. Seeds one row per category directly via the
        // DbContext (no admin endpoints for course/certificate creation exist in this factory)
        // and checks each maps through, not just that the endpoint responds.
        var (client, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var course = new Course { Id = Guid.NewGuid(), TitleEn = "Export Test Course", IsPublished = true };
            db.Courses.Add(course);
            var enrollment = new Enrollment
            {
                Id = Guid.NewGuid(), UserId = userId, CourseId = course.Id,
                Status = EnrollmentStatus.Completed, CompletedAt = DateTime.UtcNow
            };
            db.Enrollments.Add(enrollment);

            var assessment = new AssessmentDefinition
            {
                Id = Guid.NewGuid(), Key = $"export-test-{Guid.NewGuid():N}",
                TitleEn = "Export Test Assessment", SchemaJson = """{"questions":[]}"""
            };
            db.AssessmentDefinitions.Add(assessment);
            db.AssessmentSubmissions.Add(new AssessmentSubmission
            {
                Id = Guid.NewGuid(), UserId = userId, AssessmentDefinitionId = assessment.Id,
                AnswersJson = """{"q1":"answer"}"""
            });

            db.Certificates.Add(new Certificate
            {
                Id = Guid.NewGuid(), UserId = userId, EnrollmentId = enrollment.Id, CourseId = course.Id,
                RecipientName = "Test User", CourseTitleEn = course.TitleEn
            });

            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Pro", Tier = SubscriptionTier.Pro, Price = 20m, IsActive = true };
            db.SubscriptionPlans.Add(plan);
            db.BillingTransactions.Add(new BillingTransaction
            {
                Id = Guid.NewGuid(), UserId = userId, SubscriptionPlanId = plan.Id,
                Amount = 20m, Type = BillingTransactionType.Purchase, Description = "Test purchase"
            });

            db.ChatMessages.Add(new ChatMessage
            {
                Id = Guid.NewGuid(), ConversationId = Guid.NewGuid(), SenderId = userId, Content = "Hello export test"
            });
            // Someone else's message must NOT appear in this user's export.
            db.ChatMessages.Add(new ChatMessage
            {
                Id = Guid.NewGuid(), ConversationId = Guid.NewGuid(), SenderId = $"other-{Guid.NewGuid():N}", Content = "Not mine"
            });

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/profile/export");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        var export = await response.Content.ReadFromJsonAsync<MyDataExportDto>();

        export!.Profile.Email.ShouldNotBeNullOrEmpty();
        export.Enrollments.Single().CourseTitle.ShouldBe("Export Test Course");
        export.AssessmentSubmissions.Single().AssessmentTitle.ShouldBe("Export Test Assessment");
        export.Certificates.Single().CourseTitle.ShouldBe("Export Test Course");
        export.BillingTransactions.Single().PlanName.ShouldBe("Pro");
        export.ChatMessages.Single().Content.ShouldBe("Hello export test");
    }
}
