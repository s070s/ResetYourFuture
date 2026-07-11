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
| Medium | 4 |
| Low | 5 |
| Info | 0 |

Overall: logging is in noticeably better shape than typical for a project of this size. There is a deliberate, well-commented custom file provider (bounded channel, single background writer, daily files) layered on top of the default console/debug providers, sensible category filters in `appsettings.json` (EF command noise and `Microsoft.AspNetCore` capped at Warning, `Microsoft.AspNetCore.Authorization` filtered in code), and genuinely good *event coverage*: every auth outcome (register, login success/failure, lockout, refresh rotation, password reset), subscription changes, admin user-management actions, impersonation, seeding, and hub lifecycle events are all logged with structured message templates. The gaps are operational rather than developmental: log lines carry PII (emails), admin-action logs usually omit the acting admin, the provider can silently drop entries and never prunes old files, and nothing watches the logs — an error is only ever discovered by manually opening `Logs/log-YYYY-MM-DD.txt`.

## 3. Findings

### LOG-1: No alerting or error-surfacing path — logs are write-only  [Medium] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/Logging/FileLoggerProvider.cs` (writes flat text files only); no error-notification mechanism anywhere in `src/` (no email-on-error, no webhook, no event log integration beyond the default providers); README has no "how to check for errors" section.
- **Impact:** A production (or demo-day) exception is invisible until someone opens the current day's file in `src/ResetYourFuture.Web/Logs/`. Recurring failures (SMTP down, Ollama unreachable, webhook signature failures logged at Warning in `Controllers/SubscriptionController.cs:113`) can persist for weeks unnoticed.
- **Recommendation:** Cheapest meaningful step: a scheduled task or startup check that counts `[ERROR]` lines in yesterday's file and emails the admin via the existing `IEmailService`. Longer term this is subsumed by the OTel path in report 38 (OBS).

### LOG-2: PII (user emails) written to log files; stub emails put security tokens in logs  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Application/ApiServices/AuthApiService.cs` lines 53, 59, 85, 111, 120, 126, 132, 141, 144, 167, 243, 261, 266 — every auth event logs `{Email}`. `src/ResetYourFuture.Infrastructure/ApiServices/StubEmailService.cs:20-42` logs full email-confirmation and password-reset *links* (which embed the Identity tokens) into `Logs/` — intentional in Development (README documents "search STUB EMAIL"), but the tokens are live credentials while they sit in a plaintext file.
- **Impact:** Log files become a PII store with no retention limit (see LOG-5), which complicates the GDPR story (report 29, COMP) and makes casually sharing a log file for debugging unsafe. The stub-logged reset links are usable by anyone who can read the log file until consumed/expired.
- **Recommendation:** Log user IDs instead of emails in auth flows (the ID is already the key used everywhere else, e.g. `AdminUserService`); keep email only where the ID does not exist yet (registration failure). Keep StubEmailService behavior but note the token exposure in its XML doc, or log only the token's last 6 characters plus a dev endpoint to fetch the full link.

### LOG-3: File provider silently drops entries under burst and loses the tail on shutdown  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Logging/FileLoggerProvider.cs:18-23` — bounded channel (4096) with `FullMode = DropOldest`, and `FileLogger.cs:37` uses `TryWrite` — both drop without any trace. `FileLoggerProvider.Dispose()` (lines 68-72) calls `TryComplete()` but does **not** wait for `_writerTask` to drain, so entries still queued at shutdown (including the very exception that crashed the app) can be lost.
- **Impact:** The moments you most need logs — an exception storm, or a crash during shutdown — are exactly when entries vanish, with no "N entries dropped" marker to tell you the record is incomplete.
- **Recommendation:** In `Dispose()`, `TryComplete()` then `_writerTask.Wait(TimeSpan.FromSeconds(2))`. Track a drop counter (increment when `TryWrite` returns false via `Channel` full detection or a `WaitToWriteAsync` fallback) and emit a `[WARN] N entries dropped` line when the writer catches up.

