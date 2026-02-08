using System.Net.Http.Headers;
using Blazored.LocalStorage;

namespace ResetYourFuture.Client.Services;

/// <summary>
/// DelegatingHandler that attaches JWT token to outgoing HTTP requests.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;

    public AuthHeaderHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // JS interop for localStorage can fail during early WASM lifecycle;
        // proceed without token rather than crashing the request pipeline.
        try
        {
            var token = await _localStorage.GetItemAsStringAsync("authToken");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (InvalidOperationException)
        {
            // JS interop not available yet (pre-render / early init)
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
