namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// Base class for entities that require audit tracking and publishing workflow.
/// </summary>
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedByUserId { get; set; }

    public bool IsPublished { get; set; } = false;

    public DateTimeOffset? PublishedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// DB-7: DB-generated concurrency token (SQL Server <c>rowversion</c>). EF Core includes it
    /// in every UPDATE's WHERE clause automatically for an entity loaded and saved within the
    /// same DbContext scope — no DTO or client round-trip needed. A second concurrent
    /// read-modify-write against the same row (from a different request/DbContext) then throws
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> instead of
    /// silently overwriting the first writer's change; <c>ConcurrencyExceptionHandler</c> maps
    /// that to 409 Conflict.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
