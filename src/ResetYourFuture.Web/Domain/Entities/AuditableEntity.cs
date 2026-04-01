namespace ResetYourFuture.Web.Domain.Entities;

/// <summary>
/// Base class for entities that require audit tracking and publishing workflow.
/// </summary>
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? UpdatedAt { get; set; }
    
    public bool IsPublished { get; set; } = false;
    
    public DateTimeOffset? PublishedAt { get; set; }
    
    public string? UpdatedByUserId { get; set; }
}
