using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace ResetYourFuture.Api.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly Channel<string> _channel;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly Task _writerTask;

    public FileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
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
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _channel.Writer));

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _loggers.Clear();
    }
}
