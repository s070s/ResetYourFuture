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

        string[] lines;
        try
        {
            lines = File.ReadAllLines(envFilePath);
        }
        catch (Exception ex)
        {
            // This runs before the host (and logging) exist — write to the console so an
            // unreadable/locked .env is a clear message, not a raw unhandled exception (CFG-3).
            Console.WriteLine($"[EnvFileLoader] Could not read '{envFilePath}': {ex.Message}");
            return;
        }

        // Logging isn't up yet; surface which file won so a stray .env in a parent directory
        // isn't a silent mystery (CFG-3).
        Console.WriteLine($"[EnvFileLoader] Loading environment variables from '{envFilePath}'.");

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;
            var eq = trimmed.IndexOf('=');
            if (eq < 1)
                continue;

            var key = trimmed[..eq].Trim();
            var value = StripSurroundingQuotes(trimmed[(eq + 1)..].Trim());

            // Conventional dotenv precedence: a real environment variable set by the host/operator
            // wins over the .env file, so a forgotten .env can't silently override deployed config
            // (CFG-3 — the previous behaviour overwrote it unconditionally).
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string StripSurroundingQuotes(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];
        return value;
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
