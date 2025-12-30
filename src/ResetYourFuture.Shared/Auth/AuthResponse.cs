namespace ResetYourFuture.Shared.Auth;

public class AuthResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? Expiration { get; set; }
    public string? Message { get; set; }
    public IEnumerable<string>? Errors { get; set; }
}
