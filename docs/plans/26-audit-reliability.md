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
| Medium | 4 |
| Low | 4 |
| Info | 0 |

Overall reliability is above average for the project's stage. The background services are exemplary: every poll/index pass is wrapped in try/catch, cancellation is handled gracefully, `CallRingMonitor` even sweeps dangling call sessions left by a crashed process at startup, and the assistant path degrades to an "unavailable" event rather than throwing. The production pipeline funnels unhandled exceptions into RFC 7807 problem+json. The main weaknesses are a registration flow that can 500 after the account is already created, and an SSR consumer layer that swallows failures into blank UI.

## 3. Findings

### REL-2: Registration can 500 after the account is already created when email delivery fails  [Medium] [Effort: S]
- **Evidence:** `Application/ApiServices/AuthApiService.cs:56-92` — the user is created (`CreateAsync`), assigned the Student role, given a Free plan, and only then `await emailService.SendEmailConfirmationAsync(...)` is called with no try/catch (`:86`). `SmtpEmailService` (MailKit) is the transport whenever `Email:Smtp:Host` is set.
- **Impact:** If the SMTP relay is unreachable or rejects the message, the awaited send throws, the controller returns 500, but the `ApplicationUser` row, role, and Free subscription have already been committed. The user sees a failure yet cannot re-register (duplicate email) and never receives a confirmation link — a stuck, unconfirmed, un-loginable account.
- **Recommendation:** Move email delivery outside the account-creation success path (fire-and-forget with logging, or an outbox/retry queue), and return success once the account exists. Surface a "resend confirmation" affordance rather than failing registration on transient SMTP errors.

### REL-3: SSR loopback consumers swallow non-success responses into blank UI  [Medium] [Effort: M]
- **Evidence:** `Web/Consumers/ApiClientBase.cs:41-80` — `GetAsync`/`PostAsync`/`PostJsonAsync` return `default`/`null` on any non-2xx response with no logging or error propagation. The class comment itself documents that unauthenticated/failed calls "silently returned default — appearing to the user as blank/empty pages." The self-base-URL + loopback-TLS caveat is documented in `ServiceRegistrationExtensions.cs:204-210`.
- **Impact:** Any API error (401, 403, 500, TLS handshake failure against the loopback cert in a misconfigured deployment) renders as an empty page or silently no-op action, with nothing logged at the consumer layer. Failures are invisible to both user and operator.
- **Recommendation:** Log non-success responses (status + URL) in `ApiClientBase`, and distinguish "empty result" from "call failed" so pages can render an error state. Longer term, calling the application services in-process (rather than over HTTP-to-self) removes the entire failure class (see ARCH/MAINT).

### REL-4: Admin-user seeding failure at startup is silently ignored  [Medium] [Effort: S]
- **Evidence:** `Startup/DatabaseSeedingExtensions.cs:87-92` — `CreateAsync(admin, adminPassword)` result is only acted on inside `if (result.Succeeded)`; the failure branch neither logs nor throws.
- **Impact:** If the admin password fails Identity's policy (or any other create error occurs), the app starts with **no admin account** and no signal that seeding failed. The platform is left with no administrative access and the operator has no indication why.
- **Recommendation:** On `!result.Succeeded`, log the errors at Error level and throw (fail-fast, consistent with the existing "AdminUser:Password is required" throw at `:68-70`).

### REL-5: Certificate auto-generation failure is swallowed on lesson completion  [Medium] [Effort: S]
- **Evidence:** `Application/ApiServices/CourseService.cs:344-360` — when a course is completed and the user has certificate access, `certificateService.GetOrGenerateAsync` is wrapped in try/catch that only logs; the method then returns `courseCompleted: true`.
- **Impact:** The student is told the course/lesson is complete and a certificate should exist, but PDF generation (QuestPDF) or storage failures leave no certificate and no user-visible error. The `IssueCertificate` endpoint would later re-attempt, so it is recoverable, but the silent gap between "completed" and "no certificate" is confusing and unmonitored.
- **Recommendation:** Keep the completion resilient, but record the failure in a way an operator/user can act on (a retry flag, or surfacing "certificate pending" state), and ensure the log is at Error with course/user context (it currently is).

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
| REL-2 | Medium | S | Decouple confirmation email from account creation; don't fail registration on SMTP error |
| REL-3 | Medium | M | Log non-success responses in `ApiClientBase`; distinguish empty vs failed |
| REL-4 | Medium | S | Fail-fast (log + throw) when admin seeding fails |
| REL-5 | Medium | S | Surface/track certificate auto-generation failures on completion |
| REL-6 | Low | S | Wrap `BulkStudentSeedingService` work in try/catch-log |
| REL-7 | Low | M | Cache security-stamp/IsEnabled to bound per-request DB dependency |
| REL-8 | Low | M | Make refresh rotation atomic + unique to prevent double-spend |
| REL-9 | Low | S | Graceful fallback for missing `Sitemap:BaseUrl` |

## 5. Related Findings Elsewhere

- **SEC (25):** Refresh-token lifecycle (SEC-1) — REL-8 is the concurrency angle of the same token flow; SEC owns the security/reuse-detection angle.
- **COMP (29):** GDPR erasure is unblocked now that user deletion works (former REL-1, fixed); COMP owns the remaining regulatory obligations.
- **BIZ (27):** Payment webhook/activation gaps determine whether the mock-vs-real payment failure modes matter.
- **OBS (38) / LOG (37):** Silent swallows (REL-3, REL-5) argue for structured error logging + alerting.
