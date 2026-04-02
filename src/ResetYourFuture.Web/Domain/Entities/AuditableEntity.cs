namespace ResetYourFuture.Web.Domain.Entities;

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
}
