# Audit: Architecture

| | |
|---|---|
| Finding prefix | ARCH |
| Created | 2026-07-11 |
| Scope | Solution/project structure, layering and dependency direction, the SSR-over-loopback-HTTP design, authentication architecture, real-time (SignalR) feature placement, DI lifetime strategy — structural decisions and their tradeoffs |
| Delegated | Security vulnerabilities → SEC (25); runtime performance cost quantification → PERF (34); multi-instance/scale-out blockers → SCALE (35); DB schema & EF usage → DB (30); API wire conventions → API (31); config validation → CFG (39); micro duplication/naming → CQ (22); cost-of-change & hand-maintained artifacts → MAINT (23); test design → TEST (24) |

## 1. Methodology

Examined: `ResetYourFuture.sln` layout (5 src + 5 test projects), all 10 `.csproj` files plus `Directory.Build.props` / `Directory.Packages.props` / `global.json`; `src/ResetYourFuture.Web/Program.cs` and all six `Startup/*.cs` extension classes; the full consumer stack (`Consumers/ApiClientBase.cs`, `Consumers/CourseConsumer.cs`, registration in `Startup/ServiceRegistrationExtensions.cs`); the auth stack (`Startup/AuthenticationSetupExtensions.cs`, `Services/SsrApiHandler.cs`, `Services/ApiTokenProvider.cs`, `Infrastructure/Services/AuthService.cs`, `Infrastructure/ApiServices/TokenService.cs`, `Startup/InfrastructureEndpointsExtensions.cs`); representative vertical slices (CoursesController → CourseService → CourseConsumer; AdminController); the calls/chat subsystem (`Hubs/ChatHub.cs`, `Hubs/CallHub.cs`, `Services/CallRegistry.cs`, `Services/CallRingMonitor.cs`, `Services/ChatService.cs`); `Infrastructure/Data/ApplicationDbContext.cs`; namespace declarations across all src projects (grep tally); DI injection tally across all `.razor`/`.razor.cs` files.

NOT examined: `dotnet build`/`restore` was deliberately not run (a known NuGet auto-pin behavior can rewrite `ResetYourFuture.Web.csproj`/`Directory.Packages.props` with an incompatible `Microsoft.OpenApi` version; this audit must not modify files outside `docs/plans/`). Runtime behavior was not exercised; findings are from static reading.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 3 |
| Info | 2 |

