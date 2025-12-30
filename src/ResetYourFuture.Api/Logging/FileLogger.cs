namespace ResetYourFuture.Api.Logging;

public sealed class FileLogger : ILogger
{
    private readonly string _logDirectory;
    private readonly string _categoryName;
    private static readonly Lock _lock = new();

    public FileLogger(string logDirectory, string categoryName)
    {
        _logDirectory = logDirectory;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var logFileName = $"log-{DateTime.Now:yyyy-MM-dd}.txt";
        var logFilePath = Path.Combine(_logDirectory, logFileName);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var level = logLevel.ToString().ToUpperInvariant();
        var message = formatter(state, exception);

        var logEntry = $"[{timestamp}] [{level}] [{_categoryName}] {message}";
        if (exception != null)
        {
            logEntry += Environment.NewLine + exception;
        }

        lock (_lock)
        {
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        }
    }
}
