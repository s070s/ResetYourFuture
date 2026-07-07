namespace ResetYourFuture.Web.Startup;

/// <summary>
/// Security response headers (CSP, nosniff, frame-ancestors, etc.), added before any content
/// is served.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static WebApplication UseResetYourFutureSecurityHeaders(this WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Permissions-Policy"] = "camera=(self), microphone=(self), display-capture=(self), geolocation=()";
            // 'unsafe-inline' is required for Blazor's circuit initialisation scripts and for the
            // <link onload="this.media='all'"> lazy-CSS pattern used in App.razor.
            // This still meaningfully limits XSS by blocking external-domain script/style/frame injection.
            // In Development, allow VS BrowserLink (http://localhost:*) and hot-reload
            // (ws://localhost:*) in connect-src. These are dev-only tools; the condition
            // ensures the looser directive never reaches production.
            var devConnectSrc = app.Environment.IsDevelopment()
                ? " http://localhost:* ws://localhost:*"
                : string.Empty;
            ctx.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                "font-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com data:; " +
                "img-src 'self' data: blob:; " +
                "media-src 'self' blob:; " +
                "frame-src 'self' https://www.youtube.com https://www.youtube-nocookie.com; " +
                $"connect-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com{devConnectSrc}; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self';";
            await next();
        });

        return app;
    }
}
