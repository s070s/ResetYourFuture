using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using System.Security.Claims;
using System.Text;

namespace ResetYourFuture.Web.Startup;

/// <summary>
/// Database, Identity, and the MultiAuth cookie/JWT scheme (Cookie for Blazor pages, JWT for
/// API/SignalR — the same [Authorize] attribute works for both).
/// </summary>
public static class AuthenticationSetupExtensions
{
    public static WebApplicationBuilder AddResetYourFutureAuthentication(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;

        // --- Database (SQL Server) ---
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));
        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // --- Identity ---
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // --- Authentication: MultiAuth policy (Cookie for Blazor pages, JWT for API/SignalR) ---
        var jwtKey = config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 32 bytes for HMAC-SHA256 security.");
        var jwtIssuer = config["Jwt:Issuer"];
        var jwtAudience = config["Jwt:Audience"];

        builder.Services.AddAuthentication(options =>
        {
            // MultiAuth selects JWT when Authorization header is present, Cookie otherwise.
            // This allows the same [Authorize] attribute to work for both Blazor pages and API controllers.
            //
            // IMPORTANT: AddIdentity (called above) sets DefaultAuthenticateScheme = "Identity.Application"
            // internally. We must explicitly override DefaultAuthenticateScheme here — setting only
            // DefaultScheme is not enough because DefaultAuthenticateScheme takes precedence when non-null.
            // Without this, ServerAuthenticationStateProvider authenticates via "Identity.Application",
            // which cannot read the .RYF.Auth cookie, and every Blazor circuit sees an anonymous user.
            options.DefaultScheme = "MultiAuth";
            options.DefaultAuthenticateScheme = "MultiAuth";
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddPolicyScheme("MultiAuth", "MultiAuth", options =>
        {
            options.ForwardDefaultSelector = ctx =>
            {
                var authHeader = ctx.Request.Headers.Authorization.ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    return JwtBearerDefaults.AuthenticationScheme;
                return CookieAuthenticationDefaults.AuthenticationScheme;
            };
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = ".RYF.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            // Sliding window: 24 h of activity for the server-side ticket validity.
            // Cookie.MaxAge is deliberately NOT set here: CookieBuilder applies MaxAge to every
            // Set-Cookie header unconditionally, regardless of AuthenticationProperties.IsPersistent,
            // which would force a persistent (7-day) cookie even for "Remember Me" unchecked
            // sign-ins. Persistence is controlled per sign-in instead — see
            // InfrastructureEndpointsExtensions.MapInfrastructureEndpoints's /auth/complete handler,
            // which sets IsPersistent + ExpiresUtc from the login's rememberMe flag.
            options.ExpireTimeSpan = TimeSpan.FromHours(24);
            options.SlidingExpiration = true;

            // In development allow the cookie over plain HTTP
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

            // Re-validate the cookie principal on every request. Without this, a custom cookie
            // scheme bypasses Identity's SecurityStampValidator, so disabling a user or rotating
            // their security stamp (password reset / forced logout) would not end an active
            // session until the 7-day MaxAge. Reject + sign out when the user is gone, disabled,
            // or the security stamp no longer matches the one embedded in the cookie at sign-in.
            options.Events.OnValidatePrincipal = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    context.RejectPrincipal();
                    return;
                }

                var userManager = context.HttpContext.RequestServices
                    .GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByIdAsync(userId);
                var cookieStamp = context.Principal?.FindFirstValue("securityStamp");

                if (user is null || !user.IsEnabled || user.SecurityStamp != cookieStamp)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme);
                }
            };
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                // Allow SignalR (WebSocket) requests to receive JWT from query string — a
                // framework constraint, since the browser's WebSocket API cannot set headers.
                // Lesson assets used to be allowed here too (SEC-2); they now carry their own
                // short-lived, single-lesson-scoped token instead (LessonAssetsController).
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                         (path.StartsWithSegments("/hubs/chat") ||
                           path.StartsWithSegments("/hubs/call")))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (userId is null)
                        return;

                    var userManager = context.HttpContext.RequestServices
                        .GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await userManager.FindByIdAsync(userId);

                    if (user is null || !user.IsEnabled)
                    {
                        context.Fail("Account is disabled.");
                        context.HttpContext.Items["UserDisabled"] = true;
                        return;
                    }

                    // Reject tokens minted before a password reset or security-stamp rotation.
                    var tokenStamp = context.Principal?.FindFirstValue("securityStamp");
                    if (tokenStamp != user.SecurityStamp)
                        context.Fail("Security stamp mismatch — token has been invalidated.");
                },
                OnChallenge = context =>
                {
                    if (context.HttpContext.Items.ContainsKey("UserDisabled"))
                    {
                        context.Response.Headers["X-User-Disabled"] = "true";
                    }
                    return Task.CompletedTask;
                }
            };
        });

        // --- Authorization Policies ---
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
            .AddPolicy("StudentOnly", policy => policy.RequireRole("Student"));

        return builder;
    }
}
