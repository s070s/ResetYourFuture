using ResetYourFuture.Shared.Auth;

namespace ResetYourFuture.Client.Interfaces;

/// <summary>
/// Authentication service interface for client-side auth operations.
/// </summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetTokenAsync();
    Task<AuthResponse> ImpersonateAsync(string userId);
    Task ExitImpersonationAsync();
    Task<bool> IsImpersonatingAsync();
}
