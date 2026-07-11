# Audit: Observability

| | |
|---|---|
| Finding prefix | OBS |
| Created | 2026-07-11 |
| Scope | Forward-looking instrumentation: health checks, metrics, distributed tracing / OpenTelemetry, correlation IDs, structured log ingestion, dashboards — what is missing and a minimal adoption path |
| Delegated | Current file-logger design/coverage/retention → 37 (LOG). Uptime consequence of undetected downtime → 36 (AVAIL). Incident-response procedure → 42 (OPS). Cloud hosting where a metrics backend would live → 41 (CLOUD). |

## 1. Methodology

Repo-wide searches for `AddHealthChecks`, `MapHealthChecks`, `OpenTelemetry`, `ActivitySource`, `Meter(`, `AddMetrics` (zero hits in `src/`); read `src/ResetYourFuture.Web/Program.cs`, `Startup/ServiceRegistrationExtensions.cs` (ProblemDetails/traceId at lines 136-140), `Logging/FileLogger.cs`, `Directory.Packages.props` (no OTel/health-check packages), `src/ResetYourFuture.Application/ApiServices/AssistantService.cs` (existing ad-hoc Ollama status ping), `.github/workflows/tests.yml`. NOT examined: runtime behavior (no build/run per repo constraints); dashboards/hosting tooling (none exist to examine).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 3 |
| Low | 3 |
| Info | 1 |

Overall: there is no instrumentation layer at all — no health endpoints, no metrics, no tracing, no correlation between the traceId given to users and anything on disk. That is unremarkable for a university certificate project and nothing here is broken *today*; the findings are graded as the debt you would pay down first if the app ever ran unattended. The good news is the codebase is unusually well-positioned for cheap adoption: logging already uses structured message templates throughout, dependencies are few and well-bounded (SQL Server, Ollama, SMTP), an Ollama liveness probe already exists in `AssistantService.GetStatusAsync`, and ASP.NET Core 10's built-in OTel/`Microsoft.Extensions.Diagnostics` support means the minimal path below is mostly configuration, not code.

## 3. Findings

### OBS-1: No health-check endpoints  [Medium] [Effort: S]
- **Evidence:** No `AddHealthChecks`/`MapHealthChecks` anywhere in `src/` (verified by search); `src/ResetYourFuture.Web/Program.cs` maps controllers, hubs, and infrastructure endpoints only. Dependencies that can fail independently: SQL Server (`Startup/AuthenticationSetupExtensions.cs:26-31`), Ollama sidecar (`Assistant:BaseUrl`), SMTP relay (`Infrastructure/ApiServices/SmtpEmailService.cs`).
- **Impact:** Nothing — human or machine — can ask the app "are you OK?". Any future hosting (IIS app-init, systemd watchdog, container orchestrator, uptime monitor) has no probe target; downtime detection is entirely reactive (a user notices). Report 36 (AVAIL) owns that consequence; this finding owns the missing instrument.
- **Recommendation:** `builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>()` plus a tiny `IHealthCheck` that reuses the existing Ollama ping logic from `AssistantService.GetStatusAsync` (registered only when `Assistant:Enabled`); map `/healthz` (liveness, no checks) and `/readyz` (all checks), `.AllowAnonymous()` with no body detail in non-Development.

### OBS-2: No metrics or tracing (no OpenTelemetry)  [Medium] [Effort: M]
- **Evidence:** `Directory.Packages.props` contains no `OpenTelemetry.*` or `Microsoft.Extensions.Diagnostics.*` packages; no `ActivitySource`/`Meter` usage in `src/`. Interesting quantities are currently unmeasurable: request duration/error rate, SignalR connection counts (chat + call hubs), assistant latency and retrieval quality, seeding duration, rate-limiter rejections (429s configured at `Startup/ServiceRegistrationExtensions.cs:145-167`).
- **Impact:** Performance and reliability questions (reports 34/36) can only be answered by adding instrumentation *after* a problem appears — the worst time. No baseline exists to compare against when something regresses.
- **Recommendation:** Minimal adoption path, in order: (1) add `OpenTelemetry.Extensions.Hosting` + `OpenTelemetry.Instrumentation.AspNetCore` + `OpenTelemetry.Exporter.OpenTelemetryProtocol`, wired with `builder.Services.AddOpenTelemetry().WithMetrics(...).WithTracing(...)` — ASP.NET Core 10 emits `http.server.request.duration`, Kestrel and SignalR meters natively; (2) point OTLP at a local docker `grafana/otel-lgtm` all-in-one during development; (3) only then add custom meters (assistant tokens/latency, call setup outcomes). Keep it out of test hosts the same way Ollama is (registration gated on config).

