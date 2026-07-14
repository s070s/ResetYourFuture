using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace ResetYourFuture.Web.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly Channel<string> _channel;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly Task _writerTask;
    private int _droppedSinceLastReport;

    public FileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
        // LOG-3: DropWrite (not DropOldest) so a full buffer makes TryWrite return false — that lets
        // FileLogger count the drop, and the writer emits a "[WARN] N entries dropped" marker rather
        // than losing entries with no trace.
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
        _writerTask = Task.Run(WriteLoopAsync);
    }

    private async Task WriteLoopAsync()
    {
        string? currentFile = null;
        StreamWriter? writer = null;
        try
        {
            var reader = _channel.Reader;
            while (await reader.WaitToReadAsync())
            {
                var logFile = Path.Combine(_logDirectory, $"log-{DateTime.UtcNow:yyyy-MM-dd}.txt");
                if (logFile != currentFile)
                {
                    if (writer is not null)
                    {
                        await writer.FlushAsync();
                        await writer.DisposeAsync();
                    }
                    writer = new StreamWriter(logFile, append: true, Encoding.UTF8, bufferSize: 4096) { AutoFlush = false };
                    currentFile = logFile;
                }

                // Drain all queued entries, then flush once
                while (reader.TryRead(out var entry))
                    await writer!.WriteLineAsync(entry);

                // LOG-3: if entries were dropped while the buffer was full, leave a marker so the
                // record is visibly incomplete rather than silently short.
                var dropped = Interlocked.Exchange(ref _droppedSinceLastReport, 0);
                if (dropped > 0)
                {
                    var ts = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    await writer!.WriteLineAsync(
                        $"[{ts}] [WARN] [FileLogger] {dropped} log entr{(dropped == 1 ? "y" : "ies")} dropped (write buffer full).");
                }

                await writer!.FlushAsync();
            }
        }
        finally
        {
            if (writer is not null)
            {
                await writer.FlushAsync();
                await writer.DisposeAsync();
            }
        }
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName,
            name => new FileLogger(name, _channel.Writer, () => Interlocked.Increment(ref _droppedSinceLastReport)));

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        // LOG-3: wait (bounded) for the writer to flush entries still queued at shutdown — including
        // the exception that may have just crashed the app — instead of dropping the tail.
        try { _writerTask.Wait(TimeSpan.FromSeconds(2)); }
        catch { /* best-effort drain; never throw from Dispose */ }
        _loggers.Clear();
    }
}
