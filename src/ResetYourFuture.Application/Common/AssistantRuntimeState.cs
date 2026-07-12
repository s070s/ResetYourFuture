namespace ResetYourFuture.Application.Common;

/// <summary>Where the assistant currently is in its lifecycle. Unlike the old DI-time swap
/// (real pipeline vs DisabledAssistantService decided at startup), this state changes at
/// runtime: installing/starting Ollama after the app boots transitions the assistant to
/// Ready without a restart.</summary>
public enum AssistantAvailability
{
    /// <summary>Assistant:Enabled is false — terminal until a config change + restart.</summary>
    Disabled,

    /// <summary>Ollama did not answer at BaseUrl (not installed, not running, or a required
    /// model is missing while AutoPullModels is off). Re-probed continuously.</summary>
    OllamaUnreachable,

    /// <summary>Ollama is up and a required model is being pulled; see <see cref="AssistantRuntimeState.Progress"/>.</summary>
    DownloadingModels,

    /// <summary>Ollama reachable and both models present — chat and indexing may proceed.</summary>
    Ready,
}

/// <summary>Singleton holding the assistant's live availability, written by the Ollama
/// bootstrap supervisor and read by the status endpoint, chat pipeline, and indexer.</summary>
public sealed class AssistantRuntimeState
{
    private readonly object _gate = new();
    private AssistantAvailability _status = AssistantAvailability.Disabled;
    private string? _progress;

    public AssistantAvailability Status
    {
        get { lock (_gate) return _status; }
    }

    /// <summary>Human-readable progress line while downloading (e.g. "qwen3:1.7b — 43%"),
    /// or a hint naming what is missing while unreachable. Null otherwise.</summary>
    public string? Progress
    {
        get { lock (_gate) return _progress; }
    }

    public void Set(AssistantAvailability status, string? progress = null)
    {
        lock (_gate)
        {
            _status = status;
            _progress = progress;
        }
    }
}
