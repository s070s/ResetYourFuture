using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Periodically re-indexes published content for the AI assistant (see
/// <see cref="IAssistantIndexingService"/>). Runs shortly after startup, then every 6 hours, or
/// immediately when <see cref="AssistantIndexSignal"/> fires (admin "reindex now"). A failed pass
/// (e.g. Ollama unreachable) is logged and retried on the next tick — it never crashes the host.
/// </summary>
public sealed class AssistantIndexer(
    IServiceProvider services,
    AssistantIndexSignal signal,
    AssistantIndexVersion version,
    AssistantRuntimeState runtimeState,
    ILogger<AssistantIndexer> logger) : BackgroundService
{
    private static readonly TimeSpan ReadyPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the Ollama bootstrap to report Ready (models present) instead of a blind
        // startup delay — the first index pass then succeeds instead of failing and waiting 6h.
        while (runtimeState.Status != AssistantAvailability.Ready && !stoppingToken.IsCancellationRequested)
            await Task.Delay(ReadyPollInterval, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var indexingService = scope.ServiceProvider.GetRequiredService<IAssistantIndexingService>();
                await indexingService.RunIndexPassAsync(stoppingToken);
                version.Bump();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AssistantIndexer: index pass failed (Ollama unreachable?) — will retry.");
            }

            await signal.WaitAsync(Interval, stoppingToken);
        }
    }
}
