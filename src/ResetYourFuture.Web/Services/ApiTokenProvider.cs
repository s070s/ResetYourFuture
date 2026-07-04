using Microsoft.AspNetCore.Components.Authorization;
using ResetYourFuture.Web.Interfaces;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Circuit-scoped provider of the API bearer token.
///
/// Reads the current user from <see cref="AuthenticationStateProvider"/> — which works during both
/// prerender and the interactive Blazor Server circuit, unlike <c>IHttpContextAccessor</c> — and
/// mints a short-lived JWT from their claims via <see cref="IAuthService.GetTokenAsync(System.Security.Claims.ClaimsPrincipal)"/>.
///
/// This lets the server-side API consumers (<see cref="Consumers.ApiClientBase"/>) authenticate
/// from inside the circuit, where HttpContext — and therefore <see cref="SsrApiHandler"/> — is null.
/// Previously those calls went out unauthenticated, the API answered 401, and the consumer helpers
/// silently returned null, which surfaced to the user as empty/blank pages.
/// </summary>
public sealed class ApiTokenProvider( AuthenticationStateProvider authStateProvider, IAuthService authService )
{
    /// <summary>
    /// Returns a freshly minted bearer token for the current user, or <c>null</c> when the request is
    /// anonymous. A fresh token is minted per call so it cannot age out during a long-lived circuit
    /// (token building is a cheap claim re-sign with no database access).
    /// </summary>
    public async Task<string?> GetTokenAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        return await authService.GetTokenAsync( state.User );
    }
}
