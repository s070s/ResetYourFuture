using System.Threading.Channels;

namespace ResetYourFuture.Web.Logging;

public sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly ChannelWriter<string> _writer;

    public FileLogger(string categoryName, ChannelWriter<string> writer)
    {
        _categoryName = categoryName;
        _writer = writer;
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

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var level = logLevel.ToString().ToUpperInvariant();
        var message = formatter(state, exception);
        var entry = $"[{timestamp}] [{level}] [{_categoryName}] {message}";
        if (exception != null)
            entry += Environment.NewLine + exception;

        _writer.TryWrite(entry);
    }
}
