using System.Security.Claims;
using ResetYourFuture.Application.ApiInterfaces;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Resolves the acting user's id from the current HTTP request's authenticated principal.
/// Admin actions arrive as HTTP requests to the API controllers (the loopback call carries the
/// admin's JWT), so the claim is present here — see <see cref="ICurrentUserAccessor"/> (LOG-4).
/// </summary>
public sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public string? UserId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
