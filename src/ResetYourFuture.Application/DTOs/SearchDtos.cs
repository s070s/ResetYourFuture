namespace ResetYourFuture.Application.DTOs;

/// <summary>One search result. SourceType is one of "Course" | "Assessment" | "BlogArticle"
/// (lesson hits resolve to their parent course, matching the assistant's grounding links).</summary>
public record SiteSearchHitDto(string SourceType, string Title, string? Snippet, string Url);

/// <summary>SemanticSearchUsed is false when Ollama/the assistant wasn't Ready and the results
/// came from the SQL title-LIKE fallback instead — the UI shows a notice in that case.</summary>
public record SiteSearchResultDto(List<SiteSearchHitDto> Hits, bool SemanticSearchUsed);
