namespace ResetYourFuture.Web.Services;

/// <summary>
/// LOG-1: minimal error-surfacing so the logs are no longer purely write-only. Once a day (and
/// once shortly after startup) it scans the previous day's log file for error entries and, if any
/// were recorded, emits a single prominent WARN digest line — which also reaches the console/debug
/// sinks — instead of leaving the count buried in a file nobody opens.
///
/// A real "email the admin / alert" delivery needs a working email provider (the current
/// IEmailService is a dev stub with only confirmation/reset methods) and is the OTel path owned by
/// report 38 (OBS); this is the cheapest step that changes "invisible until someone opens the file".
/// </summary>
public sealed class LogErrorDigestService : BackgroundService
{
    private const string ErrorMarker = "] [ERROR] ";
    private readonly string _logDirectory;
    private readonly ILogger<LogErrorDigestService> _logger;

    public LogErrorDigestService(IWebHostEnvironment env, IConfiguration configuration, ILogger<LogErrorDigestService> logger)
    {
        // Same directory the file logger writes to: the configured Logging:File:Directory (CLOUD-1)
        // or the content-root default (LOG-6). Kept in sync with Program.cs's resolution.
        var configuredLogDir = configuration["Logging:File:Directory"];
        _logDirectory = string.IsNullOrWhiteSpace(configuredLogDir)
            ? Path.Combine(env.ContentRootPath, "Logs")
            : configuredLogDir;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup logging settle before the first digest.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            DigestPreviousDay();
            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void DigestPreviousDay()
    {
        try
        {
            var date = DateTime.UtcNow.Date.AddDays(-1);
            var file = Path.Combine(_logDirectory, $"log-{date:yyyy-MM-dd}.txt");
            if (!File.Exists(file))
                return;

            var errorCount = 0;
            foreach (var line in File.ReadLines(file))
            {
                if (line.Contains(ErrorMarker, StringComparison.Ordinal))
                    errorCount++;
            }

            if (errorCount > 0)
            {
                _logger.LogWarning(
                    "Log digest: {ErrorCount} error(s) were recorded on {Date:yyyy-MM-dd}. Review {File}.",
                    errorCount, date, file);
            }
        }
        catch (Exception ex)
        {
            // Never let the digest itself become a source of instability.
            _logger.LogWarning(ex, "Log digest: failed to scan the previous day's log file.");
        }
    }
}
