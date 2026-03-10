namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Generic server-side paged result envelope. Reusable across all list endpoints.
/// </summary>
public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling( (double)TotalCount / PageSize ) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
