namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Exposes the identity of the caller behind the current request so services can attribute
/// audit-log lines to the acting user (LOG-4) without threading the id through every method
/// signature. Implemented in the Web layer over the request's authenticated principal.
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>The authenticated caller's user id, or null when there is no authenticated request.</summary>
    string? UserId { get; }
}
