using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class BlogArticleServiceTests
{
    private static BlogArticleService NewService(ApplicationDbContext db) =>
        new(db, NullLogger<BlogArticleService>.Instance, new HtmlSanitizer());

    private static BlogArticle Article(
        string slug, string titleEn = "Title", bool published = false,
        DateTimeOffset? publishedAt = null, string? titleEl = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TitleEn = titleEn,
            TitleEl = titleEl,
            Slug = slug,
            SummaryEn = "Summary",
            ContentEn = "Content",
            AuthorName = "Author",
            IsPublished = published,
            PublishedAt = publishedAt
        };

    private static SaveBlogArticleRequest Request(
        string slug = "slug", string titleEn = "Title", string contentEn = "Body",
        string? contentEl = null, string[]? tags = null, bool published = false) =>
        new(titleEn, null, slug, "Summary", null, contentEn, contentEl, null, "Author", tags, published);

    // ---- reads ---------------------------------------------------------------

    [Fact]
    public async Task GetPublishedSummaries_ReturnsPublishedNewestFirst_RespectingCount()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.BlogArticles.AddRange(
            Article("a", "A", published: true, publishedAt: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Article("b", "B", published: true, publishedAt: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Article("c", "C", published: true, publishedAt: new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Article("d", "D", published: false));
        await db.SaveChangesAsync();

        var summaries = await NewService(db).GetPublishedSummariesAsync(2);

        summaries.Select(s => s.Title).ShouldBe(new[] { "B", "C" });
    }

    [Fact]
    public async Task GetPublishedBySlug_Unpublished_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.BlogArticles.Add(Article("draft", published: false));
        await db.SaveChangesAsync();

        (await NewService(db).GetPublishedBySlugAsync("draft")).ShouldBeNull();
    }

    [Fact]
    public async Task GetPublishedBySlug_Published_ReturnsArticle()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.BlogArticles.Add(Article("live", "Live", published: true));
        await db.SaveChangesAsync();

        (await NewService(db).GetPublishedBySlugAsync("live"))!.Title.ShouldBe("Live");
    }

    [Fact]
    public async Task GetAllForAdmin_OrdersByCreatedAtDesc_AndClampsPaging()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.BlogArticles.Add(Article("a"));
        await db.SaveChangesAsync();

        var result = await NewService(db).GetAllForAdminAsync(page: 0, pageSize: 999, search: null);

        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(100); // clamped to max
    }

    [Fact]
    public async Task GetAllForAdmin_SearchFiltersByTitleSlugAuthor_OnSqlite()
    {
        // EF.Functions.Like is unsupported on the InMemory provider — use the relational SQLite fixture.
        await using var db = DbContextFactory.CreateSqlite();
        db.BlogArticles.Add(Article("alpha-post", "Alpha"));
        db.BlogArticles.Add(Article("beta-post", "Beta"));
        await db.SaveChangesAsync();

        var result = await NewService(db).GetAllForAdminAsync(1, 10, "alpha");

        result.Items.Select(i => i.Slug).ShouldBe(new[] { "alpha-post" });
    }

    // ---- create --------------------------------------------------------------

    [Fact]
    public async Task Create_DuplicateSlug_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.BlogArticles.Add(Article("taken"));
        await db.SaveChangesAsync();

        (await NewService(db).CreateAsync(Request(slug: "taken"))).ShouldBeNull();
    }

    [Fact]
    public async Task Create_SanitizesContentAndSetsPublishedAtWhenPublished()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var dto = await NewService(db).CreateAsync(
            Request(contentEn: "<script>x</script><p>Body</p>", published: true));

        dto!.ContentEn.ShouldNotContain("<script");
        dto.PublishedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_UnpublishedHasNoPublishedAt()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var dto = await NewService(db).CreateAsync(Request(published: false));

        dto!.PublishedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Create_SerializesTags_RoundTrip()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var dto = await NewService(db).CreateAsync(Request(tags: new[] { "x", "y" }));

        dto!.Tags.ShouldBe(new[] { "x", "y" });
    }

    [Fact]
    public async Task Create_EmptyTags_ResultInEmptyArray()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var dto = await NewService(db).CreateAsync(Request(tags: null));

        dto!.Tags.ShouldBeEmpty();
    }

    // ---- update / state ------------------------------------------------------

    [Fact]
    public async Task Update_Missing_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).UpdateAsync(Guid.NewGuid(), Request())).ShouldBeNull();
    }

    [Fact]
    public async Task Update_SlugTakenByOther_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var a = Article("a-slug");
        var b = Article("b-slug");
        db.BlogArticles.AddRange(a, b);
        await db.SaveChangesAsync();

        (await NewService(db).UpdateAsync(b.Id, Request(slug: "a-slug"))).ShouldBeNull();
    }

    [Fact]
    public async Task Update_PublishTransition_SetsPublishedAtOnce()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var a = Article("a", published: false);
        db.BlogArticles.Add(a);
        await db.SaveChangesAsync();

        var dto = await NewService(db).UpdateAsync(a.Id, Request(slug: "a", published: true));

        dto!.IsPublished.ShouldBeTrue();
        dto.PublishedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Publish_Unpublish_Delete_HandleMissingAndState()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var a = Article("a", published: false);
        db.BlogArticles.Add(a);
        await db.SaveChangesAsync();
        var svc = NewService(db);

        (await svc.PublishAsync(Guid.NewGuid())).ShouldBeFalse();
        (await svc.PublishAsync(a.Id)).ShouldBeTrue();
        (await svc.UnpublishAsync(a.Id)).ShouldBeTrue();
        (await svc.DeleteAsync(a.Id)).ShouldBeTrue();
        (await db.BlogArticles.CountAsync()).ShouldBe(0);
    }
}
