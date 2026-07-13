# Audit: Testability

| | |
|---|---|
| Finding prefix | TEST |
| Created | 2026-07-11 |
| Scope | Test coverage shape, test design and infrastructure (factories, providers, fixtures), what is structurally untestable today, gaps by layer (unit / integration / component / e2e), test-affecting production decisions |
| Delegated | CI pipeline design & gates → BUILD (40); security test scenarios → SEC (25); DB schema consequences of provider choices → DB (30); structural decisions being tested-around → ARCH (21); micro code quality in src → CQ (22) |

## 1. Methodology

Examined: all 57 test files across the five test projects (17 Application.Tests, 3 Domain.Tests, 8 Infrastructure.Tests, 26 Web.Tests, 3 TestSupport) — inventory plus full reads of `tests/ResetYourFuture.Web.Tests/CustomWebAppFactory.cs`, `CrossCuttingTests.cs`, `AdminCrudAuthMatrixTests.cs` (head), `ConsumerTests.cs` (head), `CallHubTests.cs` (head), `MinimalEndpointsTests.cs` (targeted), and `tests/ResetYourFuture.TestSupport/DbContextFactory.cs`; test project csproj files and `tests/Directory.Build.props`; grep tallies (≈712 lines carrying `[Fact]`/`[Theory]`, 18 classes in the `"web"` collection, SQLite usage in 4 test files); production seams that tests depend on (`Program.cs` partial-class exposure, `DatabaseSeedingExtensions` `IsRelational()` guard, `ApplicationDbContext.ConfigureConventions`); searched the whole repo for e2e/Playwright/bUnit artifacts (none exist); `.github/workflows/tests.yml` (tests run on push/PR with dummy env secrets).

NOT examined: the suite was not executed and coverage was not measured (no coverage tooling exists in the repo — see TEST-9 — and building/restoring was deliberately avoided because NuGet restore has previously rewritten tracked project files via a `Microsoft.OpenApi` auto-pin). Assertions about what tests cover are from reading the tests, not from run results.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 1 |
| Medium | 2 |
| Low | 4 |
| Info | 1 |

