using System.Text;

namespace ResetYourFuture.Web.Startup;

/// <summary>
/// Collects every missing/invalid required startup config value into a single failure
/// instead of the app dying on the first one, restarting, then dying on the next
/// (MAINT-5) — Jwt:Key, AdminUser:Password, Email:Smtp:Host (non-Development), and
/// SeedData:StudentPassword (Development with SeedData:Enabled=true) each already throw
/// their own <see cref="InvalidOperationException"/> deeper in startup; this runs first so a
/// contributor sees every missing key at once instead of one per restart. Those individual
/// checks are left in place as defense in depth.
/// </summary>
public static class StartupConfigValidation
{
    public static WebApplicationBuilder ValidateRequiredConfig(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var errors = new List<string>();

        var jwtKey = config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
            errors.Add("Jwt:Key is required. Set it via User Secrets (dev) or environment variable Jwt__Key (prod).");
        else if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
            errors.Add("Jwt:Key must be at least 32 bytes for HMAC-SHA256 security.");

        if (string.IsNullOrWhiteSpace(config["AdminUser:Password"]))
            errors.Add("AdminUser:Password is required. Set it via User Secrets (dev) or environment variable AdminUser__Password (prod).");

        if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(config["Email:Smtp:Host"]))
            errors.Add("Email:Smtp:Host is required outside Development. Set it (and SMTP credentials) for production.");

        if (builder.Environment.IsDevelopment()
            && config.GetValue<bool>("SeedData:Enabled")
            && string.IsNullOrWhiteSpace(config["SeedData:StudentPassword"]))
        {
            errors.Add("SeedData:StudentPassword is required when SeedData:Enabled=true. Set it via User Secrets.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Startup configuration is incomplete:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(e => "  - " + e)));

        return builder;
    }
}
