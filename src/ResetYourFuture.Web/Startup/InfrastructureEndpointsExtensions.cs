using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Services;
using System.Security.Claims;
using System.Text.Json;

namespace ResetYourFuture.Web.Startup;

/// <summary>
/// Minimal-API endpoints tagged "Infrastructure": culture switching, the two Blazor
/// auth-completion endpoints, and the XML sitemap. Grouped together because none of
/// them are JSON APIs — they're browser-navigation or crawler endpoints.
/// </summary>
public static class InfrastructureEndpointsExtensions
{
    public static WebApplication MapInfrastructureEndpoints(this WebApplication app)
    {
        // --- Culture endpoint ---
        app.MapGet("/culture/set", (string culture, string? returnUrl, HttpContext ctx) =>
        {
            ctx.Response.Cookies.Append(
                Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
                Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
                    new Microsoft.AspNetCore.Localization.RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

            // NavigationManager.Uri passes an absolute URL; extract the local path so
            // Results.LocalRedirect (which only accepts local paths) doesn't throw.
            var redirect = "/";
            if (!string.IsNullOrEmpty(returnUrl))
            {
                if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute))
                    redirect = absolute.PathAndQuery;
                else if (returnUrl.StartsWith('/'))
                    redirect = returnUrl;
            }

            return Results.LocalRedirect(redirect);
        })
        .WithTags("Infrastructure")
        .WithName("SetCulture")
        .WithSummary("Set the UI culture cookie and redirect back.")
        .WithDescription("Writes the ASP.NET Core localization cookie (supported cultures: en-GB, el-GR) and 302-redirects to a local returnUrl. Used by the language switcher; browser-navigation endpoint, not a JSON API.")
        .Produces(StatusCodes.Status302Found);

        // --- Auth completion endpoints ---
        // Blazor Server circuits run after the HTTP response has already been committed, so
        // cookie operations (SignInAsync / SignOutAsync) must happen on a separate fresh request.
        // Components issue NavigationManager.NavigateTo(url, forceLoad: true) with a signed
        // DataProtection token.  These endpoints decode the token, do a single DB lookup to
        // rebuild the ClaimsPrincipal, then call SignInAsync / SignOutAsync normally.

        app.MapGet("/auth/complete", async (
            string ticket,
            string? returnUrl,
            HttpContext ctx,
            IDataProtectionProvider dpProvider,
            UserManager<ApplicationUser> userManager,
            ISubscriptionService subscriptionService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("AuthCompletion");

            // --- Decode the signed token -------------------------------------------------
            string payload;
            try
            {
                var protector = dpProvider
                    .CreateProtector(AuthService.ProtectorPurpose)
                    .ToTimeLimitedDataProtector();
                payload = protector.Unprotect(ticket);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Auth completion: token invalid or expired.");
                return Results.LocalRedirect("/login?error=session_expired");
            }

            AuthCompletionTicket? decoded;
            try
            {
                decoded = JsonSerializer.Deserialize<AuthCompletionTicket>(payload);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Auth completion: token had unexpected format.");
                return Results.LocalRedirect("/login?error=session_expired");
            }
            if (decoded is null)
            {
                logger.LogWarning("Auth completion: token had unexpected format.");
                return Results.LocalRedirect("/login?error=session_expired");
            }

            var userId = decoded.UserId;
            var adminBackupId = string.IsNullOrEmpty(decoded.AdminBackupId) ? null : decoded.AdminBackupId;
            var deleteAdminBackup = decoded.DeleteAdminBackup;
            var tokenStamp = decoded.SecurityStamp;
            var rememberMe = decoded.RememberMe;

            // --- Rebuild principal from DB -----------------------------------------------
            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                logger.LogWarning("Auth completion: user {UserId} not found.", userId);
                return Results.LocalRedirect("/login");
            }

            // Reject if the user changed their password/security stamp since the token was issued.
            if (user.SecurityStamp != tokenStamp)
            {
                logger.LogWarning("Auth completion: security stamp mismatch for user {UserId} — token invalidated.", userId);
                return Results.LocalRedirect("/login?error=session_expired");
            }

            var roles = await userManager.GetRolesAsync(user);
            SubscriptionTier tier;
            try
            {
                tier = await subscriptionService.GetUserTierAsync(userId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Auth completion: GetUserTierAsync failed for {UserId} — defaulting to Free.", userId);
                tier = SubscriptionTier.Free;
            }

            var claims = UserClaimsBuilder.Build(user, roles, tier, adminBackupId);

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // --- Write / clear admin backup cookie ---------------------------------------
            if (!string.IsNullOrEmpty(adminBackupId))
            {
                var adminCookieProtector = dpProvider.CreateProtector(
                    AuthService.AdminBackupCookieProtectorPurpose);
                ctx.Response.Cookies.Append(
                    AuthService.AdminBackupCookieName,
                    adminCookieProtector.Protect(adminBackupId),
                    new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Lax,
                        Secure = !app.Environment.IsDevelopment(),
                        Expires = DateTimeOffset.UtcNow.AddHours(8),
                        IsEssential = true
                    });
            }
            if (deleteAdminBackup)
                ctx.Response.Cookies.Delete(AuthService.AdminBackupCookieName);

            // --- Issue auth cookie -------------------------------------------------------
            // IsPersistent=false (unchecked "Remember Me") issues a session cookie the browser
            // discards on close; the cookie middleware's 24h sliding ExpireTimeSpan still governs
            // the ticket while the session is open. IsPersistent=true adds a 7-day hard cap.
            var authProperties = new AuthenticationProperties { IsPersistent = rememberMe };
            if (rememberMe)
                authProperties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7);

