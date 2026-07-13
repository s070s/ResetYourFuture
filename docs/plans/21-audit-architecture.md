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
| Medium | 4 |
| Low | 3 |
| Info | 2 |

> **Accepted since audit (out of scope — will not implement):** ARCH-1 (SSR renders through a loopback HTTP call to the app's own REST API). This is the project's signature architectural decision, and — as the finding itself concluded — a defensible, arguably clever tradeoff for a certificate project whose brief includes a documented REST API: the app is its own first API consumer, there is one authorization surface, and `WebApplicationFactory` tests exercise the real path. Reversing it (in-process consumers behind the existing interfaces) is a large refactor the project does not need, so it is consciously accepted as a documented tradeoff rather than fixed. Its day-to-day cost centres — MAINT-1 (change amplification) and PERF-1 (per-call loopback cost) — remain, with PERF-1 likewise accepted; MAINT-1 has since been mitigated (shared mapping layer). The sibling accepted findings carry matching notes: [34-audit-performance.md](34-audit-performance.md) (PERF-1), [35-audit-scalability.md](35-audit-scalability.md) (SCALE-1/2/3), [36-audit-availability.md](36-audit-availability.md) (AVAIL-4).

The solution is in far better architectural shape than a typical certificate project. Layering is real: dependency direction is strictly Domain ← Application ← Infrastructure ← Web with no cycles, each project owns its namespace root (an earlier "everything in `ResetYourFuture.Web.*`" quirk has been fully fixed — zero `ResetYourFuture.Web` namespaces remain outside the Web project), startup is decomposed into six focused extension classes, and the "pages talk only to typed consumers" convention is held everywhere it was checked. The signature decision — Blazor SSR rendering by calling the app's own REST API over a loopback HttpClient — is unusual but not naive: it demonstrably bought a complete, documented, integration-tested API surface with one authorization model. The findings below are mostly about the recurring taxes that decision levies (per-request re-authentication, silent-empty-render failure mode, production TLS coupling) and about places where the architecture's own rules are quietly bypassed (chat writes, four parallel JWT-minting sites).

## 3. Findings

### ARCH-2: Four independent JWT/claims-minting sites, and they have already drifted  [Medium] [Effort: M]
- **Evidence:** (1) `src/ResetYourFuture.Infrastructure/ApiServices/TokenService.cs:43-121` (API login tokens; includes a `"status"` claim at line 58); (2) `src/ResetYourFuture.Web/Services/SsrApiHandler.cs:35-55` (re-signs whatever claims the cookie principal carries); (3) `src/ResetYourFuture.Infrastructure/Services/AuthService.cs` (mints JWTs from a principal for the circuit path — consumed via `src/ResetYourFuture.Web/Services/ApiTokenProvider.cs` and `ChatService`'s `AccessTokenProvider`); (4) `src/ResetYourFuture.Web/Startup/InfrastructureEndpointsExtensions.cs:125-137` (`/auth/complete` builds the cookie principal's claim list by hand — it has **no** `"status"` claim).
- **Impact:** Drift is not hypothetical: a JWT obtained via `POST /api/auth/login` carries `"status"`, while a JWT minted inside the circuit from the cookie principal (sites 2/3, whose claims come from site 4) does not. Any future controller reading `"status"` will behave differently depending on which door the caller came through. Every new claim must be added in up to four places; forgetting one produces exactly this class of asymmetry. SEC (25) owns the key-handling aspects; this finding is about the structure.
- **Recommendation:** Extract one claims-builder (`ApplicationUser`/`ClaimsPrincipal` → `List<Claim>`) and one "sign these claims" function, and have all four sites call them. The `/auth/complete` endpoint and `TokenService` should share the claims-builder verbatim.

### ARCH-3: The Domain project takes a FrameworkReference to all of ASP.NET Core  [Medium] [Effort: L]
- **Evidence:** `src/ResetYourFuture.Domain/ResetYourFuture.Domain.csproj` (`<FrameworkReference Include="Microsoft.AspNetCore.App" />`); `src/ResetYourFuture.Domain/Identity/ApplicationUser.cs` (`ApplicationUser : IdentityUser`, i.e. the aggregate at the center of the model is an ASP.NET Identity type); `src/ResetYourFuture.Domain/Extensions/UserSearchExtensions.cs` (EF-translation-aware `IQueryable` sort/search logic living in Domain).
- **Impact:** The layer named "Domain" carries the entire web framework's surface area, so nothing structurally prevents domain entities from acquiring HTTP, Identity, or hosting dependencies over time — the guarantee the project split advertises does not actually exist. In exchange, Identity integrates without a mapping layer and navigation properties from `ApplicationUser` to domain entities (`Enrollments`, `RefreshTokens`, …) work directly. For this codebase that is a reasonable pragmatic call; the problem is that it is implicit.
- **Recommendation:** Do not attempt the "pure domain + separate Identity user + mapping" refactor — it is expensive and buys little here. Instead make the rule explicit and cheap to hold: state in the Domain project (one comment in the csproj or an ADR) that the FrameworkReference exists solely for `IdentityUser`, and consider moving `UserSearchExtensions` (query-shaping, not domain rules) to Application. A NetArchTest-style fitness test could pin "Domain types reference only Identity from AspNetCore" (see TEST 24).

### ARCH-4: Chat message writes bypass the Application layer entirely — the hub persists directly  [Medium] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/Hubs/ChatHub.cs:68-150` (`SendMessage`, `MarkAsRead` validate, enforce the 4,000-char cap, and write `ChatMessage` rows through `IApplicationDbContext` inline); `src/ResetYourFuture.Application/ApiServices/ChatQueryService.cs` (Application layer handles only the read side).
- **Impact:** The architecture's central claim (ARCH-1's benefit: one business/authz surface in Application services behind controllers) does not hold for the real-time write path. Chat business rules (message length, membership checks) live in a Web-layer hub where they cannot be reached by the API, by other services, or by Application-level unit tests; the read/write split across layers is confusing to navigate ("where is chat logic?" has two answers in two projects).
- **Recommendation:** Extract a `ChatCommandService` (Application, sibling of `ChatQueryService`) owning validation + persistence; the hub keeps only connection/group management and broadcasting. The Call feature already models this split better (`CallEventService`/`CallQueryService` in Application; see ARCH-10).

