using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.Auth;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Services;

/// <summary>
/// Auth service for Blazor WASM. 
/// Token storage: localStorage (justification below).
/// 
/// Why localStorage over sessionStorage or cookies:
/// - localStorage persists across tabs/browser restarts (better UX for web).
/// - HttpOnly cookies are more secure but require BFF pattern (complexity not justified at this stage).
/// - Mobile apps (future) will use secure storage; API remains unchanged.
/// - For production, consider short-lived access tokens + refresh token rotation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;

    private const string TokenKey = "authToken";
    private const string RefreshTokenKey = "refreshToken";
    private const string AdminTokenKey = "adminToken";
    private const string AdminRefreshTokenKey = "adminRefreshToken";

    public AuthService(
        HttpClient httpClient ,
        ILocalStorageService localStorage ,
        AuthenticationStateProvider authStateProvider )
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

    private const int MaxRetries = 2;
    private const int BaseDelayMs = 500;

    public async Task<AuthResponse> RegisterAsync( RegisterRequest request )
    {
        var response = await SendWithRetryAsync( () => _httpClient.PostAsJsonAsync( "api/auth/register" , request ) );
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result ?? new AuthResponse { Success = false , Message = "Unknown error" };
    }

    public async Task<AuthResponse> LoginAsync( LoginRequest request )
    {
        var response = await SendWithRetryAsync( () => _httpClient.PostAsJsonAsync( "api/auth/login" , request ) );
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

        if ( result?.Success == true && !string.IsNullOrEmpty( result.Token ) )
        {
            await _localStorage.SetItemAsStringAsync( TokenKey , result.Token );
            if ( !string.IsNullOrEmpty( result.RefreshToken ) )
            {
                await _localStorage.SetItemAsStringAsync( RefreshTokenKey , result.RefreshToken );
            }

            // Notify auth state changed
            ( ( JwtAuthStateProvider ) _authStateProvider ).NotifyUserAuthentication( result.Token );
        }

        return result ?? new AuthResponse { Success = false , Message = "Unknown error" };
    }

    /// <summary>
    /// Retries a request factory on transient network failures (browser fetch TypeError).
    /// Each retry calls the factory again, producing a fresh HttpRequestMessage.
    /// </summary>
    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> requestFactory )
    {
        for ( int attempt = 0; ; attempt++ )
        {
            try
            {
                return await requestFactory();
            }
            catch ( HttpRequestException ) when ( attempt < MaxRetries )
            {
                await Task.Delay( BaseDelayMs * ( attempt + 1 ) );
            }
        }
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync( TokenKey );
        await _localStorage.RemoveItemAsync( RefreshTokenKey );
        ( ( JwtAuthStateProvider ) _authStateProvider ).NotifyUserLogout();
    }

    public async Task<AuthResponse> ImpersonateAsync( string userId )
    {
        var response = await SendWithRetryAsync( () => _httpClient.PostAsJsonAsync( $"api/admin/users/{userId}/impersonate" , new { } ) );
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

        if ( result?.Success == true && !string.IsNullOrEmpty( result.Token ) )
        {
            var currentToken = await _localStorage.GetItemAsStringAsync( TokenKey );
            if ( !string.IsNullOrEmpty( currentToken ) )
                await _localStorage.SetItemAsStringAsync( AdminTokenKey , currentToken );

            var currentRefreshToken = await _localStorage.GetItemAsStringAsync( RefreshTokenKey );
            if ( !string.IsNullOrEmpty( currentRefreshToken ) )
                await _localStorage.SetItemAsStringAsync( AdminRefreshTokenKey , currentRefreshToken );

            await _localStorage.SetItemAsStringAsync( TokenKey , result.Token );
            await _localStorage.RemoveItemAsync( RefreshTokenKey );

            ( ( JwtAuthStateProvider ) _authStateProvider ).NotifyUserAuthentication( result.Token );
        }

        return result ?? new AuthResponse { Success = false , Message = "Unknown error" };
    }

    public async Task ExitImpersonationAsync()
    {
        var adminToken = await _localStorage.GetItemAsStringAsync( AdminTokenKey );
        if ( string.IsNullOrEmpty( adminToken ) ) return;

        await _localStorage.SetItemAsStringAsync( TokenKey , adminToken );
        await _localStorage.RemoveItemAsync( AdminTokenKey );

        var adminRefreshToken = await _localStorage.GetItemAsStringAsync( AdminRefreshTokenKey );
        if ( !string.IsNullOrEmpty( adminRefreshToken ) )
        {
            await _localStorage.SetItemAsStringAsync( RefreshTokenKey , adminRefreshToken );
            await _localStorage.RemoveItemAsync( AdminRefreshTokenKey );
        }
        else
        {
            await _localStorage.RemoveItemAsync( RefreshTokenKey );
        }

        ( ( JwtAuthStateProvider ) _authStateProvider ).NotifyUserAuthentication( adminToken );
    }

    public async Task<bool> IsImpersonatingAsync()
    {
        var adminToken = await _localStorage.GetItemAsStringAsync( AdminTokenKey );
        return !string.IsNullOrEmpty( adminToken );
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _localStorage.GetItemAsStringAsync( TokenKey );
        return !string.IsNullOrEmpty( token );
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsStringAsync( TokenKey );
    }
}
