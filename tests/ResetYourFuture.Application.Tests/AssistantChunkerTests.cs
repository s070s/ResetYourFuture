using ResetYourFuture.Application.Common;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class AssistantChunkerTests
{
    [Fact]
    public void StripHtml_RemovesTagsAndDecodesEntities()
    {
        var html = "<p>Hello &amp; welcome to <strong>Reset</strong> Your Future.</p>";

        AssistantChunker.StripHtml(html).ShouldBe("Hello & welcome to Reset Your Future.");
    }

    [Fact]
    public void StripHtml_NullOrBlank_ReturnsEmpty()
    {
        AssistantChunker.StripHtml(null).ShouldBe(string.Empty);
        AssistantChunker.StripHtml("   ").ShouldBe(string.Empty);
    }

    [Fact]
    public void Chunk_EmptyInput_ReturnsEmpty()
    {
        AssistantChunker.Chunk("", "Header").ShouldBeEmpty();
        AssistantChunker.Chunk("   ", "Header").ShouldBeEmpty();
    }

    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunkPrefixedWithHeader()
    {
        var result = AssistantChunker.Chunk("short text", "Course: X");

        result.Count.ShouldBe(1);
        result[0].ShouldBe("Course: X\nshort text");
    }

    [Fact]
    public void Chunk_LongText_SplitsWithOverlapAndEachChunkHasHeader()
    {
        var text = new string('a', 1000);

        // Step is size - overlap = 250: windows start at 0, 250, 500, 750 — the last one
        // covers [750,1000) since size (300) would overrun the text.
        var result = AssistantChunker.Chunk(text, "H", size: 300, overlap: 50);

        result.Count.ShouldBe(4);
        foreach (var chunk in result)
            chunk.ShouldStartWith("H\n");

        result[^1].ShouldBe("H\n" + new string('a', 250));
    }

    [Fact]
    public void Chunk_OverlapGreaterThanOrEqualToSize_StillTerminates()
    {
        var text = new string('b', 50);

        var result = AssistantChunker.Chunk(text, "H", size: 10, overlap: 10);

        result.ShouldNotBeEmpty();
    }
}
