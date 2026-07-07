using System.Net;
using System.Text.RegularExpressions;

namespace ResetYourFuture.Application.Common;

/// <summary>
/// Pure text chunking for the AI assistant's content index: strips HTML from rich-text lesson/blog
/// content and splits it into overlapping windows sized for the embedding model's context.
/// </summary>
public static partial class AssistantChunker
{
    private const int DefaultChunkSize = 800;
    private const int DefaultOverlap = 100;

    /// <summary>Strips HTML tags and decodes entities, collapsing whitespace to single spaces.</summary>
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withoutTags = HtmlTagRegex().Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    /// <summary>
    /// Splits <paramref name="text"/> into overlapping windows of roughly <paramref name="size"/>
    /// characters, each prefixed with <paramref name="metadataHeader"/> so the embedding captures
    /// which course/module/etc. the chunk belongs to.
    ///
    /// NOTE: once categories exist on Course/AssessmentDefinition (see feature/categories), append
    /// the category name to metadataHeader here so the next re-index picks it up with no schema change.
    /// </summary>
    public static IReadOnlyList<string> Chunk(string text, string metadataHeader, int size = DefaultChunkSize, int overlap = DefaultOverlap)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var header = string.IsNullOrWhiteSpace(metadataHeader) ? string.Empty : metadataHeader.Trim() + "\n";
        var step = Math.Max(1, size - overlap);
        var chunks = new List<string>();

        for (var start = 0; start < text.Length; start += step)
        {
            var length = Math.Min(size, text.Length - start);
            chunks.Add(header + text.Substring(start, length));

            if (start + length >= text.Length)
                break;
        }

        return chunks;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
