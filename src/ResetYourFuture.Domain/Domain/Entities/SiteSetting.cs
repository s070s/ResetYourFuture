namespace ResetYourFuture.Web.Domain.Entities;

/// <summary>
/// Stores site-wide configuration settings.
/// Key-value pairs for flexible configuration (e.g., landing background image).
/// </summary>
public class SiteSetting
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Unique setting key (e.g., "LandingBackgroundImage").
    /// </summary>
    public required string Key { get; set; }
    
    /// <summary>
    /// Setting value (file path, JSON, or plain text).
    /// </summary>
    public string? Value { get; set; }
    
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// User who last updated this setting (admin).
    /// </summary>
    public string? UpdatedByUserId { get; set; }
}
