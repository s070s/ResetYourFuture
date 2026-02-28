using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Interfaces;

/// <summary>
/// Authentication service interface for client-side auth operations.
/// </summary>
public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync( RegisterRequestDto request );
    Task<AuthResponseDto> LoginAsync( LoginRequestDto request );
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetTokenAsync();
    Task<AuthResponseDto> ImpersonateAsync( string userId );
    Task ExitImpersonationAsync();
    Task<bool> IsImpersonatingAsync();
}