> **Accepted since audit (out of scope — will not implement):** ARCH-1 (SSR renders through a loopback HTTP call to the app's own REST API). This is the project's signature architectural decision, and — as the finding itself concluded — a defensible, arguably clever tradeoff for a certificate project whose brief includes a documented REST API: the app is its own first API consumer, there is one authorization surface, and `WebApplicationFactory` tests exercise the real path. Reversing it (in-process consumers behind the existing interfaces) is a large refactor the project does not need, so it is consciously accepted as a documented tradeoff rather than fixed. Its day-to-day cost centres — MAINT-1 (change amplification) and PERF-1 (per-call loopback cost) — remain, with PERF-1 likewise accepted; MAINT-1 has since been mitigated (shared mapping layer). The sibling accepted findings carry matching notes: [34-audit-performance.md](34-audit-performance.md) (PERF-1), [35-audit-scalability.md](35-audit-scalability.md) (SCALE-1/2/3), [36-audit-availability.md](36-audit-availability.md) (AVAIL-4).

The solution is in far better architectural shape than a typical certificate project. Layering is real: dependency direction is strictly Domain ← Application ← Infrastructure ← Web with no cycles, each project owns its namespace root (an earlier "everything in `ResetYourFuture.Web.*`" quirk has been fully fixed — zero `ResetYourFuture.Web` namespaces remain outside the Web project), startup is decomposed into six focused extension classes, and the "pages talk only to typed consumers" convention is held everywhere it was checked. The signature decision — Blazor SSR rendering by calling the app's own REST API over a loopback HttpClient — is unusual but not naive: it demonstrably bought a complete, documented, integration-tested API surface with one authorization model. All four Medium findings from the original audit — drifted JWT-minting sites, the undocumented Domain FrameworkReference, chat writes bypassing Application, and the pipe-delimited auth-completion payload — are fixed; the findings below are its remaining recurring taxes (per-request re-authentication, silent-empty-render failure mode, production TLS coupling) and design notes.

## 3. Findings

### ARCH-6: IApplicationDbContext is a leaky abstraction — Identity tables in the interface, UserManager throughout Application  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Application/Data/IApplicationDbContext.cs:32-33` (`DbSet<IdentityUserRole<string>>` / `DbSet<IdentityRole>` exposed "used by ChatQueryService for role lookups"); six Application services inject `UserManager<ApplicationUser>` directly (`AdminUserService`, `AuthApiService`, `CallEventService`, `CallQueryService`, `ChatQueryService`, `ProfileService`); `src/ResetYourFuture.Application/ResetYourFuture.Application.csproj` (FrameworkReference + implicit `Microsoft.AspNetCore.Identity` using).
- **Impact:** The interface implies "Application depends on an abstract persistence surface", but Application is in fact committed to EF Core + ASP.NET Identity concretely. That is fine — but the half-abstraction costs indirection without buying substitutability (no one could implement `IApplicationDbContext` except the real `ApplicationDbContext`).
- **Recommendation:** Accept and document: `IApplicationDbContext` exists to enable the InMemory/SQLite test swap and interface-based DI, not provider independence. No code change needed; adjust expectations (and stop short of adding more Identity surface to it — prefer `UserManager` where role/user logic is needed).

### ARCH-7: Global InteractiveServer render mode forecloses per-page static SSR  [Low] [Effort: L]
- **Evidence:** `src/ResetYourFuture.Web/Program.cs:95-96` (`MapRazorComponents<App>().AddInteractiveServerRenderMode()` — applied globally via `<Routes>`); the deferred-cookie auth-completion handshake (`/auth/complete`) exists precisely because every page runs in a circuit.
- **Impact:** No page can receive a real form POST (blocks browser save-password prompts on login, requires the `/auth/complete` redirect dance), every page holds a circuit even when purely static (Home, Pricing, blog articles), and per-page render-mode optimization is off the table without a cross-cutting refactor. The upside is uniformity: one mental model, no per-page mode matrix, and the consumer/token machinery only has to solve the circuit case once.
- **Recommendation:** Leave as-is for the certificate (a per-page render-mode refactor was previously evaluated and declined as cross-cutting). Record the constraint where future work will see it; if SEO/first-paint of public pages ever matters, Home/Pricing/Blog are the candidates to move to static SSR first.

### ARCH-8: Divergent DI lifetimes for the two hub-owning services, with correctness resting on comments  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Startup/ServiceRegistrationExtensions.cs:73-77` (`ICallService` deliberately **scoped** — "so CallOverlayHost and chat components share one instance/hub/state per circuit", and `PresenceService` scoped to piggyback on it) versus line 250-251 (`IChatService`/`ChatService` registered via `AddHttpClient<,>`, i.e. **transient** — each injection point gets its own instance and therefore its own `HubConnection`).
- **Impact:** Two services solving the same problem (hold a SignalR client connection for the circuit) use opposite lifetimes; the safe usage pattern ("only one component may inject IChatService per circuit") is enforced by nothing. A second injection of `IChatService` in some future component would silently open a second hub connection per user. The invariant lives only in registration comments.
- **Recommendation:** Register `ChatService` scoped like `CallService` and give it its `HttpClient` via `IHttpClientFactory` internally (or a scoped wrapper over a transient typed client). Alternatively add a DI fitness test asserting the intended lifetimes (see TEST 24).

### ARCH-9: The "pages only talk to consumers" boundary is convention-only — currently held, structurally unenforced  [Info]
- **Evidence:** Injection tally across all `Pages/**`, `Shared/**`, `Layout/**` components: only consumer interfaces, `ICallService`/`IChatService`, `ApiTokenProvider`, and `IAuthService` appear; zero pages inject `ICourseService`, `ISubscriptionService`, or any other Application service directly (verified by grep across `src/ResetYourFuture.Web/Pages`). `IAuthService` in six pages (`Login`, `Register`, `Profile`, `AdminUsers`, `LessonViewer`, `ForgotPassword`) is the documented, deliberate exception (cookie flow + media-token minting cannot go over the API).
- **Impact:** None today — the discipline is impressively consistent. But nothing except review vigilance stops the next page from injecting an Application service and silently bypassing the API/authz surface that ARCH-1 pays for.
- **Recommendation:** One architecture-fitness test (assert no type in `ResetYourFuture.Web.Pages` references `ResetYourFuture.Application.ApiServices`, allow-listing the `IAuthService` exception) would convert the convention into a checked invariant. TEST (24) owns the test recommendation.

### ARCH-10: Calls feature: live state is a Web-layer in-memory singleton, persistence is Application — a coherent but process-local design  [Info]
- **Evidence:** `src/ResetYourFuture.Web/Services/CallRegistry.cs` (491-line pure, lock-protected state machine, deliberately dependency-free "so it is fully unit-testable"); `src/ResetYourFuture.Web/Services/CallRingMonitor.cs` (polls the registry, sweeps "dangling CallSession rows left open by a previous process at startup, since CallRegistry … loses all state on restart"); `CallEventService`/`CallQueryService` in Application handle persistence/reads; `Hubs/CallHub.cs` (456 lines) + `CallHub.Signaling.cs` translate.
- **Impact:** This is the best-factored subsystem in the codebase — the registry is testable in isolation and the layer responsibilities are explicit. The structural consequence to be aware of: call/presence truth is process memory, so the feature is single-instance by construction (the code acknowledges this via the startup sweep). SCALE (35) owns the multi-instance question; no action needed at this project's scope.
- **Recommendation:** None. ChatHub now follows this same read/write split (`ChatCommandService`/`ChatQueryService` in Application, hub keeps only transport concerns).

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| ARCH-6 | Low | S | Document IApplicationDbContext as a test-swap seam, not provider abstraction |
| ARCH-8 | Low | S | Make ChatService scoped (match CallService) or pin lifetimes with a DI test |
| ARCH-7 | Low | L | Accept global InteractiveServer; record the constraint; revisit only if public-page SSR matters |
| ARCH-9 | Info | S | Architecture-fitness test for the pages→consumers rule |
| ARCH-10 | Info | — | None; reference pattern ChatHub now follows |

## 5. Related Findings Elsewhere

- **MAINT (23)** owns the change-amplification cost of the layered loopback stack (one new field touches entity→DTO→service→controller→consumer→page→resx) and the hand-maintained consumer/Designer artifacts — the day-to-day tax of ARCH-1.
- **CQ (22)** owns the micro-level symptoms: duplicated bilingual-fallback ternaries, the two competing ServiceResult conventions, duplicated returnUrl sanitization in the minimal endpoints.
- **TEST (24)** owns the InMemory-vs-relational integration-test gap, the missing e2e layer for circuit-only flows (the auth-completion handshake and global InteractiveServer render mode have no browser-level test), and the proposed architecture/DI fitness tests.
- **SEC (25)** owns JWT key handling, cookie flags, the `access_token`-in-query-string allowance, and impersonation security around the auth-completion and token-minting flows.
- **PERF (34)** owns quantifying the loopback per-request tax (serialization, TLS, `OnTokenValidated` DB lookup per API call) identified in ARCH-1.
- **SCALE (35)** owns the single-instance constraints (CallRegistry in-memory state, filesystem DataProtection keys) acknowledged in ARCH-10.
- **API (31)** owns wire-format consistency of error responses produced by the mixed ServiceResult mappings.
- **DB (30)** owns the schema-level consequences of decisions noted here (e.g. `DateTimeOffset`→string conversion, soft-delete filters) — see also TEST (24) for why that conversion exists.
