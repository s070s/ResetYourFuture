using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Shared HTTP helper methods for all typed API consumers.
/// </summary>
public abstract class ApiClientBase
{
    protected readonly HttpClient Http;

    protected ApiClientBase( HttpClient http ) => Http = http;

    protected async Task<T?> GetAsync<T>( string url, CancellationToken ct = default )
    {
        var response = await Http.GetAsync( url, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<T>( cancellationToken: ct )
            : default;
    }

    protected async Task<byte[]?> GetBytesAsync( string url, CancellationToken ct = default )
    {
        var response = await Http.GetAsync( url, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync( ct )
            : null;
    }

    protected async Task<T?> PostAsync<T>( string url, CancellationToken ct = default )
    {
        var response = await Http.PostAsync( url, null, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<T>( cancellationToken: ct )
            : default;
    }

    protected async Task<bool> ActionAsync( string url, CancellationToken ct = default )
    {
        var response = await Http.PostAsync( url, null, ct );
        return response.IsSuccessStatusCode;
    }

    protected async Task<TResult?> PostJsonAsync<TBody, TResult>( string url, TBody body, CancellationToken ct = default )
    {
        var response = await Http.PostAsJsonAsync( url, body, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TResult>( cancellationToken: ct )
            : default;
    }

    protected async Task<bool> PostJsonActionAsync<TBody>( string url, TBody body, CancellationToken ct = default )
    {
        var response = await Http.PostAsJsonAsync( url, body, ct );
        return response.IsSuccessStatusCode;
    }

    protected async Task<TResult?> PutJsonAsync<TBody, TResult>( string url, TBody body, CancellationToken ct = default )
    {
        var response = await Http.PutAsJsonAsync( url, body, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TResult>( cancellationToken: ct )
            : default;
    }

    protected async Task<bool> DeleteAsync( string url, CancellationToken ct = default )
    {
        var response = await Http.DeleteAsync( url, ct );
        return response.IsSuccessStatusCode;
    }

    protected async Task<TResult?> PostFormAsync<TResult>( string url, HttpContent form, CancellationToken ct = default )
    {
        var response = await Http.PostAsync( url, form, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TResult>( cancellationToken: ct )
            : default;
    }

    protected async Task<bool> PostFormActionAsync( string url, HttpContent form, CancellationToken ct = default )
    {
        var response = await Http.PostAsync( url, form, ct );
        return response.IsSuccessStatusCode;
    }
}
