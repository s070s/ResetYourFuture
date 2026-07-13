using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

/// <summary>
/// Title-fallback queries use the same casing as the seeded titles: InMemory's .Contains is
/// ordinal (case-sensitive), unlike SQL Server's default case-insensitive collation — matches
/// the same documented caveat as UserSearchExtensionsTests.
/// </summary>
public class SiteSearchServiceTests
{
    private static (SiteSearchService svc, IAssistantRetrievalService retrieval, AssistantRuntimeState state) NewService(
        ApplicationDbContext db, AssistantAvailability availability = AssistantAvailability.Ready)
    {
        var retrieval = Substitute.For<IAssistantRetrievalService>();
        var state = new AssistantRuntimeState();
        state.Set(availability);

        var svc = new SiteSearchService(db, retrieval, state, NullLogger<SiteSearchService>.Instance);
        return (svc, retrieval, state);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmptyWithoutCallingRetrieval()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, retrieval, _) = NewService(db);

        var result = await svc.SearchAsync("   ", "en", 8);

        result.Hits.ShouldBeEmpty();
        await retrieval.DidNotReceiveWithAnyArgs().SearchGroupedAsync(default!, default!, default);
    }

    [Fact]
    public async Task SearchAsync_Ready_UsesSemanticResultsAndFlagsAsSemanticUsed()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, retrieval, _) = NewService(db);
        retrieval.SearchGroupedAsync("career", "en", 8, Arg.Any<CancellationToken>())
            .Returns([new AssistantSearchHit(AssistantSourceType.Course, "Career Start", "courses/1", "snippet")]);

        var result = await svc.SearchAsync("career", "en", 8);

        result.SemanticSearchUsed.ShouldBeTrue();
        var hit = result.Hits.ShouldHaveSingleItem();
        hit.SourceType.ShouldBe("Course");
        hit.Title.ShouldBe("Career Start");
        hit.Url.ShouldBe("courses/1");
    }

    [Theory]
    [InlineData(AssistantAvailability.Disabled)]
    [InlineData(AssistantAvailability.OllamaUnreachable)]
    [InlineData(AssistantAvailability.DownloadingModels)]
    public async Task SearchAsync_NotReady_SkipsSemanticAndUsesTitleFallback(AssistantAvailability availability)
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "Career Start", IsPublished = true });
        await db.SaveChangesAsync();
        var (svc, retrieval, _) = NewService(db, availability);

        var result = await svc.SearchAsync("Career", "en", 8);

        result.SemanticSearchUsed.ShouldBeFalse();
        result.Hits.ShouldHaveSingleItem().Title.ShouldBe("Career Start");
        await retrieval.DidNotReceiveWithAnyArgs().SearchGroupedAsync(default!, default!, default);
    }

    [Fact]
    public async Task SearchAsync_ReadyButRetrievalThrows_FallsBackToTitleSearch()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "Career Start", IsPublished = true });
        await db.SaveChangesAsync();
        var (svc, retrieval, _) = NewService(db);
        retrieval.SearchGroupedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("ollama down mid-request"));

        var result = await svc.SearchAsync("Career", "en", 8);

        result.SemanticSearchUsed.ShouldBeFalse();
        result.Hits.ShouldHaveSingleItem().Title.ShouldBe("Career Start");
    }

    [Fact]
    public async Task SearchAsync_ReadyButSemanticFindsNothing_FallsBackToTitleSearch()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "Career Start", IsPublished = true });
        await db.SaveChangesAsync();
        var (svc, retrieval, _) = NewService(db);
        retrieval.SearchGroupedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await svc.SearchAsync("Career", "en", 8);

        result.SemanticSearchUsed.ShouldBeFalse();
        result.Hits.ShouldHaveSingleItem().Title.ShouldBe("Career Start");
    }

    [Fact]
    public async Task TitleFallback_MatchesAcrossCoursesAssessmentsAndArticles_PublishedOnly()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "Career Foundations", IsPublished = true });
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "Career Draft (unpublished)", IsPublished = false });
        db.AssessmentDefinitions.Add(new AssessmentDefinition { Id = Guid.NewGuid(), Key = "k1", TitleEn = "Career Values Assessment", SchemaJson = "{}", IsPublished = true });
        db.BlogArticles.Add(new BlogArticle
        {
            Id = Guid.NewGuid(), TitleEn = "5 Career Tips", Slug = "career-tips", SummaryEn = "s", ContentEn = "c",
            AuthorName = "A", IsPublished = true
        });
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService(db, AssistantAvailability.Disabled);

        var result = await svc.SearchAsync("Career", "en", 20);

        result.Hits.Count.ShouldBe(3); // unpublished course excluded
        result.Hits.ShouldContain(h => h.SourceType == "Course" && h.Title == "Career Foundations");
        result.Hits.ShouldContain(h => h.SourceType == "Assessment" && h.Title == "Career Values Assessment");
        result.Hits.ShouldContain(h => h.SourceType == "BlogArticle" && h.Title == "5 Career Tips");
    }

    [Fact]
    public async Task TitleFallback_NoMatch_ReturnsEmptyHits()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "Career Start", IsPublished = true });
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService(db, AssistantAvailability.Disabled);

        (await svc.SearchAsync("zzz-no-match", "en", 8)).Hits.ShouldBeEmpty();
    }
}