### LOG-4: Admin audit trail lacks actor identity  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Application/ApiServices/AdminUserService.cs` lines 126, 143, 161, 179, 197, 248, 263, 277, 318 — "Admin assigned role {Role} to user {UserId}", "Admin deleted user {UserId}", "Admin set new password for user {UserId}" — none records *which* admin acted. Only impersonation logs the actor (`AdminUserService.cs:293`, `InfrastructureEndpointsExtensions.cs:177`). Content-admin services log almost nothing (`AdminCourseService.cs`, `AdminCategoryService.cs`: one call each).
- **Impact:** With multiple admin accounts (the system supports role assignment), destructive actions (delete user, force password reset) cannot be attributed — the log answers "what happened" but not "who did it", which is the main point of an admin audit log.
- **Recommendation:** Pass the acting admin's user ID (already available from claims at the controller layer) into the admin services and include `{AdminId}` in every admin-action log line, matching the pattern already used at `AdminUserService.cs:293`.

### LOG-5: No log retention or cleanup — unbounded growth  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Logging/FileLoggerProvider.cs:36` creates `log-{date}.txt` daily; nothing anywhere deletes old files. `src/ResetYourFuture.Web/Logs/` already accumulates files (2026-07-07 onward); the directory is gitignored (`.gitignore`: `Logs/`) so growth is invisible in the repo.
- **Impact:** Disk creep on any long-lived host; combined with LOG-2, an ever-growing PII archive with no retention policy.
- **Recommendation:** On rollover in `WriteLoopAsync` (the `logFile != currentFile` branch), delete files older than N days (configurable, default 14). One `Directory.EnumerateFiles` + date parse; ~15 lines.

### LOG-6: Log directory is resolved relative to the launch directory  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Program.cs:12` — `builder.Logging.AddFileLogger("Logs")`; `FileLoggerProvider` ctor calls `Directory.CreateDirectory(logDirectory)` against the process CWD. The repo already acknowledges launch-directory variance elsewhere: `Startup/EnvFileLoader.cs` walks up 5 directories precisely because the app is launched "from the solution root (dotnet run --project …) or the project directory".
- **Impact:** Logs land in `<repo-root>/Logs` or `<project>/Logs` depending on how the app was started; an operator tailing the wrong folder concludes there are no logs. `Program.cs:99` logs the resolved path, which mitigates but only if you can find the log…
- **Recommendation:** Anchor to content root: `AddFileLogger(Path.Combine(builder.Environment.ContentRootPath, "Logs"))`.

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

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| LOG-2 | Medium | S | Log user IDs instead of emails in auth flows; contain stub-email token exposure |
| LOG-3 | Medium | S | Drain writer task on dispose; count and report dropped entries |
| LOG-4 | Medium | S | Include acting admin's ID in all admin-action log lines |
| LOG-1 | Medium | M | Add a minimal error-surfacing path (daily ERROR-count email via IEmailService) |
| LOG-5 | Low | S | Delete log files older than N days on rollover |
| LOG-6 | Low | S | Anchor the Logs directory to ContentRootPath |
| LOG-7 | Low | S | Drop the hardcoded Information floor; respect configured levels |
| LOG-8 | Low | S | Support scopes so request context reaches file entries |
| LOG-9 | Low | S | Add minimal HTTP request logging for non-Development |

## 5. Related Findings Elsewhere

- **38 (OBS)** — owns the forward-looking fix for correlation (traceId in logs, OBS finding on ProblemDetails traceId), health checks, and the OTel adoption path that would eventually replace this file logger's monitoring role.
- **29 (COMP)** — regulatory angle of PII retention in logs (LOG-2/LOG-5 provide the technical evidence).
- **42 (OPS)** — no runbook/incident procedure; LOG-1 is the tooling half, OPS owns the process half.
- **36 (AVAIL)** — uptime consequences of nobody noticing failures; LOG-1 is the detection mechanism gap.
- **25 (SEC)** — webhook signature-check skip is *logged* but only as a Warning (`SubscriptionController.cs:113`); the vulnerability itself is SEC/CFG territory (see CFG-5 in report 39).
