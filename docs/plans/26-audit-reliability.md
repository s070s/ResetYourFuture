# Audit: Reliability

| | |
|---|---|
| Finding prefix | REL |
| Created | 2026-07-11 |
| Scope | Failure modes in code paths: unhandled exceptions, swallowed errors, missing timeouts/retries, race conditions, startup-seeding failure handling, and background-service crash behaviour. |
| Delegated | Technical vulnerabilities → SEC (25). Domain-rule correctness (subscription expiry, payment activation) → BIZ (27). Data-integrity constraints & orphan risk → DQ (28). Availability/uptime/recovery posture → AVAIL (36). Observability/health checks → OBS (38). Custom logger quality → LOG (37). |

## 1. Methodology

Traced exception and error-handling behaviour through the request pipeline (`Program.cs` exception handler + status-code pages), all API controllers, the application services (`AuthApiService`, `AdminUserService`, `SubscriptionService`, `CourseService`, `CertificateService`, `ChatQueryService`, `AssessmentService`, `ProfileService`), the SSR consumer layer (`Consumers/ApiClientBase.cs`), both SignalR hubs (`ChatHub`, `CallHub`, `CallHub.Signaling`), the three hosted `BackgroundService`s (`CallRingMonitor`, `BulkStudentSeedingService`, `AssistantIndexer`), and the startup migrate-and-seed path (`Startup/DatabaseSeedingExtensions.cs`). Cross-checked EF `OnDelete` behaviours in `Data/Configurations/*` to find delete paths that will throw. Read the email transport wiring and the assistant streaming path for swallow/timeout handling.

NOT examined: SQL Server failover behaviour (retry-on-failure is configured, `AuthenticationSetupExtensions.cs:28-31`) and reverse-proxy resilience → AVAIL (36).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 5 |
| Info | 0 |

Overall reliability is above average for the project's stage. The background services are exemplary: every poll/index pass is wrapped in try/catch, cancellation is handled gracefully, `CallRingMonitor` even sweeps dangling call sessions left by a crashed process at startup, and the assistant path degrades to an "unavailable" event rather than throwing. The production pipeline funnels unhandled exceptions into RFC 7807 problem+json. All four Medium findings are now fixed or reassessed: registration email failure no longer 500s after account creation (REL-2), admin-seed and certificate-generation failures are now surfaced instead of swallowed (REL-4, REL-5), and REL-3's operator-visibility gap turned out to already be closed by the framework's default HTTP logging handlers (see below) — what remains of it is a larger, disproportionate UI-contract change, so it is now tracked as Low.

## 3. Findings

### REL-3: SSR loopback consumers swallow non-success responses into blank UI  [Low] [Effort: M]
- **Evidence:** `Web/Consumers/ApiClientBase.cs:41-80` — `GetAsync`/`PostAsync`/`PostJsonAsync` return `default`/`null` on any non-2xx response, with no *application-level* logging or error propagation. **Reassessed (2026-07-14):** the "nothing logged" half of the original evidence is stale. `AddHttpClient<TInterface, TImpl>` (`ServiceRegistrationExtensions.cs`) wires .NET's default `HttpClientFactory` logging handlers on every consumer, and those log every request's status code — including non-success ones — at Information level with no code required: verified live via `src/ResetYourFuture.Web/Logs/log-*.txt`, e.g. `[INFORMATION] [System.Net.Http.HttpClient.ITestimonialConsumer.LogicalHandler] End processing HTTP request after 10ms - 200`. Neither `appsettings.json`'s `Logging:LogLevel` section nor `Logging/FileLogger.cs` filters that category out, so failure status codes reach the log file today. Operator visibility (the finding's original headline risk) is therefore already solved by the framework, not missing.
- **Impact:** What remains is narrower than originally scoped: pages still can't distinguish "genuinely empty result" from "call failed" (both come back as `default`/`null`/`false` from `ExecuteAsync`), so a failed call still renders as an empty state to the *user* rather than an error state. Fixing that requires changing `ExecuteAsync`'s return contract (e.g. a `Result<T>` wrapper) across all ~25 typed consumers in `Web/Consumers/` and every calling Razor page that consumes them — disproportionate for what's left once the operator-visibility half is accounted for.
- **Recommendation:** No further action for the operator-visibility angle (already covered). If the user-facing distinction is wanted later, land it as its own scoped effort — start with the highest-traffic pages rather than a blanket contract change across all consumers. Longer term, calling the application services in-process (rather than over HTTP-to-self) removes the entire failure class (see ARCH/MAINT).

