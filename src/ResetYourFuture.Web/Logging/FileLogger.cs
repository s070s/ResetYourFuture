using System.Threading.Channels;

namespace ResetYourFuture.Web.Logging;

public sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly ChannelWriter<string> _writer;
    private readonly Action _onDropped;

    public FileLogger(string categoryName, ChannelWriter<string> writer, Action onDropped)
    {
        _categoryName = categoryName;
        _writer = writer;
        _onDropped = onDropped;
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

        // LOG-3: the channel is bounded; when it is full the write is dropped. Record the drop so
        // the writer can emit a "[WARN] N entries dropped" marker instead of losing entries silently.
        if (!_writer.TryWrite(entry))
            _onDropped();
    }
}
