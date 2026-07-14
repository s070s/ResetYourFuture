using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.AI;
using OllamaSharp;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Infrastructure.ApiServices;
using ResetYourFuture.Infrastructure.Configuration;
using ResetYourFuture.Infrastructure.Data;
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
        builder.Services.AddScoped<INotificationService, NotificationService>();
        // Depends on IAssistantRetrievalService, which is always resolvable (real or Disabled
        // impl) regardless of Assistant:Enabled — see the Disabled branch below.
        builder.Services.AddScoped<ISiteSearchService, SiteSearchService>();
        builder.Services.AddScoped<ICourseReviewService, CourseReviewService>();
        builder.Services.AddScoped<ILearningPathService, LearningPathService>();
        builder.Services.AddScoped<IScheduledSessionService, ScheduledSessionService>();
        // Web-layer dispatcher (needs IHubContext<NotificationHub>) so Application/Infrastructure
        // services can raise notifications through the framework-agnostic INotificationDispatcher.
        builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        // Tracks live NotificationHub connections (per-user refcount) — the hub connects globally
        // in MainLayout, so this doubles as an online/offline signal for dispatch decisions.
        builder.Services.AddSingleton<NotificationConnectionTracker>();

        // --- Web Services ---
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IAuthApiService, AuthApiService>();
        builder.Services.AddScoped<ICourseService, CourseService>();
        builder.Services.AddScoped<IAdminCourseService, AdminCourseService>();
        builder.Services.AddScoped<ICategoryService, CategoryService>();
        builder.Services.AddScoped<IAdminCategoryService, AdminCategoryService>();
        builder.Services.AddScoped<IAdminUserService, AdminUserService>();
        builder.Services.AddScoped<IChatQueryService, ChatQueryService>();
        builder.Services.AddScoped<IChatCommandService, ChatCommandService>();
        builder.Services.AddScoped<ICallEventService, CallEventService>();
        builder.Services.AddScoped<ICallQueryService, CallQueryService>();
        builder.Services.AddSingleton<CallRegistry>();
        builder.Services.AddHostedService<CallRingMonitor>();
        builder.Services.AddHostedService<SessionStartMonitor>();
        builder.Services.AddHostedService<SubscriptionExpirySweeper>();
        builder.Services.AddHostedService<RefreshTokenPurgeService>();
        builder.Services.Configure<WebRtcOptions>(config.GetSection("WebRtc"));
        // Hub-only (no REST) — plain AddScoped, not AddHttpClient. Must be scoped (not transient
        // like ChatService) so CallOverlayHost and chat components share one instance/hub/state per circuit.
        builder.Services.AddScoped<ICallService, CallService>();
        // Scoped so it shares the circuit's single CallService hub connection for presence state.
        builder.Services.AddScoped<PresenceService>();
        builder.Services.AddScoped<IProfileService, ProfileService>();
        builder.Services.AddScoped<IAssessmentService, AssessmentService>();
        builder.Services.AddScoped<AvatarChangedNotifier>();
        // Circuit-scoped bearer-token provider used by ApiClientBase so consumers authenticate
        // from inside the Blazor circuit (where HttpContext/SsrApiHandler is unavailable).
        builder.Services.AddScoped<ApiTokenProvider>();

        // --- AI Assistant ---
        // Local-only: an Ollama sidecar serves both the chat and embedding models behind
        // Microsoft.Extensions.AI abstractions (IChatClient / IEmbeddingGenerator), so nothing
        // outside this block references OllamaSharp directly — swapping model/runtime later is
        // config plus this one registration. AssistantIndexSignal is always registered (cheap,
        // no Ollama dependency) so the admin reindex endpoint never 500s; when disabled, IAssistantService
        // resolves to a no-op that reports unavailable instead of the real Ollama-backed pipeline —
        // that's what keeps Ollama out of CI/test hosts entirely.
        builder.Services.Configure<AssistantOptions>(config.GetSection(AssistantOptions.SectionName));
        var assistantOptions = config.GetSection(AssistantOptions.SectionName).Get<AssistantOptions>() ?? new AssistantOptions();
        builder.Services.AddSingleton<AssistantIndexSignal>();
        // Runtime availability (Disabled/OllamaUnreachable/DownloadingModels/Ready) is always
        // registered: OllamaBootstrapService writes it, status/chat/indexer read it. This is what
        // lets "enabled but Ollama missing" recover at runtime instead of needing a restart.
        builder.Services.AddSingleton<AssistantRuntimeState>();
        if (assistantOptions.Enabled)
        {
            // UseFunctionInvocation wraps the Ollama client so ChatOptions.Tools are executed
            // in an agent loop, capped at MaxToolRounds iterations per request (guardrail
            // against pathological tool loops on a small model).
            builder.Services.AddChatClient(_ =>
                    new OllamaApiClient(new Uri(assistantOptions.BaseUrl), assistantOptions.ChatModel))
                .UseFunctionInvocation(configure: client =>
                    client.MaximumIterationsPerRequest = assistantOptions.MaxToolRounds);
            builder.Services.AddEmbeddingGenerator(_ =>
                new OllamaApiClient(new Uri(assistantOptions.BaseUrl), assistantOptions.EmbeddingModel));

            builder.Services.AddSingleton<AssistantIndexVersion>();
            builder.Services.AddSingleton<AssistantChunkCache>();
            builder.Services.AddScoped<IAssistantIndexingService, AssistantIndexingService>();
            builder.Services.AddScoped<IAssistantRetrievalService, AssistantRetrievalService>();
            builder.Services.AddScoped<IAssistantService, AssistantService>();
            builder.Services.AddScoped<IAssistantTools, AssistantTools>();
            builder.Services.AddHostedService<OllamaBootstrapService>();
            builder.Services.AddHostedService<AssistantIndexer>();
        }
        else
        {
            builder.Services.AddScoped<IAssistantService, DisabledAssistantService>();
            // SiteSearchService depends on IAssistantRetrievalService unconditionally and falls
            // back to a title LIKE search on empty results — this keeps it resolvable either way.
            builder.Services.AddScoped<IAssistantRetrievalService, DisabledAssistantRetrievalService>();
        }

        // --- Background-service fault isolation + graceful shutdown (AVAIL-6, AVAIL-5) ---
        // The generic host also owns Kestrel, and the default BackgroundServiceExceptionBehavior
        // (StopHost) means an unhandled exception from ANY hosted service (CallRingMonitor,
        // AssistantIndexer, RefreshTokenPurgeService, the bulk seeder, Ollama bootstrap) would take
        // the whole web server down with it. None of these are critical to serving HTTP, so a
        // failing background job should log and stop — not end availability for everyone (AVAIL-6).
        // ShutdownTimeout is set explicitly (AVAIL-5) rather than left at the default so in-flight
        // requests and the ServerShuttingDown broadcast have a bounded, documented drain window.
        builder.Services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
            options.ShutdownTimeout = TimeSpan.FromSeconds(10);
        });

        // AVAIL-5: broadcasts a clean "server is stopping" notice to chat/call clients on shutdown.
        builder.Services.AddHostedService<GracefulShutdownNotifier>();

        // --- Health checks (AVAIL-1) ---
        // "database" and "assistant" are tagged "ready" so /health/ready reflects both; the
        // assistant check reports Degraded (not Unhealthy) when Ollama is unreachable, since the
        // rest of the app serves fine without it — see AssistantHealthCheck.
        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
            .AddCheck<AssistantHealthCheck>("assistant", tags: ["ready"]);

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

        // SCALE-7: this IMemoryCache is process-local. The short-TTL entries backed by it
        // (subscription status, assistant status, sitemap) are invalidated explicitly on change
        // within this process — correct for a single instance, but the invalidation would NOT
        // propagate to a second node. Keep TTLs short and move to a shared cache (Redis) before
        // scaling out; see the accepted-limitations note in docs/plans/35-audit-scalability.md.
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

        // SCALE-7: these are the in-process ASP.NET Core rate limiters, so their windows are
        // per-instance. On a single node the limits below are authoritative; behind a load
        // balancer with N nodes each partition's effective limit multiplies by N, weakening the
        // brute-force protection SEC relies on. Move the counters to a shared store (or the
        // fronting proxy) before running more than one instance.
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("auth", limiterOptions =>
            {
                limiterOptions.PermitLimit = 10;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });
            // Per-user (not global, unlike "auth") so one chatty student can't starve everyone else's quota.
            options.AddPolicy("assistant", httpContext =>
            {
                var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
                return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(userId, _ =>
                    new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = assistantOptions.RequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
            // SEC-3: default per-user limiter for authenticated, state-changing endpoints that
            // had no back-pressure at all (password change/reset, avatar upload, checkout,
            // assessment submission). Per-user (like "assistant"), not global (like "auth") —
            // these are individual account actions, so a shared bucket would let one user's
            // burst lock out everyone else's legitimate change-password/checkout calls.
            options.AddPolicy("sensitive", httpContext =>
            {
                var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
                return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(userId, _ =>
                    new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        builder.Services.AddHttpContextAccessor();

        // LOG-4: lets services attribute admin-action audit logs to the acting user.
        builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

        // NOTE (SCALE-4)
        // Keys are persisted to the shared SQL database (the same one every instance already
        // connects to) instead of the local filesystem — this fixes both single-instance problems
        // the old filesystem store had (keys lost on a redeploy/container rebuild onto a fresh
        // disk) and is what makes a *second* instance's cookies/auth-completion tickets valid too,
        // since they now all read the same key ring. DPAPI protection still machine-locks the keys
        // at rest, which is fine for the single Windows host this runs on today; before a genuine
        // multi-instance or cross-platform deployment, swap ProtectKeysWithDpapi() for
        // ProtectKeysWithCertificate() or Key Vault so every instance can decrypt the shared keys.
        var dpBuilder = builder.Services.AddDataProtection()
            .PersistKeysToDbContext<ApplicationDbContext>()
            .SetApplicationName("ResetYourFuture");

        if (OperatingSystem.IsWindows())
            dpBuilder.ProtectKeysWithDpapi();

        // --- Blazor SSR ---
        // AddCascadingAuthenticationState registers the ServerAuthenticationStateProvider
        // which reads auth state from the Blazor Server circuit's connection principal.
        // Without this, AuthenticationState cascades as anonymous even when the auth
        // cookie is present, because the interactive circuit cannot access HttpContext.
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                // SCALE-8: make circuit retention explicit and sized for this single-instance,
                // cohort-scale deployment instead of relying on the framework defaults (100
                // retained circuits / 3 min). Every disconnected circuit keeps its full
                // server-side UI state in RAM until reclaimed, so a tighter window bounds the
                // memory held for abandoned tabs while still allowing a real reconnect (dropped
                // Wi-Fi, laptop sleep). PERF-5 already removed the largest per-circuit payload
                // (base64 avatar data URLs).
                options.DisconnectedCircuitMaxRetained = 50;
                options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);
            });

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
    /// <summary>
    /// Fail fast like Jwt:Key (AuthenticationSetupExtensions.cs) — an unset/localhost SelfBaseUrl
    /// outside Development means every API-backed page silently renders empty (ApiClientBase
    /// swallows non-success responses) instead of throwing at startup.
    /// </summary>
    public static string ResolveSelfBaseUrl(string? configuredValue, bool isDevelopment)
    {
        if (isDevelopment)
            return configuredValue ?? "https://localhost:7090";

        if (string.IsNullOrWhiteSpace(configuredValue) || configuredValue.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SelfBaseUrl must be set to the application's real bound base address outside Development (configured value: '{configuredValue ?? "<unset>"}').");
        }

        return configuredValue;
    }

    private static void AddConsumerHttpClients(WebApplicationBuilder builder)
    {
        var selfBase = ResolveSelfBaseUrl(builder.Configuration["SelfBaseUrl"], builder.Environment.IsDevelopment());

        // A bounded per-request timeout (AVAIL-3) so a hung loopback call fails in seconds instead of
        // HttpClient's 100-second default; ApiClientBase catches the resulting TaskCanceledException and
        // degrades the page rather than crashing the circuit.
        void Configure(HttpClient c)
        {
            c.BaseAddress = new Uri(selfBase);
            c.Timeout = TimeSpan.FromSeconds(30);
        }

        // The assistant client streams a Server-Sent Events response that can legitimately run longer
        // than any request timeout, so it keeps the default (unbounded-enough) timeout — its bespoke
        // StreamChatAsync already catches connection failures itself.
        void ConfigureStreaming(HttpClient c) => c.BaseAddress = new Uri(selfBase);

        // Named client for dev-only endpoints (no auth handler needed)
        builder.Services.AddHttpClient("SelfClient", Configure);

        builder.Services.AddHttpClient<ICourseConsumer, CourseConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAssessmentConsumer, AssessmentConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<ISubscriptionConsumer, SubscriptionConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IProfileConsumer, ProfileConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminAnalyticsConsumer, AdminAnalyticsConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminUserConsumer, AdminUserConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminCourseConsumer, AdminCourseConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<ICategoryConsumer, CategoryConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminCategoryConsumer, AdminCategoryConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminModuleConsumer, AdminModuleConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminLessonConsumer, AdminLessonConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminAssessmentConsumer, AdminAssessmentConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<ICertificateConsumer, CertificateConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IBlogConsumer, BlogConsumer>(Configure);
        builder.Services.AddHttpClient<IAdminBlogConsumer, AdminBlogConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<ITestimonialConsumer, TestimonialConsumer>(Configure);
        builder.Services.AddHttpClient<IAdminTestimonialConsumer, AdminTestimonialConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IChatService, ChatService>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAssistantConsumer, AssistantConsumer>(ConfigureStreaming)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<INotificationConsumer, NotificationConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<ISearchConsumer, SearchConsumer>(Configure);
        builder.Services.AddHttpClient<IAdminCourseReviewConsumer, AdminCourseReviewConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IPathConsumer, PathConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminLearningPathConsumer, AdminLearningPathConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<ISessionConsumer, SessionConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
        builder.Services.AddHttpClient<IAdminSessionConsumer, AdminSessionConsumer>(Configure)
            .AddHttpMessageHandler<SsrApiHandler>();
    }
}
