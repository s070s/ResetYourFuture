namespace ResetYourFuture.Web.Startup;

/// <summary>
/// Loads a .env file into process environment variables if present, so the env-vars
/// configuration provider picks up secrets automatically.
/// Copy .env.template → .env and fill in secrets. Never commit .env.
/// </summary>
public static class EnvFileLoader
{
    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for a .env file, so it's found
    /// whether the app is launched from the solution root (dotnet run --project …) or the
    /// project directory (VS / Rider), then loads any KEY=VALUE lines into the environment.
    /// </summary>
    public static void LoadIfPresent(string startDirectory)
    {
        var envFilePath = FindEnvFile(startDirectory);
        if (envFilePath is null)
            return;

        foreach (var line in File.ReadAllLines(envFilePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;
            var eq = trimmed.IndexOf('=');
            if (eq < 1)
                continue;
            Environment.SetEnvironmentVariable(
                trimmed[..eq].Trim(),
                trimmed[(eq + 1)..].Trim());
        }
    }

    private static string? FindEnvFile(string start)
    {
        var dir = start;
        for (var i = 0; i < 5; i++)
        {
            var candidate = Path.Combine(dir, ".env");
            if (File.Exists(candidate))
                return candidate;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir)
                break;
            dir = parent;
        }
        return null;
    }
}
