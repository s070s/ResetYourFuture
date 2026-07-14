// Bring App and Routes razor components into scope
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using ResetYourFuture.Web;
using ResetYourFuture.Web.Hubs;
using ResetYourFuture.Web.Logging;
using ResetYourFuture.Web.Startup;

EnvFileLoader.LoadIfPresent(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

builder.ValidateRequiredConfig();

// --- Logging ---
builder.Logging.AddFileLogger("Logs");

builder.AddResetYourFutureAuthentication();
builder.AddResetYourFutureServices();

var app = builder.Build();

await app.PrewarmAndSeedDatabaseWithRetryAsync();

// --- Pipeline ---
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    // OpenAPI JSON document at /openapi/v1.json (Development only).
    app.MapOpenApi();

    // Swagger UI (Swashbuckle) served at /swagger, pointed at the built-in OpenAPI document.
    // Development only — no AddSwaggerGen needed; the UI consumes /openapi/v1.json directly.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ResetYourFuture API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "ResetYourFuture API — Swagger UI";
        options.EnablePersistAuthorization();
        options.EnableTryItOutByDefault();
    });
}
else
{
    app.UseExceptionHandler(exceptionHandlerApp =>
    {
        exceptionHandlerApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.WriteAsync(new()
            {
                HttpContext = context,
                ProblemDetails = new()
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Detail = "Please try again later."
                }
            });
        });
    });
}

// Converts any response that reaches a terminal error status code (400-599) with no body yet
// written into a fitting representation. Re-executes against the dispatcher below, which keeps
// /api/* on the JSON application/problem+json envelope (bare NotFound()/Forbid()/Unauthorized()
// results, 429 rate-limit rejections — the contract API consumers expect) and renders the Blazor
// NotFound page for everything else — most importantly a genuinely unmatched page route, which
// the routing layer 404s on *before* the Router ever runs (the <NotFound> render fragment was
// removed in .NET 10 in favor of Router.NotFoundPage) — so a mistyped/dead URL gets the app shell
// instead of a bare 404 with no navigation (UX-3).
app.UseStatusCodePagesWithReExecute("/__status-code-dispatch");

app.UseRateLimiter();
app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    // Enforce HTTPS with a long max-age (1 year). Preload-ready when ready to submit to HSTS lists.
    app.UseHsts();
}

app.UseResetYourFutureSecurityHeaders();

app.UseStaticFiles();
app.UseRequestLocalization();

// Disabled-user enforcement is handled by two mechanisms:
// - Cookie auth: CookieAuthenticationHandler.OnValidatePrincipal re-validates on every Blazor request.
// - JWT auth: OnTokenValidated sets context.HttpContext.Items["UserDisabled"] and fails the token;
//             the OnChallenge handler then adds the X-User-Disabled response header.

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapInfrastructureEndpoints();

// Target of UseStatusCodePagesWithReExecute above — branches on the ORIGINAL request path
// (available via IStatusCodeReExecuteFeature, since this endpoint itself is always reached at
// /__status-code-dispatch) so /api/* keeps its JSON contract while page routes get the Blazor
// NotFound component. Not meant for direct navigation. Re-execution preserves the ORIGINAL HTTP
// method (e.g. a POST that 403'd), so this must accept any verb — Map(), not MapGet().
app.Map("/__status-code-dispatch", (HttpContext ctx) =>
{
    var originalPath = ctx.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath;
    return originalPath is not null && originalPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
        ? Results.Problem(statusCode: ctx.Response.StatusCode)
        : (IResult)new RazorComponentResult<ResetYourFuture.Web.Pages.NotFound>();
}).AllowAnonymous().ExcludeFromDescription();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<CallHub>("/hubs/call");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ResetYourFuture.Web started. Logs: {LogsPath}", Path.GetFullPath("Logs"));

app.Run();

// Exposes the implicit top-level Program class as public so integration tests can use
// WebApplicationFactory<Program>. Compile-time only — no runtime effect.
public partial class Program;
