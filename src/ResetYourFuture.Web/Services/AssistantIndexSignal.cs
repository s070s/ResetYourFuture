using System.Threading.Channels;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Lets the admin "reindex now" endpoint wake up <see cref="AssistantIndexer"/> without waiting for
/// its next timed pass.
/// </summary>
public sealed class AssistantIndexSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void RequestReindex() => _channel.Writer.TryWrite(true);

    /// <summary>Waits for a signal or for <paramref name="timeout"/> to elapse, whichever comes first.</summary>
    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await _channel.Reader.ReadAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout elapsed with no signal — fall through to the next timed pass.
        }
    }
}
