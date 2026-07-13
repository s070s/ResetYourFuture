// Bring App and Routes razor components into scope
using ResetYourFuture.Web;
using ResetYourFuture.Web.Hubs;
using ResetYourFuture.Web.Logging;
using ResetYourFuture.Web.Startup;

EnvFileLoader.LoadIfPresent(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

// --- Logging ---
builder.Logging.AddFileLogger("Logs");

builder.AddResetYourFutureAuthentication();
builder.AddResetYourFutureServices();

var app = builder.Build();

await app.PrewarmAndSeedDatabaseAsync();

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
// written — e.g. bare NotFound()/Forbid()/Unauthorized() results, or 429 rate-limit rejections —
// into the same application/problem+json envelope produced by AddProblemDetails() above.
app.UseStatusCodePages();

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
