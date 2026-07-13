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
///
/// Every request also runs through <see cref="ExecuteAsync{T}"/> (AVAIL-3): a network-level failure
/// on the loopback call (connection refused/reset during a redeploy, or the request-level timeout
/// configured on the client) is caught and degraded to <c>default</c>/<c>false</c> instead of
/// propagating into the calling Razor component, which in Blazor Server would tear down the whole
/// circuit and show the generic "reload" error banner. Idempotent GETs additionally get a couple of
/// fast retries so a momentary connection blip recovers instead of blanking the page; non-idempotent
/// verbs are never retried (a POST that timed out may already have been processed server-side).
/// </summary>
public abstract class ApiClientBase
{
    private const int MaxGetAttempts = 3;

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

    /// <summary>
    /// Sends a request and reads its response, degrading network failures to <paramref name="fallback"/>
    /// rather than letting them crash the circuit. <paramref name="retryable"/> should be true only for
    /// idempotent requests; retries fire on fast-failing connection errors, never on timeouts.
    /// </summary>
    private async Task<T?> ExecuteAsync<T>(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        Func<HttpResponseMessage, CancellationToken, Task<T?>> read,
        bool retryable,
        T? fallback,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await EnsureAuthorizationAsync();
                using var response = await send(ct);
                return response.IsSuccessStatusCode ? await read(response, ct) : fallback;
            }
            catch (HttpRequestException) when (retryable && attempt < MaxGetAttempts)
            {
                // Connection refused/reset fails fast (no timeout wait) — a brief blip during a
                // redeploy usually clears within a retry or two.
                await Task.Delay(RetryDelay(attempt), ct);
            }
            catch (Exception ex) when (IsNetworkFailure(ex, ct))
            {
                // Final connection failure, or a request-level timeout (never retried, to avoid
                // stacking waits and re-issuing a possibly-applied write): degrade, don't crash.
                return fallback;
            }
        }
    }

    // A caller-cancellation surfaces as cancellation (rethrown); an HttpClient timeout raises a
    // TaskCanceledException whose token is the internal timeout token, not the caller's.
    private static bool IsNetworkFailure(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException ||
        (ex is TaskCanceledException && !ct.IsCancellationRequested);

    private static TimeSpan RetryDelay(int attempt) =>
        TimeSpan.FromMilliseconds(100 * attempt + Random.Shared.Next(0, 50));

    protected Task<T?> GetAsync<T>(string url, CancellationToken ct = default) =>
        ExecuteAsync(c => Http.GetAsync(url, c),
            (r, c) => r.Content.ReadFromJsonAsync<T>(cancellationToken: c), retryable: true, fallback: default, ct);

    protected Task<byte[]?> GetBytesAsync(string url, CancellationToken ct = default) =>
        ExecuteAsync<byte[]?>(c => Http.GetAsync(url, c),
            async (r, c) => await r.Content.ReadAsByteArrayAsync(c), retryable: true, fallback: null, ct);

    protected Task<T?> PostAsync<T>(string url, CancellationToken ct = default) =>
        ExecuteAsync(c => Http.PostAsync(url, null, c),
            (r, c) => r.Content.ReadFromJsonAsync<T>(cancellationToken: c), retryable: false, fallback: default, ct);

    protected Task<bool> ActionAsync(string url, CancellationToken ct = default) =>
        ExecuteAsync(c => Http.PostAsync(url, null, c),
            (_, _) => Task.FromResult(true), retryable: false, fallback: false, ct);

    protected Task<TResult?> PostJsonAsync<TBody, TResult>(string url, TBody body, CancellationToken ct = default) =>
        ExecuteAsync(c => Http.PostAsJsonAsync(url, body, c),
            (r, c) => r.Content.ReadFromJsonAsync<TResult>(cancellationToken: c), retryable: false, fallback: default, ct);

    protected Task<bool> PostJsonActionAsync<TBody>(string url, TBody body, CancellationToken ct = default) =>
        ExecuteAsync(c => Http.PostAsJsonAsync(url, body, c),
            (_, _) => Task.FromResult(true), retryable: false, fallback: false, ct);

    protected Task<TResult?> PutJsonAsync<TBody, TResult>(string url, TBody body, CancellationToken ct = default) =>
        ExecuteAsync(c => Http.PutAsJsonAsync(url, body, c),
            (r, c) => r.Content.ReadFromJsonAsync<TResult>(cancellationToken: c), retryable: false, fallback: default, ct);

    protected Task<bool> DeleteAsync(string url, CancellationToken ct = default) =>
        ExecuteAsync(c => Http.DeleteAsync(url, c),
            (_, _) => Task.FromResult(true), retryable: false, fallback: false, ct);

    protected Task<TResult?> PostFormAsync<TResult>(string url, HttpContent form, CancellationToken ct = default) =>
        ExecuteAsync(c => Http.PostAsync(url, form, c),
            (r, c) => r.Content.ReadFromJsonAsync<TResult>(cancellationToken: c), retryable: false, fallback: default, ct);

    protected Task<bool> PostFormActionAsync(string url, HttpContent form, CancellationToken ct = default) =>
        ExecuteAsync(c => Http.PostAsync(url, form, c),
            (_, _) => Task.FromResult(true), retryable: false, fallback: false, ct);
}
