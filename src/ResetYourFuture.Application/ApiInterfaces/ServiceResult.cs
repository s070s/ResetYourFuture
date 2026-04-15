namespace ResetYourFuture.Web.ApiInterfaces;

/// <summary>
/// Thin wrapper that carries a value together with the HTTP status the controller should return.
/// Lets service methods signal not-found, forbidden, etc. without throwing exceptions.
/// </summary>
public record ServiceResult<T>( T? Value , int StatusCode = 200 , string? ErrorMessage = null )
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public static ServiceResult<T> Ok( T value ) => new( value );
    public static ServiceResult<T> Created( T value ) => new( value , 201 );
    public static ServiceResult<T> NotFound( T? value = default , string? error = null ) => new( value , 404 , error );
    public static ServiceResult<T> Forbidden( T? value = default , string? error = null ) => new( value , 403 , error );
    public static ServiceResult<T> BadRequest( T? value = default , string? error = null ) => new( value , 400 , error );
    public static ServiceResult<T> NoContent() => new( default , 204 );
}