### ARCH-5: The Blazor auth-completion handshake is a bespoke pipe-delimited string protocol between two distant files  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Infrastructure/Services/AuthService.cs` (mints the DataProtection ticket; format defined implicitly at the write site) and `src/ResetYourFuture.Web/Startup/InfrastructureEndpointsExtensions.cs:84-96` (parses `"{userId}|{adminBackupId}|{deleteAdminBackup}|{securityStamp}|{rememberMe}"` by `Split('|')` with a `parts.Length != 5` guard; the format lives in a comment).
- **Impact:** Producer (Infrastructure) and consumer (Web minimal endpoint) are coupled through an undocumented-by-types wire format. Adding a sixth field is a two-file, order-sensitive edit whose failure mode is every login redirecting to `/login?error=session_expired`. The flow itself (deferred cookie issuance via `/auth/complete` because circuits cannot set cookies) is a correct and well-commented solution to a real Blazor Server constraint — the fragility is only in the payload encoding.
- **Recommendation:** Replace the pipe string with a small `record AuthCompletionTicket(...)` serialized as JSON inside the same time-limited protector, shared from one project so both sites compile against the same shape.

### ARCH-6: IApplicationDbContext is a leaky abstraction — Identity tables in the interface, UserManager throughout Application  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Application/Data/IApplicationDbContext.cs:32-33` (`DbSet<IdentityUserRole<string>>` / `DbSet<IdentityRole>` exposed "used by ChatQueryService for role lookups"); six Application services inject `UserManager<ApplicationUser>` directly (`AdminUserService`, `AuthApiService`, `CallEventService`, `CallQueryService`, `ChatQueryService`, `ProfileService`); `src/ResetYourFuture.Application/ResetYourFuture.Application.csproj` (FrameworkReference + implicit `Microsoft.AspNetCore.Identity` using).
- **Impact:** The interface implies "Application depends on an abstract persistence surface", but Application is in fact committed to EF Core + ASP.NET Identity concretely. That is fine — but the half-abstraction costs indirection without buying substitutability (no one could implement `IApplicationDbContext` except the real `ApplicationDbContext`).
- **Recommendation:** Accept and document: `IApplicationDbContext` exists to enable the InMemory/SQLite test swap and interface-based DI, not provider independence. No code change needed; adjust expectations (and stop short of adding more Identity surface to it — prefer `UserManager` where role/user logic is needed).