### REL-6: Hosted-service exceptions outside the poll/index try-block can fault the host  [Low] [Effort: S]
- **Evidence:** `Infrastructure/Seeding/BulkStudentSeedingService.cs:30-51` runs `BulkStudentSeeder.SeedAsync` with no surrounding try/catch (only the missing-password guard). `AssistantIndexer` (`Web/Services/AssistantIndexer.cs`) and `CallRingMonitor` correctly guard each iteration, but a throw during `BulkStudentSeeder.SeedAsync` propagates out of `ExecuteAsync`.
- **Impact:** An unhandled `BackgroundService` exception can fault the host (default .NET behaviour is `StopHost`). Development-only (guarded by `IsDevelopment()` + `SeedData:Enabled`), so production is unaffected, but a seeding error can take down the dev app.
- **Recommendation:** Wrap the seed call in try/catch-log, matching the convention already used by the sibling background services.

### REL-7: Security-stamp revalidation performs a DB lookup on every authenticated request  [Low] [Effort: M]
- **Evidence:** `Startup/AuthenticationSetupExtensions.cs:107-127` (`OnValidatePrincipal`) and `:159-180` (`OnTokenValidated`) both call `userManager.FindByIdAsync` on every request/token validation.
- **Impact:** Correct for security, but it couples every authenticated request to a live DB read. Under a database outage or slowdown, all authenticated traffic fails or stalls rather than degrading gracefully, and it adds a query to the hot path.
- **Recommendation:** Cache the (userId → securityStamp, IsEnabled) tuple briefly (short TTL memory cache with invalidation on stamp change) to bound DB dependency, or adopt Identity's built-in `SecurityStampValidator` interval semantics.

### REL-8: Refresh-token rotation has no uniqueness/transaction guard against a double-spend race  [Low] [Effort: M]
- **Evidence:** `Application/ApiServices/AuthApiService.cs:178-216` reads the stored token, revokes it, and inserts a replacement in one `SaveChangesAsync`, but `RefreshToken.TokenHash` has only a non-unique index (`RefreshTokenConfiguration.cs:21`) and there is no serializable transaction. Two concurrent refreshes presenting the same token can both pass the `RevokedAt is null` check.
- **Impact:** A double-submit (or a racing attacker) can mint two valid token pairs from one refresh token. Low likelihood and low blast radius, but it undermines strict one-time rotation.
- **Recommendation:** Make `TokenHash` unique and perform the revoke+insert under a transaction (or `ExecuteUpdate` with a `WHERE RevokedAt IS NULL` guard and check the affected-row count) so only one rotation can win. Pairs naturally with reuse detection (SEC-1).

### REL-9: Sitemap endpoint throws (500) when `Sitemap:BaseUrl` is unconfigured  [Low] [Effort: S]
- **Evidence:** `Startup/InfrastructureEndpointsExtensions.cs:221-222` throws `InvalidOperationException` when `Sitemap:BaseUrl` is missing; the value is populated in `appsettings.json` but any environment that omits it returns 500 to crawlers.
- **Impact:** Minor — a crawler-facing 500 rather than a graceful skip. Cached 30 min so low volume.
- **Recommendation:** Fall back to the request's host or return `404`/empty sitemap instead of throwing.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| REL-3 | Low | M | (Optional) distinguish empty vs failed results in the UI — operator logging already covered by the framework |
| REL-6 | Low | S | Wrap `BulkStudentSeedingService` work in try/catch-log |
| REL-7 | Low | M | Cache security-stamp/IsEnabled to bound per-request DB dependency |
| REL-8 | Low | M | Make refresh rotation atomic + unique to prevent double-spend |
| REL-9 | Low | S | Graceful fallback for missing `Sitemap:BaseUrl` |

## 5. Related Findings Elsewhere

- **SEC (25):** Refresh-token lifecycle (SEC-1) — REL-8 is the concurrency angle of the same token flow; SEC owns the security/reuse-detection angle.
- **COMP (29):** GDPR erasure is unblocked now that user deletion works (former REL-1, fixed); COMP owns the remaining regulatory obligations.
- **BIZ (27):** Payment webhook/activation gaps determine whether the mock-vs-real payment failure modes matter.
- **OBS (38) / LOG (37):** REL-3's remaining user-facing gap (empty vs failed result) still argues for structured error-state rendering; the operator-side logging is already handled by the framework's default `HttpClientFactory` handlers.
