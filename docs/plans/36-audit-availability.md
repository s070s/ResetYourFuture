# Audit: Availability

| | |
|---|---|
| Finding prefix | AVAIL |
| Created | 2026-07-11 |
| Scope | Failure modes, uptime, and recovery: startup migrate+seed crash risk, absence of health endpoints, LocalDB/single-SQL-Server as a single point of failure, background-service failure behaviour and its blast radius, graceful shutdown of circuits/calls, retry/timeout policy (or its absence) on the loopback HttpClients, and the availability consequence of single-instance pinning. |
| Delegated | Code-path bugs — unhandled exceptions, swallowed errors in business logic — → REL (26). Multi-instance/growth blockers as capacity concerns → SCALE (35) (referenced here for their uptime/redundancy consequence only). Per-request cost optimization → PERF (34). Observability/instrumentation to detect these failure modes → OBS (38). Infra/process-supervisor/deployment topology (restart policies, orchestrator config) → CLOUD (41). |

## 1. Methodology

Traced the startup sequence in `Program.cs` end to end (`EnvFileLoader` → `AddResetYourFutureAuthentication`/`AddResetYourFutureServices` → `PrewarmAndSeedDatabaseAsync` → middleware pipeline → `app.Run()`), and `Startup/DatabaseSeedingExtensions.cs` for the migrate/seed path's exception handling. Read `Startup/AuthenticationSetupExtensions.cs:26-31` for the EF `EnableRetryOnFailure` configuration. Grepped the whole `src/` tree for `AddHealthChecks`/`MapHealthChecks`, `CircuitOptions`/`HubOptions`, `ApplicationStopping`/`IHostApplicationLifetime`, and `Timeout` inside `ServiceRegistrationExtensions.cs` — all absent. Read all three `BackgroundService` implementations in full (`Web/Services/AssistantIndexer.cs`, `Web/Services/CallRingMonitor.cs`, `Infrastructure/Seeding/BulkStudentSeedingService.cs`) for per-iteration vs. startup-path exception guarding. Read `Consumers/ApiClientBase.cs` and `Web/Services/ChatService.cs`/`CallService.cs` to compare exception handling between the 15 `ApiClientBase`-derived consumers and the two hand-written hub-backed services. Cross-checked `Directory.Packages.props` for a resilience library (Polly) — none present. Re-read SCALE-1/2/4 (`35-audit-scalability.md`) to frame the redundancy consequence of single-instance pinning without duplicating their content.

NOT examined: process-supervisor/container restart-policy configuration (no Dockerfile/systemd unit in the repo to inspect) → CLOUD (41); actual failover testing (static analysis only, app not launched).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 4 |
| Low | 2 |
| Info | 1 |

> **Accepted since audit (out of scope — will not implement):** AVAIL-4 (single-instance pinning means any crash or restart is a full outage — there is no failover). This is purely the availability consequence of the accepted scalability limitations (SCALE-1/2/3): with no shared call/presence store and no SignalR backplane, a second instance cannot run correctly, so there is no node to fail over to. Since running more than one instance is consciously out of scope for this single-instance university project, AVAIL-4 has no separate fix and is accepted along with the SCALE findings it depends on. Blast-radius mitigations that don't need a second instance remain covered by AVAIL-1 (health checks, fixed), AVAIL-2 (startup retry, fixed) and infrastructure-level restart supervision (CLOUD 41). See [20-audit-gaps.md](20-audit-gaps.md) §4.

> **Fixed since audit:** AVAIL-1 (High — no health/readiness endpoints) — `/health/live` (no dependency checks) and `/health/ready` (database + assistant, tagged `"ready"`) are now mapped via `DatabaseHealthCheck`/`AssistantHealthCheck`; the assistant check reports Degraded (200) rather than Unhealthy (503) when Ollama is unreachable, since the rest of the app serves fine without it. AVAIL-2 (High — unguarded startup migrate+seed) — `PrewarmAndSeedDatabaseWithRetryAsync` wraps the existing seed path in a bounded exponential-backoff retry (5 attempts) with a critical log on final failure. AVAIL-3 (High — loopback consumers had no timeout/retry, so a transient blip crashed the circuit) — every consumer `HttpClient` now has a 30s request timeout (the streaming assistant client excepted), and `ApiClientBase` routes all calls through a resilience helper that catches connection failures/timeouts and degrades to the consumer's empty state instead of propagating into the Razor component; idempotent GETs get a couple of fast jittered retries, non-idempotent verbs and timeouts are never retried. Verified by `ApiClientResilienceTests`. (Per-failure logging was deferred — it would mean threading a logger through all 26 consumers — and the degradation is the same empty state consumers already render for non-success responses.)

