namespace ResetYourFuture.Application.Common;

/// <summary>Binds the "Assistant" configuration section for the local AI helper.
/// All models run locally via an Ollama sidecar — no cloud API keys involved.</summary>
public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";

    /// <summary>Master switch. When false, no AI clients or background indexing are registered
    /// and the widget/API report the assistant as unavailable. Keep false in test hosts.</summary>
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string ChatModel { get; set; } = "gemma3:4b";

    public string EmbeddingModel { get; set; } = "bge-m3";

    /// <summary>Maximum retrieved context chunks injected into the system prompt per question.</summary>
    public int MaxContextChunks { get; set; } = 6;

    public int MaxOutputTokens { get; set; } = 500;

    public float Temperature { get; set; } = 0.3f;

    /// <summary>Per-user chat requests allowed per minute (rate-limit policy "assistant").</summary>
    public int RequestsPerMinute { get; set; } = 10;
}
