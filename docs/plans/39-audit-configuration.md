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
| High | 1 |
| Medium | 4 |
| Low | 3 |
| Info | 1 |

Overall: the configuration design is thoughtful for a project of this scale — secrets are kept out of the repo by design (blank-by-design keys in `appsettings.json`, gitignored `.env` seeded from `.env.template`, README Configuration table documenting each key), the custom `EnvFileLoader` runs *before* `CreateBuilder` so the standard environment-variable provider picks values up in the normal precedence chain, and the three most dangerous keys fail fast at startup with clear messages (JWT key length, admin password, missing email transport in Production). The weaknesses are at the edges: the one config value whose failure is *silent* rather than fast (`SelfBaseUrl`) is also the one every page depends on; the `.env.template` doesn't cover everything Production requires; options binding is entirely unvalidated; and a number of behavioral constants are compiled in.

## 3. Findings

### CFG-1: `SelfBaseUrl` falls back silently to localhost — every API-backed page renders empty when it's wrong  [High] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Startup/ServiceRegistrationExtensions.cs:213` — `var selfBase = builder.Configuration["SelfBaseUrl"] ?? "https://localhost:7090";` feeds the base address of all ~20 self-calling typed HttpClients (lines 216-253). The in-code NOTE (lines 204-210) itself states that in production a wrong value means "every API-backed page silently renders empty (ApiClientBase swallows non-success responses)". `.env.template` does not mention the key; `appsettings.json:10` carries only the dev value.
- **Impact:** The single most load-bearing config value has the *worst* failure mode in the codebase: no exception, no log, just an empty site. Anyone deploying to a real host and missing this key gets a fully "running" app that shows no data — maximally confusing to debug.
- **Recommendation:** Fail fast like the app already does for `Jwt:Key` (`AuthenticationSetupExtensions.cs:48-50`): in non-Development, throw at startup if `SelfBaseUrl` is unset or points at localhost. Add the key to `.env.template` and the README production checklist. (The self-HTTP-call architecture itself is report 21 (ARCH) territory; this finding is only about making the config failure loud.)