### ARCH-7: Global InteractiveServer render mode forecloses per-page static SSR  [Low] [Effort: L]
- **Evidence:** `src/ResetYourFuture.Web/Program.cs:95-96` (`MapRazorComponents<App>().AddInteractiveServerRenderMode()` — applied globally via `<Routes>`); the deferred-cookie machinery of ARCH-5 exists precisely because every page runs in a circuit.
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
- **Recommendation:** None. Noted as the pattern ChatHub should be brought in line with (ARCH-4).

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| ARCH-5 | Medium | S | Replace the pipe-delimited auth ticket with a shared record serialized inside the protector |
| ARCH-2 | Medium | M | Single claims-builder + single JWT-mint function; converge all four sites (fixes the `"status"` claim drift) |
| ARCH-4 | Medium | M | Extract ChatCommandService (Application); ChatHub keeps only transport concerns |
| ARCH-3 | Medium | L | Document the Domain FrameworkReference as Identity-only; move UserSearchExtensions to Application; optionally pin with a fitness test |
| ARCH-6 | Low | S | Document IApplicationDbContext as a test-swap seam, not provider abstraction |
| ARCH-8 | Low | S | Make ChatService scoped (match CallService) or pin lifetimes with a DI test |
| ARCH-7 | Low | L | Accept global InteractiveServer; record the constraint; revisit only if public-page SSR matters |
| ARCH-9 | Info | S | Architecture-fitness test for the pages→consumers rule |
| ARCH-10 | Info | — | None; reference pattern for ARCH-4 |

## 5. Related Findings Elsewhere

- **MAINT (23)** owns the change-amplification cost of the layered loopback stack (one new field touches entity→DTO→service→controller→consumer→page→resx) and the hand-maintained consumer/Designer artifacts — the day-to-day tax of ARCH-1.
- **CQ (22)** owns the micro-level symptoms: duplicated bilingual-fallback ternaries, the two competing ServiceResult conventions, duplicated returnUrl sanitization in the minimal endpoints.
- **TEST (24)** owns the InMemory-vs-relational integration-test gap, the missing e2e layer for circuit-only flows (the ARCH-5/7 handshake has no browser-level test), and the proposed architecture/DI fitness tests.
- **SEC (25)** owns JWT key handling, cookie flags, the `access_token`-in-query-string allowance, and impersonation security around the flows described in ARCH-2/5.
- **PERF (34)** owns quantifying the loopback per-request tax (serialization, TLS, `OnTokenValidated` DB lookup per API call) identified in ARCH-1.
- **SCALE (35)** owns the single-instance constraints (CallRegistry in-memory state, filesystem DataProtection keys) acknowledged in ARCH-10.
- **API (31)** owns wire-format consistency of error responses produced by the mixed ServiceResult mappings.
- **DB (30)** owns the schema-level consequences of decisions noted here (e.g. `DateTimeOffset`→string conversion, soft-delete filters) — see also TEST (24) for why that conversion exists.