### OBS-3: traceId is handed to users but cannot be found in any log  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Startup/ServiceRegistrationExtensions.cs:136-140` adds `traceId` (`HttpContext.TraceIdentifier`) to every ProblemDetails error response. The only persistent sink, the file logger, discards scopes (`Logging/FileLogger.cs:16`, `BeginScope => null`) and its line format (`FileLogger.cs:30-35`) has no field for it — so the identifier a user could report has no counterpart anywhere on the server.
- **Impact:** The correlation loop is half-built: "give us the traceId from the error page" is the natural support flow the ProblemDetails setup implies, but the operator can do nothing with it. Exception details for 500s are additionally *not* logged with the response (the production handler in `Program.cs:42-59` writes a generic body; the exception itself is only logged by the framework's default providers, console-only).
- **Recommendation:** Include the current `Activity.Current?.Id ?? TraceIdentifier` in the file-log entry format (pairs with LOG-8 scope support in report 37), and add one `logger.LogError(exception, "Unhandled exception {TraceId}", traceId)` in the production exception handler so response and log line share the key.

### OBS-4: Logs are unstructured flat text — nothing can ingest them  [Low] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/Logging/FileLogger.cs:30-35` renders `[timestamp] [LEVEL] [category] message` plain text; message-template arguments are flattened away. Call sites are already structured (`{Email}`, `{UserId}`, `{Role}` templates everywhere, e.g. `AuthApiService.cs`, `AdminUserService.cs`).
- **Impact:** Grep is the only query tool; any future log aggregation (Loki, Seq, CloudWatch) requires reparsing free text. The structured information present at every call site is being thrown away at the sink.
- **Recommendation:** Either emit JSON lines from the custom provider (serialize timestamp/level/category/message/traceId), or — less code to own — replace the provider with `Serilog.Sinks.File` using the compact JSON formatter, which preserves the existing template properties for free. Keep the current provider if the learning exercise is the point; this is a when-needed change.

### OBS-5: Real-time call/hub layer has no instrumentation  [Low] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/Hubs/CallHub.cs` / `CallHub.Signaling.cs` / `Services/CallRingMonitor.cs` log a handful of lifecycle events, but call setup success/failure, ICE negotiation failures, and audio-only fallbacks (documented behaviors in README "Video Calls") are observable only in the *browser* console — the server never learns whether a call actually connected.
- **Impact:** The feature with the most environment-dependent failure modes (NAT, permissions, devices) is the least observable one; a demo failure is undiagnosable server-side.
- **Recommendation:** Emit one server-side event per call outcome (connected / ring-timeout / media-failure, reported by the client over the existing hub) — a counter via `Meter` once OBS-2 lands, or structured log lines until then. The e2e technique already proves connection client-side; mirror that signal to the server.

### OBS-6: Assistant pipeline quality is untracked  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Application/ApiServices/AssistantService.cs` logs only failures (lines 43, 66, 108); nothing records retrieval scores, chunk counts, latency, or token usage. Tuning knobs exist (`Assistant` section in `appsettings.json`; hardcoded `MinScore` at `AssistantRetrievalService.cs:18` — CFG-6 in report 39) but there is no data to tune them with.
- **Impact:** "Is 0.4 the right MinScore?" / "is 6 chunks enough?" are unanswerable; grounding quality regressions after reindexing are invisible.
- **Recommendation:** One Information log per answered question with `{TopScore}`, `{ChunkCount}`, `{DurationMs}`, `{Grounded}` — cheap now, and the natural first custom meter after OBS-2.

### OBS-7: Built-in runtime counters exist but are undocumented as the current fallback  [Info] [Effort: S]
- **Evidence:** .NET 10 exposes EventCounters/System.Runtime metrics consumable via `dotnet-counters` with zero code; `.config/dotnet-tools.json` pins only `dotnet-ef` and README's tooling sections never mention it.
- **Impact:** None today; it is simply the only live-diagnostics option the project currently has, and nobody would know.
- **Recommendation:** One README line under Quality & Tests: `dotnet counters monitor -n ResetYourFuture.Web` as the stopgap until OBS-2.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| OBS-1 | Medium | S | Add /healthz + /readyz with DbContext and Ollama checks |
| OBS-3 | Medium | S | Log the traceId with errors so ProblemDetails responses are correlatable |
| OBS-2 | Medium | M | Adopt minimal OTel (hosting + ASP.NET instrumentation + OTLP exporter) |
| OBS-6 | Low | S | Log assistant retrieval quality per question |
| OBS-4 | Low | M | Emit structured (JSON) log lines or swap sink to Serilog compact JSON |
| OBS-5 | Low | M | Report call outcomes server-side (counter or structured event) |
| OBS-7 | Info | S | Document dotnet-counters as the current live-diagnostics fallback |

## 5. Related Findings Elsewhere

- **37 (LOG)** — owns the file logger's current-state defects that OBS-3/OBS-4 build on (scope support LOG-8, retention LOG-5, alerting LOG-1).
- **36 (AVAIL)** — owns the uptime/detection consequence of having no health endpoint; OBS-1 is the instrument.
- **34 (PERF)** — performance findings lack baselines until OBS-2 exists.
- **39 (CFG)** — hardcoded assistant `MinScore` (CFG territory) is untunable partly because OBS-6 data is missing.
- **41 (CLOUD)** — where an OTLP backend/dashboard would actually be hosted.
- **42 (OPS)** — runbook/incident procedure that would consume health and metrics signals.