### CFG-2: `.env.template` is incomplete against what Production actually requires  [Medium] [Effort: S]
- **Evidence:** `.env.template` covers `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `AdminUser__Password`, `SeedData__StudentPassword`, `Payment__WebhookSecret` (commented), `AllowedHosts` (commented). Missing entirely: `Email__Smtp__Host`/`Port`/`Username`/`Password` — yet startup *throws* in non-Development without an SMTP host (`ServiceRegistrationExtensions.cs:49-53`); `SelfBaseUrl` (CFG-1); `Assistant__Enabled`/`Assistant__BaseUrl` (README's assistant setup section relies on editing appsettings instead).
- **Impact:** The template is the de-facto deployment contract ("copy the template and fill in your own values" — README Quickstart step 3), but following it to the letter in Production yields a startup crash (missing SMTP) and an empty site (missing SelfBaseUrl). The two failure messages arrive one at a time, not as one checklist.
- **Recommendation:** Add the missing keys as commented Production-section entries with one-line comments, mirroring the existing `Payment__WebhookSecret` style. Keep dev-required vs prod-required keys visually separated.

### CFG-3: `EnvFileLoader` inverts env-var precedence and parses permissively with no diagnostics  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Startup/EnvFileLoader.cs`: (a) lines 29-31 — `Environment.SetEnvironmentVariable` unconditionally, so a `.env` value **overwrites** a real environment variable set by the host/operator, the opposite of conventional dotenv precedence; (b) lines 35-49 — the walk-up searches as many as 5 parent directories, so a stray `.env` in e.g. `C:\Users\GS\Desktop` would be silently loaded when running from the repo; (c) quotes are not stripped (`KEY="value"` keeps the quotes in the value) and malformed lines are skipped silently; (d) `File.ReadAllLines` (line 21) has no error handling and runs before logging exists (Program.cs:7 precedes builder creation), so an unreadable/locked `.env` crashes the process with a raw exception.
- **Impact:** (a) is the sharp edge: on any host where ops sets environment variables (systemd unit, IIS config, container env), a forgotten `.env` file up the directory tree silently wins, and nothing logs which file was loaded or which keys it set. (c) produces classic "my password has quotes in it" mysteries.
- **Recommendation:** Only set a variable if `Environment.GetEnvironmentVariable(key) is null`; strip surrounding single/double quotes; `Console.WriteLine` (logging isn't up yet) the resolved `.env` path when one is found; wrap the read in try/catch with a clear message. ~15 lines total in `EnvFileLoader.cs`.

### CFG-4: No options validation — most sections bind unchecked, some fail at first request instead of startup  [Medium] [Effort: M]
- **Evidence:** Zero hits for `ValidateDataAnnotations`/`ValidateOnStart`/`IValidateOptions` in `src/`. Ad-hoc startup guards exist only for `Jwt:Key` (`AuthenticationSetupExtensions.cs:48-50`), `AdminUser:Password` (`DatabaseSeedingExtensions.cs:68-70`), email transport presence (`ServiceRegistrationExtensions.cs:49-53`), and `SeedData:StudentPassword` (`DatabaseSeedingExtensions.cs:108-110`). Unvalidated: `WebRtcOptions` (negative timeouts/participants bind fine), `AssistantOptions` (malformed `BaseUrl` throws deep in registration at `ServiceRegistrationExtensions.cs:98-101`), `EmailOptions` beyond Host presence (a typo'd port fails at first send), and `Sitemap:BaseUrl`, which throws at *request time* (`Startup/InfrastructureEndpointsExtensions.cs:221-222`) — the first crawler hit 500s instead of the operator learning at boot.
- **Impact:** Config errors surface late, one at a time, at the worst moments (first email, first crawler visit) instead of as a startup failure list.
- **Recommendation:** Use the pattern already half-present: `builder.Services.AddOptions<EmailOptions>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` for Email/Assistant/WebRtc, with `[Range]`/`[Url]`/`[Required]` annotations on the options classes (`Application/Common/AssistantOptions.cs`, `Infrastructure/Configuration/EmailOptions.cs`). Move the `Sitemap:BaseUrl` null-check to startup.

### CFG-5: Payment configuration is split, optional, and dangerous to get wrong  [Medium] [Effort: S]
- **Evidence:** `Payment:MockEnabled` exists only in `appsettings.Development.json:11-13` (defaults to `false` elsewhere — safe); `SubscriptionService.cs:31` reads it via raw `configuration.GetValue` (no options class, no section in `appsettings.json` to document it); `Payment:WebhookSecret` is optional and when absent the Stripe webhook endpoint **skips signature verification with only a Warning log** (`Controllers/SubscriptionController.cs:110-113`). The README Configuration table documents both keys, but the production checklist (README lines 401-406) mentions neither.
- **Impact:** Two inverse traps: a public host accidentally run with `ASPNETCORE_ENVIRONMENT=Development` gets mock payments (free subscriptions) *plus* seed data and Swagger; a Production host without the webhook secret accepts unauthenticated webhook posts (exploit mechanics → report 25, SEC). And with `MockEnabled` correctly off, checkout dead-ends in `pending_payment` (`SubscriptionService.cs:142-144`) — there is no real payment path at all (business consequence → report 27, BIZ).
- **Recommendation:** Add a `Payment` section (with `MockEnabled: false`) to `appsettings.json` so the key is discoverable; bind a `PaymentOptions` class; in non-Development, fail fast (or refuse the webhook route) when `WebhookSecret` is unset rather than warning-and-continuing; add both keys to the README production checklist (see also OPS-5 in report 42).

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

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| CFG-1 | High | S | Fail fast on unset/localhost SelfBaseUrl outside Development; add to template + checklist |
| CFG-2 | Medium | S | Complete .env.template (SMTP, SelfBaseUrl, Assistant keys) |
| CFG-3 | Medium | S | Fix .env precedence (env wins), strip quotes, log resolved path, handle read errors |
| CFG-5 | Medium | S | PaymentOptions + fail-fast on missing WebhookSecret in non-Development |
| CFG-4 | Medium | M | ValidateDataAnnotations + ValidateOnStart for Email/Assistant/WebRtc; startup-check Sitemap:BaseUrl |
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
