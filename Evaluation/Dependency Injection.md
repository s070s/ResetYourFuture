Concern: DI composition root ownership

Correct way: Only `Api` and `Client` are composition roots. `Application` and `Domain` define abstractions but do not decide concrete implementations. `Infrastructure` provides implementations and registers via extension methods.

False example: `Application` registers `DbContext`, Stripe, email, and HTTP clients because “it needs them”.



Concern: Registration style (modular, feature-based)

Correct way: Use extension methods per layer/feature: `AddApplication()`, `AddInfrastructure(config)`, `AddApi()`, and optionally `AddCoursesFeature()`, `AddBillingFeature()`. Keep `Program.cs` small.

False example: `Program.cs` contains 300 lines of registrations and duplicated configuration across environments.



Concern: Service lifetimes (Scoped/Singleton/Transient)

Correct way:



\* `DbContext`: Scoped

\* Use-case handlers/services: Scoped (or Transient if stateless and no captured scoped deps)

\* HTTP clients: via `AddHttpClient` (managed)

\* Caches/config/options: Singleton

\* ViewModels (Blazor): Scoped \*per circuit\* (Server) or Scoped (WASM DI scope) and reset on navigation

&nbsp; False example: Singleton services depend on `DbContext` (scoped) or store per-user state in singleton.



Concern: Avoiding “service locator” anti-pattern

Correct way: Inject dependencies via constructor; keep `IServiceProvider` usage limited to rare factory scenarios.

False example: Services call `provider.GetService<T>()` everywhere, hiding dependencies and making tests brittle.



Concern: Options pattern for configuration

Correct way: Bind config into typed options with validation: `services.AddOptions<StripeOptions>().Bind(...).ValidateDataAnnotations().ValidateOnStart();` Inject `IOptionsMonitor<T>` where hot reload is needed.

False example: Read configuration values ad-hoc in random services via `configuration\["Stripe:Key"]`.



Concern: Cross-cutting concerns via decorators/pipelines

Correct way: Implement logging, validation, caching, retry, and transaction boundaries via middleware or application pipeline/decorators (e.g., MediatR behaviors or custom wrapper).

False example: Every handler manually logs, validates, and starts transactions (duplicated boilerplate).



Concern: EF abstraction injection

Correct way: Application depends on an abstraction (`IAppDbContext` or repository interfaces). Infrastructure registers `DbContext` as itself and as the abstraction: `services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());`

False example: Application directly injects `AppDbContext`, binding to Infrastructure.



Concern: Minimal API endpoint DI usage

Correct way: Endpoints accept dependencies as parameters (`(ICourseService svc, CancellationToken ct) => ...`) or use typed endpoint classes. Keep endpoints thin and test use-cases directly.

False example: Endpoints new-up services manually or fetch from `HttpContext.RequestServices`.



Concern: HttpClient configuration (Client and Server)

Correct way:



\* Client (Blazor WASM): `AddHttpClient("Api", c => c.BaseAddress = new(...))` + typed clients using that named client

\* Server: `AddHttpClient<StripeClient>()` etc. with policies (timeouts, retries)

&nbsp; False example: `new HttpClient()` in each ViewModel/service with no timeout and duplicated base URLs.



Concern: Auth propagation (Client -> Api)

Correct way: Centralize token attachment using a delegating handler (WASM) or `AuthorizationMessageHandler`; DI registers it once; typed clients use it.

False example: Each API call manually reads token from localStorage and sets headers, leading to inconsistency.



Concern: Per-request context injection

Correct way: Provide a DI service like `ICurrentUser` (reads claims from `HttpContext`) and `IClock`. Register `ICurrentUser` as Scoped.

False example: Business logic reads `HttpContext` directly or uses static globals for “current user”.



Concern: Background work and scoped dependencies

Correct way: Use `IHostedService`/`BackgroundService` with `IServiceScopeFactory` to create scopes; never inject scoped services into hosted singletons directly.

False example: `HostedService` injects `DbContext` directly and keeps it for the lifetime of the app.



Concern: ViewModel injection and separation (Blazor WASM)

Correct way: Register ViewModels as Scoped and inject feature API clients + state primitives. Keep ViewModels testable and UI-agnostic. Prefer `IMemoryCache`-like abstractions on client only if needed.

False example: ViewModels injected as Singleton with mutable state, causing cross-user/state leakage (or persistent stale data in WASM sessions).



Concern: Avoid circular dependencies across layers

Correct way: Enforce reference rules: `Api` references `Application`; `Infrastructure` references `Application`; `Application` references `Domain`; `Client` references `Shared`. Use analyzers/solution checks.

False example: `Application` references `Infrastructure` to access EF/Stripe; `Shared` references `Domain` entities.



Concern: Keyed services / multiple implementations

Correct way: Use named/keyed DI (or explicit factories) when multiple implementations exist (e.g., `IEmailSender` = SMTP vs SendGrid). Choose via options and register clearly.

False example: Multiple registrations for the same interface and “last one wins” accidentally in different environments.



Concern: Testing DI configuration

Correct way: Provide test host builders that swap Infrastructure implementations for fakes (e.g., in-memory DB, fake email sender) and validate service graph on startup.

False example: Tests rely on production DI with real Stripe/email and fail intermittently.



Concern: Preventing redundant registrations

Correct way: Centralize shared registrations in one place per layer; use `TryAdd` where appropriate; keep `ServiceCollection` extensions idempotent.

False example: The same service is registered in both `Api` and `Infrastructure` (different lifetimes), causing hard-to-debug behavior.



Concern: DI + EF interceptors and auditing

Correct way: Register EF interceptors (audit, soft delete, outbox) via DI and add them to DbContext options. Use scoped `ICurrentUser` and `IClock`.

False example: Audit fields set in controllers/endpoints manually or via static helpers.



Concern: DI for caching

Correct way: Define cache abstractions in Application (e.g., `ICourseCatalogCache`) and implement in Infrastructure (server) or Client (WASM) depending on need.

False example: Use `IMemoryCache` directly in Application, tying it to ASP.NET runtime and preventing reuse/testing.



Concern: Explicit “composition root” for each executable

Correct way:



\* `Api` registers: Application + Infrastructure + auth + endpoints

\* `Client` registers: typed API clients + ViewModels + localization + auth handlers

&nbsp; Both are the only places that wire concrete implementations.

&nbsp; False example: ViewModels register server services; server registers client concerns; cross-wiring spreads everywhere.



