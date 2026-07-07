using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class AssistantIndexingServiceTests
{
    private static (AssistantIndexingService svc, IEmbeddingGenerator<string, Embedding<float>> embedGen) NewService(ApplicationDbContext db)
    {
        var embedGen = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        embedGen.GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new GeneratedEmbeddings<Embedding<float>>(
                ((IEnumerable<string>)callInfo[0]).Select(_ => new Embedding<float>(new float[] { 1f, 2f, 3f }))));

        return (new AssistantIndexingService(db, embedGen, NullLogger<AssistantIndexingService>.Instance), embedGen);
    }

    private static Course PublishedCourse(string titleEl = "Τίτλος") => new()
    {
        Id = Guid.NewGuid(),
        TitleEn = "Career Discovery",
        TitleEl = titleEl,
        DescriptionEn = "Find your path.",
        IsPublished = true
    };

    [Fact]
    public async Task FirstPass_PublishedCourseWithBothLanguages_CreatesEnAndElChunks()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add(PublishedCourse());
        await db.SaveChangesAsync();
        var (svc, embedGen) = NewService(db);

        var summary = await svc.RunIndexPassAsync();

        summary.Added.ShouldBe(2); // en + el
        var chunks = await db.AssistantContentChunks.ToListAsync();
        chunks.Select(c => c.Language).OrderBy(l => l).ShouldBe(["el", "en"]);
        chunks.ShouldAllBe(c => c.SourceType == AssistantSourceType.Course);
        await embedGen.Received(2).GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FirstPass_CourseWithNoGreekTitle_OnlyCreatesEnglishChunk()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "English Only", IsPublished = true, TitleEl = null });
        await db.SaveChangesAsync();
        var (svc, _) = NewService(db);

        await svc.RunIndexPassAsync();

        var chunks = await db.AssistantContentChunks.ToListAsync();
        chunks.ShouldAllBe(c => c.Language == "en");
    }

    [Fact]
    public async Task SecondPass_UnchangedContent_DoesNotReEmbed()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add(PublishedCourse());
        await db.SaveChangesAsync();
        var (svc, embedGen) = NewService(db);

        await svc.RunIndexPassAsync();
        embedGen.ClearReceivedCalls();

        var summary = await svc.RunIndexPassAsync();

        summary.Unchanged.ShouldBe(2);
        summary.Added.ShouldBe(0);
        summary.Updated.ShouldBe(0);
        await embedGen.DidNotReceive().GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondPass_EditedSource_ReEmbedsAndReplacesChunks()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = PublishedCourse();
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        var (svc, embedGen) = NewService(db);
        await svc.RunIndexPassAsync();
        var before = await db.AssistantContentChunks.ToListAsync();
        var enIdBefore = before.Single(c => c.Language == "en").Id;
        var elIdBefore = before.Single(c => c.Language == "el").Id;

        course.DescriptionEn = "A brand new description entirely.";
        await db.SaveChangesAsync();
        embedGen.ClearReceivedCalls();
        var summary = await svc.RunIndexPassAsync();

        summary.Updated.ShouldBe(1); // only the "en" text changed
        summary.Unchanged.ShouldBe(1); // "el" text (title only) unchanged
        var after = await db.AssistantContentChunks.ToListAsync();
        after.Single(c => c.Language == "en").Id.ShouldNotBe(enIdBefore);
        after.Single(c => c.Language == "el").Id.ShouldBe(elIdBefore);
        await embedGen.Received(1).GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondPass_UnpublishedSource_RemovesItsChunks()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = PublishedCourse();
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        var (svc, _) = NewService(db);
        await svc.RunIndexPassAsync();

        course.IsPublished = false;
        await db.SaveChangesAsync();
        var summary = await svc.RunIndexPassAsync();

        summary.Removed.ShouldBe(2); // en + el keys removed
        (await db.AssistantContentChunks.ToListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RunIndexPassAsync_PublishedLesson_UsesCourseAndModuleInHeader()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "Course A", IsPublished = true };
        var module = new Module { Id = Guid.NewGuid(), TitleEn = "Module A", CourseId = course.Id, Course = course };
        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            TitleEn = "Lesson A",
            ContentEn = "<p>Lesson body text.</p>",
            ModuleId = module.Id,
            Module = module,
            IsPublished = true
        };
        db.Courses.Add(course);
        db.Modules.Add(module);
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();
        var (svc, _) = NewService(db);

        await svc.RunIndexPassAsync();

        var lessonChunk = (await db.AssistantContentChunks.ToListAsync())
            .Single(c => c.SourceType == AssistantSourceType.Lesson && c.Language == "en");
        lessonChunk.Text.ShouldContain("Course A");
        lessonChunk.Text.ShouldContain("Module A");
        lessonChunk.Text.ShouldContain("Lesson body text.");
        lessonChunk.Text.ShouldNotContain("<p>");
    }

    [Fact]
    public async Task RunIndexPassAsync_UnpublishedLessonInPublishedCourse_IsSkipped()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "Course A", IsPublished = true };
        var module = new Module { Id = Guid.NewGuid(), TitleEn = "Module A", CourseId = course.Id, Course = course };
        var lesson = new Lesson
        {
            Id = Guid.NewGuid(), TitleEn = "Draft Lesson", ModuleId = module.Id, Module = module, IsPublished = false
        };
        db.Courses.Add(course);
        db.Modules.Add(module);
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();
        var (svc, _) = NewService(db);

        await svc.RunIndexPassAsync();

        (await db.AssistantContentChunks.ToListAsync()).ShouldNotContain(c => c.SourceType == AssistantSourceType.Lesson);
    }
}
