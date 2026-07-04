namespace ResetYourFuture.Application.DTOs;

public class AuthResponseDto
{
    /// <summary>True if the operation succeeded.</summary>
    /// <example>true</example>
    public bool Success
    {
        get; set;
    }

    /// <summary>JWT access token (present on successful login/refresh).</summary>
    /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyIn0.sig</example>
    public string? Token
    {
        get; set;
    }

    /// <summary>Opaque refresh token used to obtain a new access token.</summary>
    /// <example>9f8c1e2b7a6d4f3e0b5c8a1d2e3f4a5b</example>
    public string? RefreshToken
    {
        get; set;
    }

    /// <summary>UTC expiry of the access token.</summary>
    /// <example>2026-06-05T12:30:00Z</example>
    public DateTime? Expiration
    {
        get; set;
    }

    /// <summary>Human-readable status message.</summary>
    /// <example>Registration successful. Please check your email to confirm your account.</example>
    public string? Message
    {
        get; set;
    }

    /// <summary>Validation or error messages, when <see cref="Success"/> is false.</summary>
    public IEnumerable<string>? Errors
    {
        get; set;
    }
}