The background-service layer shows real availability awareness in places — `CallRingMonitor` sweeps dangling `CallSession` rows left by a crashed prior process (`CallRingMonitor.cs:133-154`), every hub connection (`ChatService`, `CallService`) auto-reconnects and rejoins in-flight calls (`CallService.cs:89-96`), and EF's `EnableRetryOnFailure` absorbs transient SQL blips once the app is running. Startup migration/seeding now retries with backoff instead of taking the process down on the first failure, `/health/live`/`/health/ready` give an orchestrator something to poll, and the loopback HTTP layer now times out and degrades gracefully instead of crashing the circuit (AVAIL-3, fixed). Because SCALE-1/2/4 pin the app to one instance, every remaining gap is also a full-outage risk rather than a degraded one — there is no second node to fail over to (AVAIL-4).

## 3. Findings

### AVAIL-5: No graceful shutdown — active calls, chat connections, and in-flight circuits are hard-dropped, not drained  [Medium] [Effort: M]
- **Evidence:** No use of `IHostApplicationLifetime`/`ApplicationStopping` anywhere in `src/` (confirmed by search); no `CircuitOptions` tuning; `Program.cs` relies entirely on ASP.NET Core defaults for shutdown. `CallHub`'s only disconnect-aware cleanup is `OnDisconnectedAsync`, which fires per-connection when SignalR notices the socket drop (`CallHub.cs:75-105`) — there is no explicit "server is stopping, tell every active call to end and every circuit to save state" step.
- **Impact:** A deploy or restart drops every open SignalR connection (chat and call) simultaneously; clients discover this only via their own reconnect/timeout logic (`ChatService`'s `WithAutomaticReconnect()`, `CallService.cs:81,89-96`) rather than a clean server-initiated notice. Active video calls end abruptly for both participants with no "call ended: server restarting" message — the client-side reconnect will re-establish the hub connection but the call state itself (mesh peer connections) is not designed to survive a server bounce. `CallRingMonitor.SweepDanglingSessionsAsync` (`CallRingMonitor.cs:133-154`) is a good compensating control on the *next* boot (marks orphaned `CallSession` rows `Cancelled`), but nothing runs on the way down.
- **Recommendation:** Register an `IHostApplicationLifetime.ApplicationStopping` callback that broadcasts a `ServerShuttingDown` hub event (both hubs) so connected clients can show a clear message instead of a silent drop, and gives in-flight requests/circuits a short grace window before the process exits (`WebApplication` already supports `Host.CreateDefaultBuilder`'s default shutdown timeout — tune it explicitly rather than relying on the default).

### AVAIL-6: Unhandled `BackgroundService` exceptions stop the entire host, not just the failing feature  [Medium] [Effort: S]
- **Evidence:** REL-6 (`26-audit-reliability.md`) identifies that `BulkStudentSeedingService.ExecuteAsync` (`Infrastructure/Seeding/BulkStudentSeedingService.cs:30-51`) has no try/catch around `BulkStudentSeeder.SeedAsync`, unlike `AssistantIndexer` and `CallRingMonitor`'s poll loop, which guard each iteration. The availability angle REL-6 doesn't spell out: .NET's `HostOptions.BackgroundServiceExceptionBehavior` defaults to `StopHost` — an unhandled exception from *any* registered `BackgroundService` (there are three: `CallRingMonitor`, `BulkStudentSeedingService`, `AssistantIndexer`) stops the entire generic host, which also owns the Kestrel web server. It is not feature-isolated.
- **Impact:** A single seeding bug in a Development-only, opt-in bulk-seed path (`SeedData:Enabled`) has the same blast radius as a web-server crash: the whole app stops serving HTTP traffic, not just "seeding failed." Because this only runs when `SeedData:Enabled=true` in Development, production is unaffected today, but the pattern — three unrelated background jobs sharing fate with the web server — is a latent trap for any future hosted service added without the same discipline `AssistantIndexer`/`CallRingMonitor`'s loops show.
- **Recommendation:** Fix REL-6's specific gap (wrap `BulkStudentSeeder.SeedAsync` in try/catch-log). As a systemic guard, consider setting `HostOptions.BackgroundServiceExceptionBehavior = Ignore` for genuinely non-critical hosted services (or keep `StopHost` but ensure every `ExecuteAsync` is fully self-guarded, as two of the three already are) so a future hosted-service bug can't silently take the whole app down.

### AVAIL-7: `CallRingMonitor`'s startup dangling-session sweep is the one unguarded step in an otherwise well-guarded service  [Low] [Effort: S]
- **Evidence:** `CallRingMonitor.ExecuteAsync` (`CallRingMonitor.cs:48-72`) calls `await SweepDanglingSessionsAsync(stoppingToken)` at line 50 with no try/catch, then enters the poll loop where every iteration *is* guarded (`:54-61`). `SweepDanglingSessionsAsync` (`:133-154`) issues a DB query and a `SaveChangesAsync`.
- **Impact:** If the database is transiently unreachable at the exact moment hosted services start (a narrow window, since `PrewarmAndSeedDatabaseAsync` already proved connectivity moments earlier in `Program.cs:19`), this call throws and — per AVAIL-6 — stops the whole host. Narrow window, but it is the one gap in a service whose author clearly intended full resilience (the surrounding loop is carefully guarded).
- **Recommendation:** Wrap the sweep call in the same try/catch-log pattern used by `PollOnceAsync`, treating a failed sweep as "retry next cycle" rather than fatal.

### AVAIL-8: LocalDB / a single SQL Server instance is the platform's one true single point of failure, with no documented production guard  [Low] [Effort: S]
- **Evidence:** Development connects to `(localdb)\MSSQLLocalDB` (`appsettings.Development.json:9`), a single-process, Windows-only, non-clustered SQL Server edition with no HA story by design. Production's `ConnectionStrings:DefaultConnection` ships empty (`appsettings.json:18`), relying entirely on environment/user-secret override with no startup validation that the resolved connection string isn't accidentally still LocalDB. No `appsettings.Production.json` exists to document the expected production shape.
- **Impact:** Entirely expected for a university certificate project (a single managed SQL Server instance is a normal, acceptable production posture at this scale) — flagged as Low/Info rather than higher because a single DB is not itself a defect. The gap worth noting is process, not architecture: nothing fails fast or warns if a real deployment is accidentally pointed at LocalDB or an empty connection string beyond the eventual `MigrateAsync` failure covered by AVAIL-2.
- **Recommendation:** No architectural change needed. Optionally add a startup assertion that rejects an empty/LocalDB connection string outside Development, so a misconfiguration is a clear fail-fast message rather than a generic migration exception.

### AVAIL-9: Reconnect and dangling-session recovery are genuine strengths worth preserving  [Info]
- **Evidence:** `ChatService.StartAsync`/`CallService.EnsureConnectedAsync` both use `.WithAutomaticReconnect()` and re-run `RejoinCall` on reconnect (`CallService.cs:89-96`); `CallRingMonitor.SweepDanglingSessionsAsync` repairs `CallSession` rows orphaned by a prior crashed process on every boot (`CallRingMonitor.cs:133-154`); EF's `EnableRetryOnFailure` (`AuthenticationSetupExtensions.cs:28-31`) absorbs transient SQL Server blips once the app is running (as opposed to at boot, per AVAIL-2).
- **Impact:** None — these are the parts of the recovery story already done well and should be the template extended to AVAIL-5/7's gaps.
- **Recommendation:** None; preserve.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| AVAIL-6 | Medium | S | Guard `BulkStudentSeedingService`'s body (closes REL-6); reconsider `BackgroundServiceExceptionBehavior` |
| AVAIL-7 | Low | S | Guard `CallRingMonitor`'s startup dangling-session sweep |
| AVAIL-8 | Low | S | Fail-fast on empty/LocalDB connection string outside Development |
| AVAIL-5 | Medium | M | Broadcast a shutdown notice + drain window before process exit |

## 5. Related Findings Elsewhere

- **SCALE (35):** SCALE-1 (CallRegistry singleton), SCALE-2 (no SignalR backplane), and SCALE-4 (DPAPI-only DataProtection key protection) are the root causes AVAIL-4 names as the reason no failover is possible; SCALE-3's loopback-connection multiplier compounds AVAIL-3's blast radius during restarts.
- **REL (26):** REL-6 identifies the specific unguarded `BulkStudentSeedingService` code path that AVAIL-6 gives the host-wide-outage framing for; REL-3 (consumer swallowing of non-success status codes) is the sibling gap to AVAIL-3 (consumer swallowing of network-level exceptions) in the same `ApiClientBase` class; REL-7 (per-request DB lookup in auth validation) is a related DB-availability coupling on the request hot path rather than at boot.
- **DB (30):** DB-13 notes the startup auto-migrate+seed and design-time hardcoded fallback connection string as Info; AVAIL-2 gives that same startup path its failure-mode treatment.
- **PERF (34):** PERF-1 quantifies the per-call cost of the same loopback consumers AVAIL-3 flags for missing resilience — one report owns cost, the other owns failure behaviour.
- **OBS (38):** Owns the forward-looking instrumentation (structured health metrics, alerting) beyond the minimal liveness/readiness pair AVAIL-1 asks for.
- **CLOUD (41):** Owns process-supervisor/restart-policy configuration and any real multi-instance deployment topology that would consume AVAIL-4's fix.