> **Fixed since audit:** TEST-5 (Medium — the production schema was bent to the test provider: `DateTimeOffset` stored as strings so SQLite could sort) — the `DateTimeOffsetToStringConverter` is now applied only under the SQLite provider (`ApplicationDbContext.ConfigureConventions` branches on `Database.ProviderName`), and a migration restored native `datetimeoffset` on SQL Server (owned by DB-2). The test provider no longer dictates production storage. TEST-1 (High — the integration suite ran entirely on EF InMemory, so relational behaviors were never integration-tested) — added `SqliteWebAppFactory`, a `CustomWebAppFactory` variant backed by a real relational SQLite database (shared open connection + `EnsureCreated`, via a new provider branch in `DatabaseSeedingExtensions`), plus `SqliteRelationalIntegrationTests` proving the enrollment `(UserId, CourseId)` unique index is enforced on the relational host (the premise of `CourseService.EnrollAsync`'s `DbUpdateException` catch) while InMemory silently allows the duplicate. The migration chain — which no test previously applied — is now covered by `MigrationChainTests`: a provider-correct, always-on `HasPendingModelChanges` guard against forgotten migrations, plus a LocalDB-gated test that applies the full chain end-to-end (self-skips where LocalDB is absent).

For a solo certificate project, the automated test estate is genuinely strong: ~700 test cases across four layers, real-pipeline integration tests through `WebApplicationFactory<Program>` that exercise the same loopback API the app itself uses, a purpose-built pure state machine (`CallRegistry`) that makes the hardest feature unit-testable, a hand-rolled SignalR hub harness, consumer tests against a stubbed handler, an auth-matrix suite, and CI running everything on every push. Relational behavior is no longer entirely simulated — a SQLite-backed factory variant now exists for constraint-sensitive suites and the migration chain is verified against LocalDB (TEST-1, fixed) — though most integration classes still run on InMemory by default. The remaining structural hole is browser-side: everything is untested there (no e2e and no component tests, leaving the Blazor circuit — where this app's trickiest behavior lives — entirely to manual verification). One production decision made *for* testability (storing `DateTimeOffset` as strings so SQLite can sort) deserves a conscious revisit.

## 3. Findings

### TEST-2: Zero end-to-end/browser tests, while the app's riskiest logic is circuit-only  [High] [Effort: L]
- **Evidence:** Repo-wide search finds no Playwright/Selenium/e2e artifacts and no bUnit (`Directory.Packages.props` contains neither). The behaviors that cannot be reached by `WebApplicationFactory` HTTP calls: the login → DataProtection ticket → `/auth/complete` redirect → cookie → circuit re-auth chain (only its server halves are tested, `MinimalEndpointsTests.cs:41-91`); every consumer-driven page render (blank-page failure mode of `ApiClientBase`); presence/chat/call UI flows; and 438 lines of JS interop (`src/ResetYourFuture.Web/wwwroot/js/webrtc-interop.js` alone is 332 lines with no test of any kind). Git history shows circuit-level defects are this project's dominant bug class (e.g. commit `2dbe6a6` "Make video calls fully working: … fix WebRTC connection bugs").
- **Impact:** The demo-critical paths — a user logging in, a page actually showing data, a call connecting — have no automated safety net. Because ApiClientBase renders failures as empty UI rather than errors, a regression here passes every existing test and manifests only when a human clicks through.
- **Recommendation:** Add a small Playwright smoke suite (login completes and lands authenticated; one consumer-backed page shows seeded data; two-context call connects with fake media — the two-browser-context + `--use-fake-device-for-media-stream` recipe has already been proven against this app in prior manual verification and just needs to be committed into the repo, e.g. `tests/e2e/`). Even 5 scenarios would cover more real risk than doubling the unit suite.

### TEST-3: All 18 integration classes share one serialized collection and one long-lived InMemory database  [Medium] [Effort: M]
- **Evidence:** `tests/ResetYourFuture.Web.Tests/CustomWebAppFactory.cs:187-188` (`[CollectionDefinition("web")]` + `ICollectionFixture<CustomWebAppFactory>`); 18 test classes carry `[Collection("web")]` (grep-verified). xUnit runs a collection's classes sequentially, and every class shares the same `_dbName` store for the factory's lifetime — state accumulates across the whole run (mitigated, but only by convention, via GUID-unique emails and fresh entities per test).
- **Impact:** No parallelism across the largest test project (wall-clock grows linearly forever), and any test that mutates broadly-scoped data (site settings, seeded plans, admin user) can poison later classes in ways that appear as order-dependent flakes. `SiteSettingsIntegrationTests` mutating a singleton-ish table in a shared DB is the canonical hazard.
- **Recommendation:** Split into a few smaller collections (auth/admin/content/realtime) each with its own factory, or move to per-class `IClassFixture<CustomWebAppFactory>` — the factory already generates a unique DB name per instance, so isolation is one attribute change per class; pay the extra boot cost only if runtime allows.

### TEST-4: No component-level tests for 70 Razor components and ~4,900 lines of code-behind  [Medium] [Effort: M]
- **Evidence:** No bUnit reference anywhere in the solution. High-logic code-behinds are entirely untested: `src/ResetYourFuture.Web/Pages/AdminCourseEdit.razor.cs` (405 lines), `AdminAssessmentEdit.razor.cs` (357), `Chat.razor.cs` (316), `LessonViewer.razor.cs` (315), plus `Home.razor.cs`'s persist/restore hydration logic (`SetParametersAsync` state juggling, lines 43-60). The consistent code-behind pattern means the logic is *already* in plain C# classes — well-positioned for bUnit.
- **Impact:** Render-state machines (loading/empty/error branches), pagination handlers, and the prerender-persistence logic are verified only by eyeball. These are exactly the components the e2e gap (TEST-2) also misses, so page logic currently sits in a double blind spot.
- **Recommendation:** Introduce bUnit for the top-5 logic-heavy components; components inject consumer *interfaces* (per the architecture's own rule), so NSubstitute doubles drop in with no production change. Prioritize `LessonViewer` (completion flow) and `Chat` (hub event handling).

### TEST-6: Auth-plumbing and infrastructure utilities have no direct tests  [Low] [Effort: S]
- **Evidence:** No test file references `SsrApiHandler` (the JWT-minting DelegatingHandler every SSR API call passes through — `src/ResetYourFuture.Web/Services/SsrApiHandler.cs`), `FileLogger`/`FileLoggerProvider` (`src/ResetYourFuture.Web/Logging/`), or `EnvFileLoader` (`src/ResetYourFuture.Web/Startup/EnvFileLoader.cs` — pure parsing logic, trivially unit-testable). `ApiTokenProvider` appears in tests only as a hand-built stub (`ConsumerTests.cs:27-33`). By contrast the `/auth/complete` endpoint *is* covered (`MinimalEndpointsTests.cs:41-91`: happy path, garbage ticket, signout, culture) — credit where due.
- **Impact:** SsrApiHandler is on the hot path of every server-side render; a claims/expiry regression there breaks all pages at once and would surface only as blank UI (see TEST-2's failure-mode note). EnvFileLoader bugs change configuration silently. FileLogger failures lose diagnostics exactly when needed (LOG 37 owns logger quality; the *coverage* gap is recorded here).
- **Recommendation:** Three cheap unit suites: SsrApiHandler with a stub inner handler + fake `IHttpContextAccessor` (assert Bearer attached/omitted, claims copied); EnvFileLoader against temp files (comments, blank lines, `=`-in-value, missing file); FileLoggerProvider smoke (writes, rotation behavior if any).

### TEST-7: CustomWebAppFactory triplicates its user-provisioning block  [Low] [Effort: S]
- **Evidence:** `tests/ResetYourFuture.Web.Tests/CustomWebAppFactory.cs:73-158` — `CreateAuthenticatedClientAsync`, `CreateAuthenticatedClientWithIdAsync`, and `CreateAuthenticatedClientWithPlanAsync` repeat the same create-user → add-role → mint-token → attach-header sequence with minor deltas.
- **Impact:** Any change to test-user shape (a new required `ApplicationUser` field, a different token call) is a three-place edit in the single most-depended-on test helper; the copies have already begun to differ in what they return.
- **Recommendation:** One private `ProvisionUserAsync(role, tier?)` returning `(HttpClient, string userId)`; the three public helpers become one-liners. (Test-code duplication is homed here rather than CQ 22 by the primary-home rule.)

### TEST-8: The factory configures the app via process-wide environment variables in a static constructor  [Low] [Effort: S]
- **Evidence:** `tests/ResetYourFuture.Web.Tests/CustomWebAppFactory.cs:34-43` — `Environment.SetEnvironmentVariable` for `Jwt__Key`, `AdminUser__Password`, `ConnectionStrings__DefaultConnection`, etc., mutating the entire test process rather than the factory's host.
- **Impact:** Order-and-process-global: any future second factory (e.g. a SQLite variant from TEST-1, or one testing *missing*-config behavior) inherits these values invisibly and cannot test their absence; values also leak into any non-web test that happens to read configuration in the same process.
- **Recommendation:** Move them into `ConfigureWebHost` via `builder.UseSetting(...)` or `ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(...))` — same effect, scoped to the host under test.

### TEST-9: No coverage measurement anywhere  [Low] [Effort: S]
- **Evidence:** `.github/workflows/tests.yml` runs `dotnet test` with a TRX logger only; no coverlet/`--collect:"XPlat Code Coverage"` and no coverage packages in `Directory.Packages.props`.
- **Impact:** Coverage claims (including this report's) rest on reading, not measurement; blind spots like the ones in TEST-4/6 stay invisible between audits. For a certificate project a number is also cheap evidence of rigor.
- **Recommendation:** Add `coverlet.collector` to the test projects and `--collect:"XPlat Code Coverage"` to the CI step (report upload optional). No threshold gate needed — measurement first. BUILD (40) owns the pipeline change itself.

### TEST-10: Architecture invariants that the codebase relies on are not test-enforced  [Info]
- **Evidence:** Three comment-enforced rules with no fitness tests: pages must inject only consumers (currently true everywhere — see ARCH-9 in report 21); DI lifetimes of `ICallService`/`PresenceService`/`ApiTokenProvider` must stay scoped (`Startup/ServiceRegistrationExtensions.cs:73-83`; MAINT-10 in report 23); Domain may reference AspNetCore only for Identity (ARCH-3).
- **Impact:** Each rule fails silent-and-subtle when broken. All three are assertable in a few lines each (reflection over the DI container the factory already builds; NetArchTest or plain reflection for layering).
- **Recommendation:** One `ArchitectureTests.cs` in Web.Tests: resolve `IServiceCollection` via the existing factory, assert lifetimes; assert no `Pages/**` type references `Application.ApiServices` (allow-list `IAuthService`); optionally assert Domain's referenced assemblies.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| TEST-2 | High | L | Commit a 5-scenario Playwright smoke suite (login, data page, call connect) into the repo |
| TEST-6 | Low→quick win | S | Unit tests for SsrApiHandler, EnvFileLoader, FileLoggerProvider |
| TEST-3 | Medium | M | Break the single "web" collection into isolated fixtures/collections |
| TEST-4 | Medium | M | bUnit for the top-5 logic-heavy components |
| TEST-7 | Low | S | Deduplicate CustomWebAppFactory provisioning helpers |
| TEST-8 | Low | S | Replace static-ctor env vars with per-host configuration |
| TEST-9 | Low | S | Add coverage collection to CI |
| TEST-10 | Info | S | Architecture/DI fitness tests |

## 5. Related Findings Elsewhere

- **ARCH (21)** owns the structural decisions this suite tests around: the loopback self-API (whose silent-`default` consumers make failures invisible to e2e-less testing), the convention-only pages→consumers boundary (TEST-10 supplies the enforcement), and hub-inline chat writes that keep chat business logic out of unit-testable Application services.
- **MAINT (23)** owns the comment-enforced DI lifetime invariants (MAINT-10) that TEST-10 would pin, and the change-ripple that makes forgotten test updates likely.
- **CQ (22)** owns production-code duplication; test-side duplication (TEST-7) is homed here.
- **DB (30)** owned the schema consequences of the DateTimeOffset string storage (TEST-5, now fixed alongside DB-2 — the converter is SQLite-only and SQL Server uses native `datetimeoffset`); migrations previously ran under no test (TEST-1, now fixed — the chain is verified against LocalDB by `MigrationChainTests`).
- **SEC (25)** owns security-scenario coverage (auth matrix depth, token expiry/rotation tests) beyond the structural gaps noted here.
- **BUILD (40)** owns the CI workflow that would host coverage collection (TEST-9), e2e jobs (TEST-2), and any format/generation gates.
- **REL (26)** owns the runtime failure modes (silent empty renders, hub reconnect behavior) that the missing e2e layer would otherwise catch.
