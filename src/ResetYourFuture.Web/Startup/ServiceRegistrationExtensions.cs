using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Infrastructure.ApiServices;
using ResetYourFuture.Infrastructure.Configuration;
using ResetYourFuture.Infrastructure.Seeding;
using ResetYourFuture.Infrastructure.Services;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Web.OpenApi;
using ResetYourFuture.Web.Services;

namespace ResetYourFuture.Web.Startup;

/// <summary>
/// Application/infrastructure service registrations, typed HttpClient consumers, and
/// cross-cutting ASP.NET Core services (localization, SignalR, rate limiting, data protection,
/// Blazor SSR). Split out from authentication setup since none of this depends on it.
/// </summary>
public static class ServiceRegistrationExtensions
{
    public static WebApplicationBuilder AddResetYourFutureServices(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;

        // --- HTML Sanitizer (XSS protection for rich-text content) ---
        builder.Services.AddSingleton<Ganss.Xss.IHtmlSanitizer>(_ => new Ganss.Xss.HtmlSanitizer());

        // --- API Services ---
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
        // Email transport. SmtpEmailService (MailKit) is used whenever Email:Smtp:Host is configured —
        // point it at Papercut/Mailhog in Development or a real relay (SES/SendGrid SMTP/etc.) in prod.
        // With no SMTP host configured, Development falls back to StubEmailService (logs only); any other
        // environment fails fast so emails are never silently swallowed in production.
        builder.Services.Configure<EmailOptions>(config.GetSection(EmailOptions.SectionName));
        if (!string.IsNullOrWhiteSpace(config["Email:Smtp:Host"]))
        {
            builder.Services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddScoped<IEmailService, StubEmailService>();
        }
        else
        {
            throw new InvalidOperationException(
                "No email transport configured. Set Email:Smtp:Host (and credentials) for production.");
        }
        builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
        builder.Services.AddScoped<ICertificateService, CertificateService>();
        builder.Services.AddScoped<IBlogArticleService, BlogArticleService>();
        builder.Services.AddScoped<ITestimonialService, TestimonialService>();

        // --- Web Services ---
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IAuthApiService, AuthApiService>();
        builder.Services.AddScoped<ICourseService, CourseService>();
        builder.Services.AddScoped<IAdminCourseService, AdminCourseService>();
        builder.Services.AddScoped<IAdminUserService, AdminUserService>();
        builder.Services.AddScoped<IChatQueryService, ChatQueryService>();
        builder.Services.AddScoped<IProfileService, ProfileService>();
        builder.Services.AddScoped<IAssessmentService, AssessmentService>();
        builder.Services.AddScoped<AvatarChangedNotifier>();
        // Circuit-scoped bearer-token provider used by ApiClientBase so consumers authenticate
        // from inside the Blazor circuit (where HttpContext/SsrApiHandler is unavailable).
        builder.Services.AddScoped<ApiTokenProvider>();

        // --- SSR API Handler (attaches JWT from cookie claims for loopback HttpClient calls) ---
        builder.Services.AddTransient<SsrApiHandler>();

        AddConsumerHttpClients(builder);

        // --- Localization ---
        builder.Services.AddLocalization();
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[] { "en-GB", "el-GR" };
            options.SetDefaultCulture("en-GB")
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);
        });

        builder.Services.AddMemoryCache();
        builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 32_000);
        builder.Services.AddControllers();
        // Enables IProblemDetailsService, used by UseStatusCodePages() (bare-status error responses,
        // e.g. NotFound()/Forbid()/429 rate-limit rejections) and the production exception handler
        // below, so every error response follows the same RFC 7807 application/problem+json shape.
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
        });
        // Built-in OpenAPI document ("v1") + API metadata, JWT bearer scheme, and per-operation security.
        builder.Services.AddResetYourFutureOpenApi();
        builder.Services.AddHostedService<BulkStudentSeedingService>();

        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("auth", limiterOptions =>
            {
                limiterOptions.PermitLimit = 10;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        builder.Services.AddHttpContextAccessor();

        // NOTE
        // Keys are persisted to the local
        // filesystem, fine for single-instance Development. On a multi-instance or container/ephemeral
        // host this breaks sign-in — each instance has its own key ring (so a cookie or auth-completion
        // ticket issued by one instance is rejected by another) and keys are lost on restart. For
        // production, persist to shared storage (Azure Blob / Redis / network share) and protect with a
        // certificate or Key Vault.
        var dpBuilder = builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")))
            .SetApplicationName("ResetYourFuture");

        // On Windows: encrypt keys at rest with DPAPI (user account or machine scope).
        // On Linux/containers: replace this block with ProtectKeysWithCertificate or Azure KeyVault.
        if (OperatingSystem.IsWindows())
            dpBuilder.ProtectKeysWithDpapi();

        // --- Blazor SSR ---
        // AddCascadingAuthenticationState registers the ServerAuthenticationStateProvider
        // which reads auth state from the Blazor Server circuit's connection principal.
        // Without this, AuthenticationState cascades as anonymous even when the auth
        // cookie is present, because the interactive circuit cannot access HttpContext.
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Suppress noisy info-level authorization logs
        builder.Logging.AddFilter("Microsoft.AspNetCore.Authorization", LogLevel.Warning);

        return builder;
    }

    // --- Consumer registrations (server-side HttpClient calling the same host) ---
    // NOTE
    // The Blazor server renders pages by
    // calling its OWN API over HTTP via these typed consumers. The localhost:7090 default is
    // correct for Development only. In production, SelfBaseUrl MUST point at the real bound base
    // address AND the loopback TLS certificate must be trusted by this in-process HttpClient —
    // otherwise every API-backed page silently renders empty (ApiClientBase swallows non-success
    // responses). Consider calling the application services in-process instead of over HTTP.
    private static void AddConsumerHttpClients(WebApplicationBuilder builder)
    {
        var selfBase = builder.Configuration["SelfBaseUrl"] ?? "https://localhost:7090";

        // Named client for dev-only endpoints (no auth handler needed)
        builder.Services.AddHttpClient("SelfClient", c => c.BaseAddress = new Uri(selfBase));

        builder.Services.AddHttpClient<ICourseConsumer, CourseConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAssessmentConsumer, AssessmentConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<ISubscriptionConsumer, SubscriptionConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IProfileConsumer, ProfileConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminAnalyticsConsumer, AdminAnalyticsConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminUserConsumer, AdminUserConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminCourseConsumer, AdminCourseConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminModuleConsumer, AdminModuleConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminLessonConsumer, AdminLessonConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminAssessmentConsumer, AdminAssessmentConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<ICertificateConsumer, CertificateConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IBlogConsumer, BlogConsumer>(c => c.BaseAddress = new Uri(selfBase));
        builder.Services.AddHttpClient<IAdminBlogConsumer, AdminBlogConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<ITestimonialConsumer, TestimonialConsumer>(c => c.BaseAddress = new Uri(selfBase));
        builder.Services.AddHttpClient<IAdminTestimonialConsumer, AdminTestimonialConsumer>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IChatService, ChatService>(c => c.BaseAddress = new Uri(selfBase))
            .AddHttpMessageHandler<SsrApiHandler>();
    }
}
