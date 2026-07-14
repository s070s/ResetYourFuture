# Audit: Configuration

| | |
|---|---|
| Finding prefix | CFG |
| Created | 2026-07-11 |
| Scope | Configuration mechanics: appsettings layout, the custom `.env` loader (load order, error behavior), `.env.template` completeness, startup validation of options, launchSettings, and values hardcoded in code that belong in configuration |
| Delegated | Secret *strength/exposure* vulnerabilities → 25 (SEC). Build-time pinning (global.json, Directory.Packages.props) → 40 (BUILD). Production checklist/process gaps → 42 (OPS). Hosting-environment config (reverse proxy, certs) → 41 (CLOUD). |

## 1. Methodology

Read in full: `src/ResetYourFuture.Web/appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`, `Startup/EnvFileLoader.cs`, `Startup/ServiceRegistrationExtensions.cs`, `Startup/AuthenticationSetupExtensions.cs`, `Startup/DatabaseSeedingExtensions.cs`, `.env.template`, `src/ResetYourFuture.Application/Common/AssistantOptions.cs`, `src/ResetYourFuture.Infrastructure/Configuration/EmailOptions.cs`; searched `src/` for `ValidateOnStart` / `ValidateDataAnnotations` / `IValidateOptions` (zero hits) and for consumers of `Payment:*`, `SelfBaseUrl`, `Sitemap:BaseUrl`. Secret *values* were not read or reproduced anywhere in this report (keys/files referenced only). No build/run performed.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 3 |
| Info | 1 |

Overall: the configuration design is thoughtful for a project of this scale — secrets are kept out of the repo by design, the custom `EnvFileLoader` runs *before* `CreateBuilder`, and the most dangerous keys fail fast at startup with clear messages. All four Medium findings are now resolved: `.env.template` covers the production-required keys (CFG-2); `EnvFileLoader` respects real env-var precedence, strips quotes, logs its source, and handles read errors (CFG-3); Assistant/WebRtc/Email options are validated at startup and `Sitemap:BaseUrl` moved to the aggregated startup check (CFG-4); and the payment keys are bound through a discoverable `PaymentOptions` class (CFG-5, its security half already closed under SEC-4). What remains is two Low items and one Info.

## 3. Findings

> The four Medium findings (CFG-2 `.env.template`, CFG-3 `EnvFileLoader`, CFG-4 options validation, CFG-5 payment config) are fixed — see git (`Fix CFG-2` … `Fix CFG-4 and CFG-5`). CFG-5's security half (unverified webhook) was already closed under SEC-4. The remaining open items are two Low and one Info.

### CFG-6: Behavioral constants hardcoded in code that belong in configuration  [Low] [Effort: M]
- **Evidence:** `AssistantRetrievalService.cs:18` — retrieval `MinScore = 0.4f` (the one assistant knob *not* in the otherwise complete `Assistant` section); auth rate limit 10/min (`ServiceRegistrationExtensions.cs:147-153`); SignalR `MaximumReceiveMessageSize = 32_000` (line 131); cookie lifetimes 24 h sliding / 7-day persistent (`AuthenticationSetupExtensions.cs:94`, `InfrastructureEndpointsExtensions.cs:168`); upload size caps (`Infrastructure/ApiServices/LocalFileStorage.cs:15-18`); sitemap cache 30 min (`InfrastructureEndpointsExtensions.cs:227`).
- **Impact:** Tuning any of these (e.g. MinScore, which directly controls assistant grounding quality — see OBS-6 in report 38) requires a rebuild; inconsistent with the project's own habit of configuring similar knobs (`WebRtc:RingTimeoutSeconds`, `Assistant:RequestsPerMinute`).
- **Recommendation:** Promote selectively, not wholesale: `MinScore` into `AssistantOptions` (clear precedent), rate-limit numbers into config if they'll ever be demo-tuned; leave the rest unless a need appears — each promoted key must also land in `appsettings.json` and the README table.

### CFG-7: Three overlapping base-URL keys must agree but nothing relates them  [Low] [Effort: S]
- **Evidence:** `appsettings.json` — `SelfBaseUrl` (line 10, dev value), `App:BaseUrl` (lines 40-42, production value), `Sitemap:BaseUrl` (lines 49-51, production value). `App:BaseUrl` builds email confirmation/reset links; `Sitemap:BaseUrl` builds sitemap URLs; `SelfBaseUrl` is the loopback API address. The committed defaults are mutually inconsistent (one localhost, two production).
- **Impact:** Easy to update one and not the others; a deployment that sets `SelfBaseUrl` but inherits the committed `App:BaseUrl` sends users email links to the wrong host (or vice versa in dev, where email links point at reset-your-future.com unless overridden — Development json overrides `App:BaseUrl` but not `Sitemap:BaseUrl`).
- **Recommendation:** Collapse `Sitemap:BaseUrl` into `App:BaseUrl` (single public-URL key), keep `SelfBaseUrl` separate (it is legitimately different — loopback), and document the distinction in the README Configuration table.

### CFG-8: Single launch profile; no Production-like local profile  [Info] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Properties/launchSettings.json` — one `https` profile, `ASPNETCORE_ENVIRONMENT=Development`, port 7090 (matching the `SelfBaseUrl` dev default).
- **Impact:** None day-to-day; it just means the Production code paths (real exception handler, HSTS, email fail-fast, no seed data) are never exercised locally, so CFG-1/CFG-2 class problems stay invisible until a real deployment.
- **Recommendation:** Optionally add a `https-prodlike` profile with `ASPNETCORE_ENVIRONMENT=Production` and documented required env vars, as a cheap pre-deployment smoke test.

## 4. Prioritized Action List

All four Medium items (CFG-2 through CFG-5) are resolved. The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| CFG-7 | Low | S | Merge Sitemap:BaseUrl into App:BaseUrl; document key roles |
| CFG-6 | Low | M | Promote MinScore (and selectively other constants) into configuration |
| CFG-8 | Info | S | Optional Production-like launch profile |

## 5. Related Findings Elsewhere

- **25 (SEC)** — exploitability of the unverified Stripe webhook and JWT/secret-strength topics; CFG-5 covers only the configuration mechanics.
- **27 (BIZ)** — checkout dead-ending in `pending_payment` when mock payments are off is a business-logic gap; CFG-5 references it.
- **21 (ARCH)** — the self-HTTP-loopback architecture that makes `SelfBaseUrl` exist at all.
- **40 (BUILD)** — version pinning files (`global.json`, `Directory.Packages.props`) and the OpenApi auto-pin trap.
- **41 (CLOUD)** — host-level configuration (reverse proxy forwarded headers, Kestrel certs) that no appsettings key currently addresses.
- **42 (OPS)** — the README production checklist as an operational document (OPS-5 owns the checklist gap; CFG-5 the underlying keys).
- **37 (LOG)** — `Logging:LogLevel` config partially ignored by the file logger (LOG-7).
