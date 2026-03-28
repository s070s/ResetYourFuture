using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Extensions;

internal static class UserSearchExtensions
{
    /// <summary>
    /// Applies smart search to a user query.
    /// - '@' present → email-mode: match by prefix (StartsWith) and/or suffix (Contains "@suffix")
    /// - space present → first+last split: match FirstName/LastName in either order
    /// - otherwise → contains match on Email, FirstName, LastName
    /// </summary>
    internal static IQueryable<ApplicationUser> ApplySearch( this IQueryable<ApplicationUser> query, string term )
    {
        if ( term.Contains( '@' ) )
        {
            var parts = term.Split( '@', 2 );
            var prefix = parts[0];
            var suffix = parts.Length > 1 ? parts[1] : string.Empty;

            if ( !string.IsNullOrEmpty( prefix ) && !string.IsNullOrEmpty( suffix ) )
                return query.Where( u => u.Email!.StartsWith( prefix ) && u.Email!.Contains( "@" + suffix ) );

            if ( !string.IsNullOrEmpty( prefix ) )
                return query.Where( u => u.Email!.StartsWith( prefix ) );

            if ( !string.IsNullOrEmpty( suffix ) )
                return query.Where( u => u.Email!.Contains( "@" + suffix ) );

            return query;
        }

        var nameParts = term.Split( ' ', 2, StringSplitOptions.RemoveEmptyEntries );
        if ( nameParts.Length == 2 )
        {
            var first = nameParts[0];
            var last = nameParts[1];
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
