using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using ResetYourFuture.Application.Common;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Supervises the Ollama sidecar so a fresh machine needs zero manual model management:
/// probes <c>Assistant:BaseUrl</c>, auto-pulls any missing chat/embedding model (unless
/// <c>Assistant:AutoPullModels</c> is off), and drives <see cref="AssistantRuntimeState"/>
/// through OllamaUnreachable → DownloadingModels → Ready. Keeps supervising after Ready so
/// an Ollama restart (or late install) is detected and recovered from without an app restart.
/// Never throws out of <see cref="ExecuteAsync"/>.
/// </summary>
public sealed class OllamaBootstrapService(
    IOptions<AssistantOptions> assistantOptions,
    AssistantRuntimeState state,
    ILogger<OllamaBootstrapService> logger) : BackgroundService
{
    private static readonly TimeSpan MinDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);

    private readonly AssistantOptions _options = assistantOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return; // state stays Disabled

        var delay = MinDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await EnsureReadyAsync(stoppingToken))
                {
                    if (state.Status != AssistantAvailability.Ready)
                        logger.LogInformation("OllamaBootstrap: assistant is Ready (chat={Chat}, embeddings={Embed}).",
                            _options.ChatModel, _options.EmbeddingModel);
                    state.Set(AssistantAvailability.Ready);
                    delay = MaxDelay; // supervision cadence once healthy
                }
                else
                {
                    delay = Grow(delay);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (state.Status != AssistantAvailability.OllamaUnreachable)
                    logger.LogWarning("OllamaBootstrap: Ollama unreachable at {BaseUrl} ({Reason}) — retrying.",
                        _options.BaseUrl, ex.Message);
                state.Set(AssistantAvailability.OllamaUnreachable);
                delay = Grow(delay);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Probes Ollama and pulls whatever is missing. True when both models are present.</summary>
    private async Task<bool> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        var api = new OllamaApiClient(new Uri(_options.BaseUrl));

        var missing = await GetMissingModelsAsync(api, cancellationToken);
        if (missing.Count == 0)
            return true;

        if (!_options.AutoPullModels)
        {
            state.Set(AssistantAvailability.OllamaUnreachable,
                $"missing model(s): {string.Join(", ", missing)} (AutoPullModels is off)");
            return false;
        }

        foreach (var model in missing)
        {
            logger.LogInformation("OllamaBootstrap: pulling {Model}…", model);
            state.Set(AssistantAvailability.DownloadingModels, $"{model} — 0%");

            var lastLoggedPercent = -10d;
            await foreach (var progress in api.PullModelAsync(new PullModelRequest { Model = model }, cancellationToken))
            {
                if (progress is null)
                    continue;

                var percent = progress.Percent;
                state.Set(AssistantAvailability.DownloadingModels, $"{model} — {percent:0}%");
                if (percent - lastLoggedPercent >= 10)
                {
                    logger.LogInformation("OllamaBootstrap: {Model} — {Percent:0}% ({Status})", model, percent, progress.Status);
                    lastLoggedPercent = percent;
                }
            }

            logger.LogInformation("OllamaBootstrap: pulled {Model}.", model);
        }

        // Verify the pulls actually landed before reporting Ready.
        return (await GetMissingModelsAsync(api, cancellationToken)).Count == 0;
    }

    private async Task<List<string>> GetMissingModelsAsync(OllamaApiClient api, CancellationToken cancellationToken)
    {
        var local = (await api.ListLocalModelsAsync(cancellationToken)).Select(m => m.Name).ToList();
        return new[] { _options.ChatModel, _options.EmbeddingModel }
            .Where(required => !local.Any(name => ModelMatches(name, required)))
            .ToList();
    }

    /// <summary>"bge-m3" must match a local "bge-m3:latest"; tagged names must match exactly.</summary>
    private static bool ModelMatches(string localName, string required) =>
        string.Equals(localName, required, StringComparison.OrdinalIgnoreCase) ||
        (!required.Contains(':') && localName.StartsWith(required + ":", StringComparison.OrdinalIgnoreCase));

    private static TimeSpan Grow(TimeSpan delay) =>
        delay >= MaxDelay ? MaxDelay : TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, MaxDelay.TotalSeconds));
}
