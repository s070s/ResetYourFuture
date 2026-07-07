namespace ResetYourFuture.Application.Common;

/// <summary>
/// Singleton counter bumped after each successful assistant index pass, so
/// <c>AssistantRetrievalService</c> knows when its in-memory chunk cache is stale.
/// </summary>
public sealed class AssistantIndexVersion
{
    private int _value;

    public int Current => _value;

    public void Bump() => Interlocked.Increment(ref _value);
}
