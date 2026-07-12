using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class BillingTransactionSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to a billing-transaction query.
    /// Same pattern as <see cref="UserSearchExtensions"/>: a switch expression keeps
    /// EF Core SQL translation intact, and every branch ends with a stable
    /// .ThenBy(Id) tie-breaker so ordering is deterministic across pages.
    /// Type and Reference are unsortable by design (cosmetic/opaque values).
    /// Unknown keys fall through to the pre-sorting default: CreatedAt descending.
    /// </summary>
    public static IQueryable<BillingTransaction> ApplySort(
        this IQueryable<BillingTransaction> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("plan", "desc") => query.OrderByDescending(bt => bt.SubscriptionPlan.Name),
            ("plan", _) => query.OrderBy(bt => bt.SubscriptionPlan.Name),
            ("amount", "desc") => query.OrderByDescending(bt => bt.Amount),
            ("amount", _) => query.OrderBy(bt => bt.Amount),
            ("createdat", "asc") => query.OrderBy(bt => bt.CreatedAt),
            _ => query.OrderByDescending(bt => bt.CreatedAt),
        };
        return ordered.ThenBy(bt => bt.Id);
    }
}
