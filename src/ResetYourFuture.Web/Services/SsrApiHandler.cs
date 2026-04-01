using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// DelegatingHandler for server-side HttpClient consumers.
/// Attaches a short-lived JWT (built from the current user's cookie claims) as the
/// Authorization header so API controllers continue to receive a Bearer token when
/// the Blazor Server circuit calls them via HttpClient on the same server.
/// No database calls are made — claims are read directly from the authenticated principal.
/// </summary>
public class SsrApiHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _jwtKey;
    private readonly string? _jwtIssuer;
    private readonly string? _jwtAudience;
    private readonly double _expirationMinutes;

    private static readonly JwtSecurityTokenHandler TokenHandler = new() { SetDefaultTimesOnTokenCreation = false };

    public SsrApiHandler( IHttpContextAccessor httpContextAccessor , IConfiguration config )
    {
        _httpContextAccessor = httpContextAccessor;
        _jwtKey = config [ "Jwt:Key" ]
            ?? throw new InvalidOperationException( "Jwt:Key not configured" );
        _jwtIssuer = config [ "Jwt:Issuer" ];
        _jwtAudience = config [ "Jwt:Audience" ];
        _expirationMinutes = double.Parse( config [ "Jwt:AccessTokenExpirationMinutes" ] ?? "60" );
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request , CancellationToken cancellationToken )
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if ( principal?.Identity?.IsAuthenticated == true )
        {
            var key = new SymmetricSecurityKey( Encoding.UTF8.GetBytes( _jwtKey ) );
            var creds = new SigningCredentials( key , SecurityAlgorithms.HmacSha256 );
            var jwt = new JwtSecurityToken(
                issuer: _jwtIssuer ,
                audience: _jwtAudience ,
                claims: principal.Claims ,
                expires: DateTime.UtcNow.AddMinutes( _expirationMinutes ) ,
                signingCredentials: creds );

            request.Headers.Authorization =
                new AuthenticationHeaderValue( "Bearer" , TokenHandler.WriteToken( jwt ) );
        }

        return base.SendAsync( request , cancellationToken );
    }
}