            await ctx.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);

            logger.LogInformation("User {UserId} signed in via /auth/complete.", userId);
            if (!string.IsNullOrEmpty(adminBackupId))
                logger.LogWarning("Admin {AdminId} is impersonating user {UserId}.", adminBackupId, userId);

            var redirect = "/";
            if (!string.IsNullOrEmpty(returnUrl))
            {
                if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var abs))
                    redirect = abs.PathAndQuery;
                else if (returnUrl.StartsWith('/'))
                    redirect = returnUrl;
            }

            return Results.LocalRedirect(redirect);
        }).AllowAnonymous()
        .WithTags("Infrastructure")
        .WithName("CompleteAuth")
        .WithSummary("Complete Blazor sign-in and issue the auth cookie.")
        .WithDescription("Decodes a signed DataProtection ticket, rebuilds the principal from the database, and issues the .RYF.Auth cookie (and admin-backup cookie when impersonating). Invoked via full-page redirect from Blazor after login. Returns a 302 redirect; browser-navigation endpoint, not a JSON API.")
        .Produces(StatusCodes.Status302Found);

        app.MapGet("/auth/signout", async (string? returnUrl, HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            ctx.Response.Cookies.Delete(AuthService.AdminBackupCookieName);

            var redirect = "/";
            if (!string.IsNullOrEmpty(returnUrl))
            {
                if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var abs))
                    redirect = abs.PathAndQuery;
                else if (returnUrl.StartsWith('/'))
                    redirect = returnUrl;
            }

            return Results.LocalRedirect(redirect);
        }).AllowAnonymous()
        .WithTags("Infrastructure")
        .WithName("SignOut")
        .WithSummary("Clear the auth cookie and redirect.")
        .WithDescription("Signs out the cookie session and deletes the admin-backup cookie, then 302-redirects to a local returnUrl. Browser-navigation endpoint, not a JSON API.")
        .Produces(StatusCodes.Status302Found);

        // --- Sitemap ---
        app.MapGet("/sitemap.xml", async (IBlogArticleService blog, IConfiguration sitemapConfig, IMemoryCache sitemapCache, HttpContext ctx) =>
        {
            var baseUrl = sitemapConfig["Sitemap:BaseUrl"]
                ?? throw new InvalidOperationException("Sitemap:BaseUrl is not configured.");

            // Cache the rendered XML for 30 minutes — avoids a DB hit + StringBuilder allocation on every crawler request.
            var xml = await sitemapCache.GetOrCreateAsync("sitemap.xml", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                var articles = await blog.GetPublishedSummariesAsync(200, "en", ctx.RequestAborted);

                var escapedBase = System.Security.SecurityElement.Escape(baseUrl);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
                sb.AppendLine($"  <url><loc>{escapedBase}/</loc><priority>1.0</priority><changefreq>weekly</changefreq></url>");

                foreach (var article in articles)
                {
                    var safeSlug = System.Security.SecurityElement.Escape(article.Slug);
                    var lastmod = article.PublishedAt?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
                    sb.AppendLine($"  <url><loc>{escapedBase}/blog/{safeSlug}</loc><lastmod>{lastmod}</lastmod><priority>0.8</priority><changefreq>monthly</changefreq></url>");
                }

                sb.AppendLine("</urlset>");
                return sb.ToString();
            });

            ctx.Response.ContentType = "application/xml; charset=utf-8";
            await ctx.Response.WriteAsync(xml!);
        }).AllowAnonymous()
        .WithTags("Infrastructure")
        .WithName("Sitemap")
        .WithSummary("XML sitemap of public pages and published blog articles.")
        .WithDescription("Returns an application/xml sitemap for search-engine crawlers. Cached for 30 minutes.")
        .Produces(StatusCodes.Status200OK, contentType: "application/xml");

        // --- Health checks (AVAIL-1) ---
        // Liveness: is the process itself responsive? No dependency checks (Predicate: false runs
        // none of the registered checks) — a slow/unreachable DB should fail readiness, not liveness.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .WithTags("Infrastructure")
            .WithName("HealthLive")
            .WithSummary("Liveness probe — process is up, no dependency checks.")
            .ExcludeFromDescription();

        // Readiness: can this instance actually serve traffic? Runs every check tagged "ready"
        // (database, assistant). Healthy/Degraded both return 200 by default; only Unhealthy (the
        // database check) returns 503 — the assistant being degraded doesn't block readiness.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            })
            .AllowAnonymous()
            .WithTags("Infrastructure")
            .WithName("HealthReady")
            .WithSummary("Readiness probe — database + assistant status.")
            .ExcludeFromDescription();

        return app;
    }
}
