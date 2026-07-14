using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class AssessmentServiceTests
{
    private const string UserId = "user-1";

    private const string SchemaJson =
        """{"questions":[{"id":"q1","type":"text","required":true},{"id":"q2","type":"text","required":false}]}""";

    private static AssessmentService NewService(ApplicationDbContext db, SubscriptionTier tier = SubscriptionTier.Pro)
    {
        var subs = Substitute.For<ISubscriptionService>();
        subs.GetUserStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserSubscriptionStatusDto(
                tier, tier.ToString(), DateTime.UtcNow, null, true,
                new PlanFeaturesDto { AssessmentAccess = true }));

        return new AssessmentService(db, subs, new MemoryCache(new MemoryCacheOptions()), NullLogger<AssessmentService>.Instance);
    }

    private static AssessmentDefinition Published(string schemaJson = SchemaJson) => new()
    {
        Id = Guid.NewGuid(),
        Key = $"key-{Guid.NewGuid():N}",
        TitleEn = "Assessment",
        SchemaJson = schemaJson,
        IsPublished = true,
        RequiredTier = SubscriptionTier.Free
    };

    // ---- SubmitAssessmentAsync (DQ-2) -----------------------------------------

    [Fact]
    public async Task Submit_ValidAnswers_Succeeds()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var assessment = Published();
        db.AssessmentDefinitions.Add(assessment);
        await db.SaveChangesAsync();

        var result = await NewService(db).SubmitAssessmentAsync(
            UserId, assessment.Id, new SubmitAssessmentRequest("""{"q1":"answer","q2":""}""", null));

        result.IsSuccess.ShouldBeTrue();
        (await db.AssessmentSubmissions.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Submit_MissingOptionalAnswer_Succeeds()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var assessment = Published();
        db.AssessmentDefinitions.Add(assessment);
        await db.SaveChangesAsync();

        var result = await NewService(db).SubmitAssessmentAsync(
            UserId, assessment.Id, new SubmitAssessmentRequest("""{"q1":"answer"}""", null));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Submit_MalformedAnswersJson_ReturnsBadRequestAndDoesNotPersist()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var assessment = Published();
        db.AssessmentDefinitions.Add(assessment);
        await db.SaveChangesAsync();

        var result = await NewService(db).SubmitAssessmentAsync(
            UserId, assessment.Id, new SubmitAssessmentRequest("not json", null));

        result.StatusCode.ShouldBe(400);
        (await db.AssessmentSubmissions.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Submit_UnknownAnswerKey_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var assessment = Published();
        db.AssessmentDefinitions.Add(assessment);
        await db.SaveChangesAsync();

        var result = await NewService(db).SubmitAssessmentAsync(
            UserId, assessment.Id, new SubmitAssessmentRequest("""{"q1":"answer","not-a-question":"x"}""", null));

        result.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task Submit_MissingRequiredAnswer_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var assessment = Published();
        db.AssessmentDefinitions.Add(assessment);
        await db.SaveChangesAsync();

        var result = await NewService(db).SubmitAssessmentAsync(
            UserId, assessment.Id, new SubmitAssessmentRequest("""{"q1":"   ","q2":"answer"}""", null));

        result.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task Submit_CorruptStoredSchema_DoesNotBlockSubmission()
    {
        // DQ-4 (schema corruption) shouldn't compound into DQ-2 rejecting every submission
        // for an assessment whose schema is already broken.
        await using var db = DbContextFactory.CreateInMemory();
        var assessment = Published(schemaJson: "not json");
        db.AssessmentDefinitions.Add(assessment);
        await db.SaveChangesAsync();

        var result = await NewService(db).SubmitAssessmentAsync(
            UserId, assessment.Id, new SubmitAssessmentRequest("""{"anything":"goes"}""", null));

        result.IsSuccess.ShouldBeTrue();
    }
}
