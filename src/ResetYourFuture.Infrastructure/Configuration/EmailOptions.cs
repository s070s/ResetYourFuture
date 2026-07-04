namespace ResetYourFuture.Infrastructure.Configuration;

/// <summary>Binds the "Email" configuration section. Secrets (Username/Password) come from
/// User Secrets / environment variables, never committed appsettings.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@reset-your-future.com";
    public string FromName { get; set; } = "Reset Your Future";
}
