using ResetYourFuture.Web.Identity;

namespace ResetYourFuture.Web.Extensions;

public static class UserSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to a user query.
    /// Uses a switch expression to keep EF Core SQL translation intact —
    /// never boxes to object, which would trigger client-side evaluation.
    /// Always appends .ThenBy(Email) as a stable tie-breaker across pages.
    /// </summary>
    public static IQueryable<ApplicationUser> ApplySort(
        this IQueryable<ApplicationUser> query , string? sortBy , string? sortDir )
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("firstname", "desc") => query.OrderByDescending( u => u.FirstName ).ThenBy( u => u.LastName ) ,
            ("firstname", _) => query.OrderBy( u => u.FirstName ).ThenBy( u => u.LastName ) ,
            ("lastname", "desc") => query.OrderByDescending( u => u.LastName ).ThenBy( u => u.FirstName ) ,
            ("lastname", _) => query.OrderBy( u => u.LastName ).ThenBy( u => u.FirstName ) ,
            ("createdat", "desc") => query.OrderByDescending( u => u.CreatedAt ) ,
            ("createdat", _) => query.OrderBy( u => u.CreatedAt ) ,
            ("isenabled", "desc") => query.OrderByDescending( u => u.IsEnabled ) ,
            ("isenabled", _) => query.OrderBy( u => u.IsEnabled ) ,
            ("email", "desc") => query.OrderByDescending( u => u.Email ) ,
            _ => query.OrderBy( u => u.Email ) ,
        };
        return ordered.ThenBy( u => u.Email );
    }

    /// <summary>
    /// Applies smart search to a user query.
    /// - '@' present → email-mode: match by prefix (StartsWith) and/or suffix (Contains "@suffix")
    /// - space present → first+last split: match FirstName/LastName in either order
    /// - otherwise → contains match on Email, FirstName, LastName
    /// </summary>
    public static IQueryable<ApplicationUser> ApplySearch( this IQueryable<ApplicationUser> query , string term )
    {
        if ( term.Contains( '@' ) )
        {
            var parts = term.Split( '@' , 2 );
            var prefix = parts [ 0 ];
            var suffix = parts.Length > 1 ? parts [ 1 ] : string.Empty;

            if ( !string.IsNullOrEmpty( prefix ) && !string.IsNullOrEmpty( suffix ) )
                return query.Where( u => u.Email!.StartsWith( prefix ) && u.Email!.Contains( "@" + suffix ) );

            if ( !string.IsNullOrEmpty( prefix ) )
                return query.Where( u => u.Email!.StartsWith( prefix ) );

            if ( !string.IsNullOrEmpty( suffix ) )
                return query.Where( u => u.Email!.Contains( "@" + suffix ) );

            return query;
        }

        var nameParts = term.Split( ' ' , 2 , StringSplitOptions.RemoveEmptyEntries );
        if ( nameParts.Length == 2 )
        {
            var first = nameParts [ 0 ];
            var last = nameParts [ 1 ];
            return query.Where( u =>
                ( u.FirstName.Contains( first ) && u.LastName.Contains( last ) ) ||
                ( u.FirstName.Contains( last ) && u.LastName.Contains( first ) ) );
        }

        return query.Where( u =>
            u.Email!.Contains( term ) ||
            u.FirstName.Contains( term ) ||
            u.LastName.Contains( term ) );
    }
}
