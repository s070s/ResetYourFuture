using Microsoft.Extensions.AI;
using NSubstitute;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class AssistantRetrievalServiceTests
{
    private static (AssistantRetrievalService svc, IEmbeddingGenerator<string, Embedding<float>> embedGen) NewService(
        ApplicationDbContext db, float[] queryVector)
    {
        var embedGen = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        embedGen.GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_ => new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(queryVector)]));

        var svc = new AssistantRetrievalService(db, embedGen, new AssistantChunkCache(), new AssistantIndexVersion());
        return (svc, embedGen);
    }

    private static AssistantContentChunk Chunk(AssistantSourceType type, Guid sourceId, string lang, string text, float[] vector, int index = 0) => new()
    {
        Id = Guid.NewGuid(),
        SourceType = type,
        SourceId = sourceId,
        Language = lang,
        ChunkIndex = index,
        Text = text,
        Embedding = EmbeddingCodec.ToBytes(vector),
        ContentHash = "hash",
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task SearchAsync_RanksClosestVectorFirstAndExcludesBelowThreshold()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "Course A", IsPublished = true };
        db.Courses.Add(course);
        db.AssistantContentChunks.Add(Chunk(AssistantSourceType.Course, course.Id, "en", "close match", [1f, 0f, 0f]));
        db.AssistantContentChunks.Add(Chunk(AssistantSourceType.Course, course.Id, "en", "orthogonal match", [0f, 1f, 0f], index: 1));
        await db.SaveChangesAsync();
        var (svc, _) = NewService(db, [1f, 0f, 0f]);

        var results = await svc.SearchAsync("query", "en", topK: 5);

        // "orthogonal match" scores cosine 0.0, below the 0.4 threshold, so only one result.
        results.ShouldHaveSingleItem();
        results[0].Text.ShouldBe("close match");
        results[0].SourceTitle.ShouldBe("Course A");
        results[0].SourceUrl.ShouldBe($"courses/{course.Id}");
    }

    [Fact]
    public async Task SearchAsync_FiltersByLanguage()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "Course A", TitleEl = "Μάθημα Α", IsPublished = true };
        db.Courses.Add(course);
        db.AssistantContentChunks.Add(Chunk(AssistantSourceType.Course, course.Id, "en", "english text", [1f, 0f, 0f]));
        db.AssistantContentChunks.Add(Chunk(AssistantSourceType.Course, course.Id, "el", "greek text", [1f, 0f, 0f], index: 1));
        await db.SaveChangesAsync();
        var (svc, _) = NewService(db, [1f, 0f, 0f]);

        var results = await svc.SearchAsync("query", "el", topK: 5);

        results.ShouldHaveSingleItem();
        results[0].Text.ShouldBe("greek text");
        results[0].SourceTitle.ShouldBe("Μάθημα Α");
    }

    [Fact]
    public async Task SearchAsync_NothingAboveThreshold_ReturnsEmpty()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "Course A", IsPublished = true };
        db.Courses.Add(course);
        db.AssistantContentChunks.Add(Chunk(AssistantSourceType.Course, course.Id, "en", "unrelated", [0f, 1f, 0f]));
        await db.SaveChangesAsync();
        var (svc, _) = NewService(db, [1f, 0f, 0f]);

        (await svc.SearchAsync("query", "en", topK: 5)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ChunkForDeletedOrMissingSource_IsSkipped()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.AssistantContentChunks.Add(Chunk(AssistantSourceType.Course, Guid.NewGuid(), "en", "orphaned chunk", [1f, 0f, 0f]));
        await db.SaveChangesAsync();
        var (svc, _) = NewService(db, [1f, 0f, 0f]);

        (await svc.SearchAsync("query", "en", topK: 5)).ShouldBeEmpty();
    }
}
