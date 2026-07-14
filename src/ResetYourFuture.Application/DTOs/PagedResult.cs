namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// Generic server-side paged result envelope. Reusable across all list endpoints.
/// SortBy/SortDir are null when the endpoint doesn't support sorting — they describe
/// the actual sort applied, not a fixed field that happens to be present on every list.
/// </summary>
public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    string? SortBy = null,
    string? SortDir = null)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
