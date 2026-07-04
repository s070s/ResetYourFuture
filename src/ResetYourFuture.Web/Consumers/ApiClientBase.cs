using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResetYourFuture.Web.Services;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Shared HTTP helper methods for all typed API consumers.
///
/// Before every request the bearer token is attached from <see cref="ApiTokenProvider"/>, so calls
/// authenticate even inside the Blazor Server circuit where HttpContext (and therefore
/// <c>SsrApiHandler</c>) is unavailable. Without this, interactive calls went out unauthenticated,
/// the API answered 401, and these helpers silently returned default — appearing to the user as
/// blank/empty pages or no-op actions.
/// </summary>
public abstract class ApiClientBase
{
    protected readonly HttpClient Http;
    private readonly ApiTokenProvider _tokenProvider;

    protected ApiClientBase(HttpClient http, ApiTokenProvider tokenProvider)
    {
        Http = http;
        _tokenProvider = tokenProvider;
    }

    /// <summary>
    /// Attaches a freshly minted bearer token (or clears it when anonymous) to the consumer's
    /// <see cref="HttpClient"/> before a request. The token is identical for every call within a
    /// circuit, so mutating the default header is safe on the single-threaded circuit dispatcher.
    /// Call this from any bespoke method that uses <see cref="Http"/> directly instead of the
    /// helpers below.
    /// </summary>
    protected async Task EnsureAuthorizationAsync()
    {
        var token = await _tokenProvider.GetTokenAsync();
        Http.DefaultRequestHeaders.Authorization =
            string.IsNullOrEmpty(token) ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    protected async Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.GetAsync(url, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            : default;
    }

    protected async Task<byte[]?> GetBytesAsync(string url, CancellationToken ct = default)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.GetAsync(url, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync(ct)
            : null;
    }

    protected async Task<T?> PostAsync<T>(string url, CancellationToken ct = default)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.PostAsync(url, null, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            : default;
    }

    protected async Task<bool> ActionAsync(string url, CancellationToken ct = default)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.PostAsync(url, null, ct);
        return response.IsSuccessStatusCode;
    }

    protected async Task<TResult?> PostJsonAsync<TBody, TResult>(string url, TBody body, CancellationToken ct = default)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.PostAsJsonAsync(url, body, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: ct)
            : default;
    }

    protected async Task<bool> PostJsonActionAsync<TBody>(string url, TBody body, CancellationToken ct = default)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.PostAsJsonAsync(url, body, ct);
        return response.IsSuccessStatusCode;
    }

    protected async Task<TResult?> PutJsonAsync<TBody, TResult>(string url, TBody body, CancellationToken ct = default)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.PutAsJsonAsync(url, body, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: ct)
            : default;
    }

    protected async Task<bool> DeleteAsync(string url, CancellationToken ct = default)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.DeleteAsync(url, ct);
        return response.IsSuccessStatusCode;
    }

    protected async Task<TResult?> PostFormAsync<TResult>(string url, HttpContent form, CancellationToken ct = default)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.PostAsync(url, form, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: ct)
            : default;
    }

    protected async Task<bool> PostFormActionAsync(string url, HttpContent form, CancellationToken ct = default)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.PostAsync(url, form, ct);
        return response.IsSuccessStatusCode;
    }
}
