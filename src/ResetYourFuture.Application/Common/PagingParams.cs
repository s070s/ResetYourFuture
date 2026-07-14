namespace ResetYourFuture.Application.Common;

/// <summary>
/// Shared page/pageSize normalization for every list endpoint and service. Clamp-to-max
/// semantics: an oversized pageSize is capped at <see cref="MaxPageSize"/>, not silently
/// reset to some smaller default, and page/pageSize are never allowed below 1.
/// </summary>
public static class PagingParams
{
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
}
