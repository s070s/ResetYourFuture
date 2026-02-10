using System.Net;
using System.Net.Http.Headers;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

namespace ResetYourFuture.Client.Services;

/// <summary>
/// DelegatingHandler that attaches JWT token to outgoing HTTP requests.
/// Also detects disabled-user responses and forces redirect to /disabled.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _navigation;

    public AuthHeaderHandler(ILocalStorageService localStorage, NavigationManager navigation)
    {
        _localStorage = localStorage;
        _navigation = navigation;
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

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            response.Headers.Contains("X-User-Disabled"))
        {
            try
            {
                await _localStorage.RemoveItemAsync("authToken");
                await _localStorage.RemoveItemAsync("refreshToken");
            }
            catch (InvalidOperationException) { }

            _navigation.NavigateTo("/disabled", forceLoad: true);
        }

        return response;
    }
}
