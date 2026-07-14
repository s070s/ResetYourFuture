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
| Medium | 0 |
| Low | 3 |
| Info | 0 |

Overall: this is a university certificate project and nothing here is broken *today*; the findings are graded as the debt you would pay down first if the app ever ran unattended. The Medium findings are now resolved: **OBS-1** (health endpoints) was delivered under AVAIL-1 — `/health/live` and `/health/ready` with a database check and an Ollama check that degrades gracefully; **OBS-3** (correlation) is fixed — the production exception handler logs the exception with the same traceId ProblemDetails returns, and log lines carry the ambient trace id; and **OBS-2** (full OpenTelemetry adoption) is accepted as forward-looking debt — its value requires a metrics backend this single-instance, zero-new-infrastructure project deliberately does not run, and `dotnet-counters` (now documented in the README, OBS-7) plus the daily log error-digest (LOG-1) cover live diagnostics until then. The codebase remains well-positioned for cheap OTel adoption when a backend exists: logging already uses structured message templates throughout, dependencies are few and well-bounded, and ASP.NET Core 10's built-in OTel support means that path is mostly configuration, not code.

> **Accepted since audit (out of scope — will not implement):** OBS-2 (no metrics/tracing / no OpenTelemetry). A useful OTel pipeline needs somewhere to send the telemetry — an OTLP collector + metrics/trace backend (the finding suggests a local `grafana/otel-lgtm` docker) — which is exactly the new infrastructure this single-instance, zero-new-infrastructure / fresh-clone project deliberately avoids, and there is no unattended operation for the metrics to serve. `dotnet-counters` (documented in the README, OBS-7) gives zero-setup live runtime metrics, and the daily log error-digest (LOG-1) surfaces errors, so the current diagnostics needs are met. Adopting OTel remains the documented next step (§2) if the app is ever run unattended, on the same basis as the accepted scalability/availability limitations. See [35-audit-scalability.md](35-audit-scalability.md) and [36-audit-availability.md](36-audit-availability.md).

## 3. Findings

> The three Medium findings are resolved: **OBS-1** (health endpoints) was delivered under AVAIL-1; **OBS-3** (traceId correlation) is fixed — see git (`Fix OBS-3`); and **OBS-2** (OpenTelemetry) is accepted as forward-looking debt (see the banner above and §2). The remaining open items are three Low.

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

## 4. Prioritized Action List

All three Medium items are resolved (OBS-1 via AVAIL-1, OBS-3 fixed, OBS-2 accepted) and OBS-7 (document `dotnet-counters`) is done in the README. The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| OBS-6 | Low | S | Log assistant retrieval quality per question |
| OBS-4 | Low | M | Emit structured (JSON) log lines or swap sink to Serilog compact JSON |
| OBS-5 | Low | M | Report call outcomes server-side (counter or structured event) |

## 5. Related Findings Elsewhere

- **37 (LOG)** — owns the file logger's current-state defects that OBS-3/OBS-4 build on (scope support LOG-8, retention LOG-5, alerting LOG-1).
- **36 (AVAIL)** — owns the uptime/detection consequence of having no health endpoint; OBS-1 is the instrument.
- **34 (PERF)** — performance findings lack baselines until OBS-2 exists.
- **39 (CFG)** — hardcoded assistant `MinScore` (CFG territory) is untunable partly because OBS-6 data is missing.
- **41 (CLOUD)** — where an OTLP backend/dashboard would actually be hosted.
- **42 (OPS)** — runbook/incident procedure that would consume health and metrics signals.
