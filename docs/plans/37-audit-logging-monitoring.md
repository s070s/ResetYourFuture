# Audit: Logging & Monitoring

| | |
|---|---|
| Finding prefix | LOG |
| Created | 2026-07-11 |
| Scope | Current state of logging: the custom file logger (design, delivery guarantees, rotation/retention), what is and is not logged (auth, payments, admin actions, assistant), log levels/config, PII in logs, alerting on logged errors |
| Delegated | Forward-looking instrumentation (OTel, health checks, correlation IDs, metrics, dashboards) → 38 (OBS). Uptime/failure-mode consequences → 36 (AVAIL). GDPR/retention regulatory posture → 29 (COMP). Incident-response *procedure* → 42 (OPS). |

## 1. Methodology

Examined: `src/ResetYourFuture.Web/Logging/FileLogger.cs`, `FileLoggerProvider.cs`, `FileLoggerExtensions.cs`; wiring in `src/ResetYourFuture.Web/Program.cs` (line 12); `Logging` sections of `appsettings.json` / `appsettings.Development.json`; the on-disk `src/ResetYourFuture.Web/Logs/` directory (daily files present, gitignored); a repo-wide inventory of `logger.Log*` call sites (169 calls across 52 files) with targeted reads of `AuthApiService.cs`, `AdminUserService.cs`, `SubscriptionService.cs`, `SubscriptionController.cs`, `AssistantService.cs`, `StubEmailService.cs`, `InfrastructureEndpointsExtensions.cs`. NOT examined: contents of the actual log files (they contain user emails; verified only file names/dates), and no build/run was performed (per repo constraints).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 4 |
| Info | 0 |

Overall: logging is in noticeably better shape than typical for a project of this size. There is a deliberate, well-commented custom file provider (bounded channel, single background writer, daily files) layered on top of the default console/debug providers, sensible category filters in `appsettings.json` (EF command noise and `Microsoft.AspNetCore` capped at Warning, `Microsoft.AspNetCore.Authorization` filtered in code), and genuinely good *event coverage*: every auth outcome (register, login success/failure, lockout, refresh rotation, password reset), subscription changes, admin user-management actions, impersonation, seeding, and hub lifecycle events are all logged with structured message templates.

All four Medium findings are resolved: auth flows now log user IDs, not emails (LOG-2); the file provider counts dropped entries and drains its writer on shutdown (LOG-3); admin-action logs carry the acting admin's ID (LOG-4); and a daily digest surfaces the previous day's error count instead of leaving the logs write-only (LOG-1). The log directory is also anchored to the content root (LOG-6). What remains is four Low items.

## 3. Findings

> The four Medium findings (LOG-1 error digest, LOG-2 PII, LOG-3 file-provider drop/drain, LOG-4 admin actor) are fixed, along with LOG-6 (log directory anchored to the content root) — see git (`Fix LOG-1` … `Fix LOG-4`, `Fix LOG-1 and LOG-6`). The remaining open items are four Low.

### LOG-5: No log retention or cleanup — unbounded growth  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Logging/FileLoggerProvider.cs:36` creates `log-{date}.txt` daily; nothing anywhere deletes old files. `src/ResetYourFuture.Web/Logs/` already accumulates files (2026-07-07 onward); the directory is gitignored (`.gitignore`: `Logs/`) so growth is invisible in the repo.
- **Impact:** Disk creep on any long-lived host; combined with LOG-2, an ever-growing PII archive with no retention policy.
- **Recommendation:** On rollover in `WriteLoopAsync` (the `logFile != currentFile` branch), delete files older than N days (configurable, default 14). One `Directory.EnumerateFiles` + date parse; ~15 lines.

### LOG-7: Hardcoded `Information` floor ignores configured log levels  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Logging/FileLogger.cs:18` — `IsEnabled(logLevel) => logLevel >= LogLevel.Information`, regardless of `Logging:LogLevel` in `appsettings.json`.
- **Impact:** Setting `"Default": "Debug"` in configuration raises console verbosity but the file silently never captures Debug/Trace — the one sink that persists is the one you cannot turn up when diagnosing an issue.
- **Recommendation:** Remove the hardcoded check (return `true`; the `LoggerFactory` already applies configured filter rules per provider) or make the floor a constructor parameter fed from configuration (`Logging:File:MinLevel`).

### LOG-8: Scopes are discarded — per-request context never reaches the file  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Logging/FileLogger.cs:16` — `BeginScope` returns `null`.
- **Impact:** ASP.NET Core's request/connection scopes (which carry the `TraceIdentifier` that ProblemDetails hands to clients) are dropped, so log lines from one request cannot be grouped. This is the mechanical half of the correlation gap; the adoption path is OBS territory (report 38).
- **Recommendation:** Implement a minimal `IExternalScopeProvider` (or accept one via `ISupportExternalScope`) and append flattened scope values to the entry line.

### LOG-9: No HTTP request/access logging  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Program.cs` — no `UseHttpLogging`/W3C logging middleware; only application events are logged. `Microsoft.AspNetCore` is filtered to Warning (`appsettings.json:5`), so per-request Info lines from the framework are suppressed too.
- **Impact:** No record of which endpoints were hit, by whom, with what status — makes post-hoc investigation of "was this endpoint probed / who downloaded that certificate" impossible from logs alone.
- **Recommendation:** For the demo context, `app.UseHttpLogging()` gated to Production with fields limited to method/path/status/duration is enough; revisit under the OTel path (report 38) where `http.server.request.duration` metrics largely replace access logs.

## 4. Prioritized Action List

All four Medium items (LOG-1 through LOG-4) are resolved, along with LOG-6. The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| LOG-5 | Low | S | Delete log files older than N days on rollover |
| LOG-7 | Low | S | Drop the hardcoded Information floor; respect configured levels |
| LOG-8 | Low | S | Support scopes so request context reaches file entries |
| LOG-9 | Low | S | Add minimal HTTP request logging for non-Development |

## 5. Related Findings Elsewhere

- **38 (OBS)** — owns the forward-looking fix for correlation (traceId in logs, OBS finding on ProblemDetails traceId), health checks, and the OTel adoption path that would eventually replace this file logger's monitoring role.
- **29 (COMP)** — regulatory angle of PII retention in logs (LOG-2/LOG-5 provide the technical evidence).
- **42 (OPS)** — no runbook/incident procedure; LOG-1 is the tooling half, OPS owns the process half.
- **36 (AVAIL)** — uptime consequences of nobody noticing failures; LOG-1 is the detection mechanism gap.
- **25 (SEC)** — webhook signature-check skip is *logged* but only as a Warning (`SubscriptionController.cs:113`); the vulnerability itself is SEC/CFG territory (see CFG-5 in report 39).
