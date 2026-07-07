using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// A chunk of embedded, published-content text used to ground the AI assistant's answers.
/// Purely derived data (re-indexed from Courses/Lessons/Assessments/BlogArticles), so this is
/// a plain entity rather than an <see cref="AuditableEntity"/>: rows are hard-deleted and
/// recreated whenever their source content changes, not soft-deleted.
/// </summary>
public class AssistantContentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public AssistantSourceType SourceType { get; set; }

    public Guid SourceId { get; set; }

    /// <summary>
    /// ISO 639-1 language code the chunk text is written in ("en" or "el").
    /// </summary>
    public required string Language { get; set; }

    /// <summary>
    /// Position of this chunk within its source's chunked text, for stable ordering.
    /// </summary>
    public int ChunkIndex { get; set; }

    public required string Text { get; set; }

    /// <summary>
    /// Embedding vector for <see cref="Text"/>, stored as the raw bytes of a float[] array.
    /// </summary>
    public required byte[] Embedding { get; set; }

    /// <summary>
    /// SHA-256 hash (hex) of the source text this chunk set was generated from, used to skip
    /// re-embedding unchanged content on subsequent index passes.
    /// </summary>
    public required string ContentHash { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
