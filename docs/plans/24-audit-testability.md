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
| High | 0 |
| Medium | 0 |
| Low | 4 |
| Info | 1 |

For a solo certificate project, the automated test estate is genuinely strong: ~700 test cases across four layers, real-pipeline integration tests through `WebApplicationFactory<Program>` that exercise the same loopback API the app itself uses, a purpose-built pure state machine (`CallRegistry`) that makes the hardest feature unit-testable, a hand-rolled SignalR hub harness, consumer tests against a stubbed handler, an auth-matrix suite, and CI running everything on every push. Relational behavior is no longer entirely simulated — a SQLite-backed factory variant now exists for constraint-sensitive suites and the migration chain is verified against LocalDB (TEST-1, fixed) — though most integration classes still run on InMemory by default. The browser side is no longer a total blind spot: a Playwright smoke suite covers login, data rendering, the culture switch, and a real two-user call (TEST-2, fixed). Both Medium findings are now fixed: the 26-class "web" collection was split into isolated per-class fixtures (TEST-3), and bUnit now covers `LessonViewer`'s completion flow and `Chat`'s SignalR event handling (TEST-4) — the two components the finding's own recommendation prioritized. Three more logic-heavy components (`AdminCourseEdit`, `AdminAssessmentEdit`, `Home.razor.cs`'s hydration logic) remain untested at the component level; the bUnit plumbing now exists, so covering them is a smaller lift than before.

## 3. Findings

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
| TEST-6 | Low→quick win | S | Unit tests for SsrApiHandler, EnvFileLoader, FileLoggerProvider |
| TEST-7 | Low | S | Deduplicate CustomWebAppFactory provisioning helpers |
| TEST-8 | Low | S | Replace static-ctor env vars with per-host configuration |
| TEST-9 | Low | S | Add coverage collection to CI |
| TEST-10 | Info | S | Architecture/DI fitness tests |
| — | (opportunistic) | S | bUnit for the remaining 3 logic-heavy components (AdminCourseEdit, AdminAssessmentEdit, Home.razor.cs hydration) — the harness now exists (LessonViewerTests.cs, ChatComponentTests.cs) |

## 5. Related Findings Elsewhere

- **ARCH (21)** owns the structural decisions this suite tests around: the loopback self-API (whose silent-`default` consumers make failures invisible to e2e-less testing), the convention-only pages→consumers boundary (TEST-10 supplies the enforcement) — chat's hub-inline writes were fixed by ARCH-4, unblocking Application-level unit tests for that logic.
- **MAINT (23)** owns the comment-enforced DI lifetime invariants (MAINT-10) that TEST-10 would pin, and the change-ripple that makes forgotten test updates likely.
- **CQ (22)** owns production-code duplication; test-side duplication (TEST-7) is homed here.
- **DB (30)** owned the schema consequences of the DateTimeOffset string storage (TEST-5, now fixed alongside DB-2 — the converter is SQLite-only and SQL Server uses native `datetimeoffset`); migrations previously ran under no test (TEST-1, now fixed — the chain is verified against LocalDB by `MigrationChainTests`).
- **SEC (25)** owns security-scenario coverage (auth matrix depth, token expiry/rotation tests) beyond the structural gaps noted here.
- **BUILD (40)** owns the CI workflow that would host coverage collection (TEST-9), e2e jobs (TEST-2), and any format/generation gates.
- **REL (26)** owns the runtime failure modes (silent empty renders, hub reconnect behavior) that the missing e2e layer would otherwise catch.
