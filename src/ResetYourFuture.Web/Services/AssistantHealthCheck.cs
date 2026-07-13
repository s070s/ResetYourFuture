using Microsoft.Extensions.Diagnostics.HealthChecks;
using ResetYourFuture.Application.Common;

namespace ResetYourFuture.Web.Services;

/// <summary>Readiness check (AVAIL-1): surfaces the AI assistant's live state (see
/// <see cref="AssistantRuntimeState"/> / <see cref="OllamaBootstrapService"/>) without failing
/// overall readiness — the rest of the app serves fine while Ollama is unreachable or still
/// downloading models, so this reports Degraded rather than Unhealthy in those states.</summary>
public sealed class AssistantHealthCheck(AssistantRuntimeState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = state.Status switch
        {
            AssistantAvailability.Ready => HealthCheckResult.Healthy("Assistant ready."),
            AssistantAvailability.Disabled => HealthCheckResult.Healthy("Assistant disabled by configuration."),
            AssistantAvailability.DownloadingModels => HealthCheckResult.Degraded(
                $"Assistant downloading required models: {state.Progress}."),
            AssistantAvailability.OllamaUnreachable => HealthCheckResult.Degraded(
                $"Ollama unreachable: {state.Progress}."),
            _ => HealthCheckResult.Degraded("Assistant status unknown."),
        };

        return Task.FromResult(result);
    }
}
