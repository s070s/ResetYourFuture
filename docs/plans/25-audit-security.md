# Audit: Security

| | |
|---|---|
| Finding prefix | SEC |
| Created | 2026-07-11 |
| Scope | Technical vulnerabilities and hardening: authentication/authorization, JWT & refresh-token handling, session/cookie security, injection & XSS, CSRF posture, secrets handling, rate limiting, security headers, file-upload safety. |
| Delegated | Regulatory posture (GDPR erasure/retention/consent, privacy/terms pages, special-category data) → COMP (29). Domain-rule correctness of payments/subscriptions/certificates → BIZ (27). Unhandled-exception / swallowed-error failure modes → REL (26). Config mechanics (AllowedHosts, HSTS toggle, secret provisioning) → CFG (39). Custom file-logger behaviour → LOG (37). |

## 1. Methodology

Read the authentication pipeline (`Startup/AuthenticationSetupExtensions.cs`, `Program.cs`, `Startup/InfrastructureEndpointsExtensions.cs`, `Startup/ServiceRegistrationExtensions.cs`, `Startup/SecurityHeadersMiddlewareExtensions.cs`), the token services (`Infrastructure/ApiServices/TokenService.cs`, `Infrastructure/Services/AuthService.cs`, `Web/Services/SsrApiHandler.cs`, `Web/Services/ApiTokenProvider.cs`, `Web/Consumers/ApiClientBase.cs`), all auth flows (`Application/ApiServices/AuthApiService.cs`, `Web/Controllers/AuthController.cs`, `Web/Pages/Login.razor.cs`), authorization on every controller and both SignalR hubs, the sanitizer wiring (grep of `MarkupString` / `Sanitize`), file-upload paths (`Infrastructure/ApiServices/LocalFileStorage.cs`, `Web/Controllers/MediaController.cs`, `ProfileController.cs`, `SiteSettingsController.cs`, `LessonAssetsController.cs`), the payment webhook (`Web/Controllers/SubscriptionController.cs`), rate-limiter registration, and `.env.template` / `appsettings*.json` (keys only — no secret values read into this report).

NOT examined at the code level: TLS/reverse-proxy config, DataProtection key-ring persistence for multi-instance (called out in code comments; a deployment concern → CFG/CLOUD), and dependency CVEs → DEP (43).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 4 |
| Low | 4 |
| Info | 1 |

> **Fixed since audit:** SEC-1 (High — refresh tokens survived password reset and had no reuse detection) — `RefreshToken` now carries `SecurityStampAtIssuance`, checked on every `RefreshAsync` call (a stale stamp both rejects and revokes the token); every password-reset path (`AuthApiService.ResetPasswordAsync`/`DevResetPasswordAsync`, `AdminUserService.SetPasswordAsync`, and the Blazor-circuit `AuthService.ResetPasswordAsync`) now bulk-revokes the user's active refresh tokens; and presenting an already-revoked token now walks and revokes the whole `ReplacedByTokenId` descendant chain (reuse detection). Verified live against the real JWT API: rotation, replay-of-a-spent-token rejection, chain-wide revocation on reuse, and admin-forced-reset invalidating a token minted before it. Caught and fixed a real bug along the way — `ExecuteUpdateAsync` (the initial implementation) throws `InvalidOperationException` on the EF InMemory provider the test suite uses; switched to tracked-mutation + `SaveChangesAsync`, matching the pattern `NotificationService.cs` already uses for the same reason.

Overall the security foundations are notably strong for a certificate project: HttpOnly + `SameSite=Strict` auth cookie (which neutralises CSRF on the cookie-authenticated API surface), security-stamp revalidation on every cookie request, every JWT validation, and now every refresh-token use, SHA-256-hashed rotating refresh tokens with reuse detection, HMAC-SHA256 Stripe signature verification with timestamp replay rejection, an HTML sanitizer applied at every rich-text write path, a hardened public media endpoint (`sandbox` CSP + extension allowlist), and DataProtection-signed sign-in tickets. The remaining gaps: token-in-query-string exposure, incomplete rate-limiter coverage, and a CSP that still relies on `unsafe-inline` for scripts. None are exploitable to a full compromise in the current dev/demo configuration.

## 3. Findings

### SEC-2: Access tokens transmitted in query strings (SignalR hubs and lesson-asset streaming)  [Medium] [Effort: M]
- **Evidence:** `Startup/AuthenticationSetupExtensions.cs:145-158` reads the JWT from `?access_token=` for `/hubs/chat`, `/hubs/call`, and `/api/lessons`. `Web/Pages/LessonViewer.razor.cs:152-178` builds `<video>`/`<iframe>` URLs that append `&access_token={jwt}` to `/api/lessons/{id}/asset`.
- **Impact:** Bearer JWTs land in browser history, server request logs, and any intermediary/proxy access logs. A 15-minute token captured from a log grants the holder the user's identity until expiry. The lesson-asset URL is especially exposed because it is embedded in rendered HTML the browser stores.
- **Recommendation:** SignalR-over-WebSocket query-string tokens are a framework constraint, but keep their lifetime minimal and ensure request logging strips `access_token` (coordinate with LOG-37). For lesson assets, prefer a short-lived signed path token (DataProtection, single-asset scope) or a cookie-authenticated asset endpoint instead of the raw JWT in the URL.

### SEC-3: Several sensitive state-changing endpoints have no rate limiting  [Medium] [Effort: S]
- **Evidence:** Only two limiter policies exist — `"auth"` (fixed window 10/min, applied to `AuthController` methods) and `"assistant"` (`ServiceRegistrationExtensions.cs:145-167`). No global limiter is configured. `ProfileController.ChangePassword` / `UploadAvatar`, `AdminController.SetPassword` / `ForcePasswordReset`, `SubscriptionController.CreateCheckout`, `AssessmentsController.SubmitAssessment`, and `ChatController` carry no `[EnableRateLimiting]`. `ChatHub.SendMessage` has only a per-message length cap, no send-rate cap.
- **Impact:** Unbounded request volume enables password-change brute force (bounded by lockout, but still), avatar/upload storage abuse, assessment-submission spam, and chat flooding. No back-pressure exists for a single authenticated abuser.
- **Recommendation:** Add a sensible per-user default limiter (mirroring the `"assistant"` partition pattern) and attach `"auth"`-class limits to `change-password`, `set-password`, and `force-password-reset`. Consider a lightweight send-rate guard in `ChatHub`.

### SEC-4: Stripe webhook accepts unsigned requests when `Payment:WebhookSecret` is unset  [Medium] [Effort: S]
- **Evidence:** `Web/Controllers/SubscriptionController.cs:100-155`. The endpoint is `[AllowAnonymous]`; when `Payment:WebhookSecret` is blank (the production default — the key is unset in `appsettings.json` and only optionally set via env) it logs a warning and returns `200 OK` without verifying any signature (`:111-115`).
- **Impact:** Anyone can POST to `/api/subscriptions/webhook` and receive a success acknowledgement. Today the handler performs no state change (event dispatch is unimplemented — see BIZ), so it is not yet exploitable for privilege escalation, but the fail-open default is a latent vulnerability: the moment event dispatch is wired without first requiring the secret, forged events could grant paid tiers.
- **Recommendation:** Fail closed — if the endpoint is reachable and no secret is configured, reject (or require an explicit `Payment:MockEnabled` bypass gate that is never on in production). Never dispatch subscription-granting events on an unverified request.

### SEC-5: Content-Security-Policy relies on `script-src 'unsafe-inline'` plus broad CDN allowances  [Medium] [Effort: M]
- **Evidence:** `Startup/SecurityHeadersMiddlewareExtensions.cs:26-37`: `script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net`, `style-src` also `'unsafe-inline'`, plus `cdn.jsdelivr.net` / `cdnjs.cloudflare.com` in script/style/font/connect.
- **Impact:** `'unsafe-inline'` on `script-src` means the CSP provides no meaningful defence against injected inline scripts — any stored/reflected HTML that slips past the sanitizer executes. The sanitizer is currently applied everywhere rich HTML is written (verified), so this is defence-in-depth rather than an active hole, but it removes the CSP as a second layer.
- **Recommendation:** Move Blazor's inline bootstrap to a nonce-based CSP (`script-src 'self' 'nonce-…'`) and drop `'unsafe-inline'` for scripts; self-host the two CDN assets to tighten `script-src`/`style-src` to `'self'`. Coordinate with the Blazor circuit-init script requirement noted in the file.

### SEC-6: DEBUG-only auth-bypass endpoints allow arbitrary email-confirm and password reset  [Low] [Effort: S]
- **Evidence:** `Web/Controllers/AuthController.cs:134-160` (`dev/confirm-email`, `dev/reset-password`), backed by `AuthApiService.DevConfirmEmailAsync` / `DevResetPasswordAsync` (`:288-321`). Guarded by both `#if DEBUG` (compiled out of Release) and a runtime `_env.IsDevelopment()` check.
- **Impact:** These confirm any account or set any user's password with no current-password/token. The double guard (compile-time + runtime) is solid, so the risk is only realised if a `DEBUG` build is ever shipped to a non-dev host — in which case it is full account takeover of any email.
- **Recommendation:** Keep the guards; add a CI gate that fails if a `DEBUG` assembly is produced for release, and consider a startup assertion that throws if `#if DEBUG` endpoints are registered outside Development.

### SEC-7: Account enumeration via distinct "email not confirmed" response on the Blazor login path  [Low] [Effort: S]
- **Evidence:** `Infrastructure/Services/AuthService.cs:111-112` returns `EmailNotConfirmedError` (a distinct message) when the email exists but is unconfirmed; `Web/Pages/Login.razor.cs:69-75` surfaces it as targeted UI. (The JSON API path `AuthApiService.LoginAsync:117-128` correctly returns the generic `InvalidCredentials` for both cases.)
- **Impact:** An attacker can distinguish "registered-but-unconfirmed" from "unknown/wrong-password" accounts, enabling email enumeration through the interactive login form.
- **Recommendation:** Return the generic invalid-credentials message for unconfirmed accounts too, and surface resend-confirmation guidance only after a separate, rate-limited "resend" action rather than inferring it from the login failure.

### SEC-8: Weak password policy and no breach/compromised-password check  [Low] [Effort: S]
- **Evidence:** `Startup/AuthenticationSetupExtensions.cs:37-40`: `RequiredLength = 8`, digit + uppercase required, `RequireNonAlphanumeric = false`.
- **Impact:** Eight-character passwords with no symbol and no compromised-password screening are brute-forceable offline if a hash ever leaks and weak against credential-stuffing.
- **Recommendation:** Raise to 10–12 chars, and add a compromised-password check (k-anonymity HaveIBeenPwned API or a local common-password list) on register / change-password.

### SEC-9: Admin-backup impersonation cookie uses `SameSite=Lax`, not `Strict`  [Low] [Effort: S]
- **Evidence:** `Startup/InfrastructureEndpointsExtensions.cs:147-157` writes `.RYF.AdminUserId` with `SameSite=SameSiteMode.Lax` (the primary `.RYF.Auth` cookie is `Strict`). Value is DataProtection-protected (integrity-protected).
- **Impact:** Minor — the cookie only records which admin to restore to on impersonation exit and is integrity-protected, but the looser `Lax` scope is inconsistent with the Strict primary cookie for no clear reason.
- **Recommendation:** Set `SameSite=Strict` to match `.RYF.Auth`.

### SEC-10: `.env.template` ships weak default development secrets  [Info] [Effort: S]
- **Evidence:** `.env.template` documents `Jwt__Key`, `AdminUser__Password`, and `SeedData__StudentPassword` with weak, human-guessable placeholder values (keys referenced only; values not reproduced here). `appsettings.json` correctly leaves these blank by design; the JWT length guard (`AuthenticationSetupExtensions.cs:49-50`) enforces ≥32 bytes at startup.
- **Impact:** No production impact as long as real values are provisioned via env/User Secrets. The risk is purely that a developer copies the template verbatim into a shared/exposed environment.
- **Recommendation:** Replace the template placeholders with clearly non-functional tokens (e.g. `__REPLACE_ME__`) so a copy-paste cannot silently yield a working weak secret; document rotation. (Provisioning mechanics → CFG-39.)

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| SEC-2 | Medium | M | Remove raw JWT from lesson-asset URLs; ensure `access_token` is stripped from logs |
| SEC-3 | Medium | S | Add per-user default limiter + `auth`-class limits on password/checkout/submit endpoints |
| SEC-4 | Medium | S | Make the Stripe webhook fail closed when the signing secret is absent |
| SEC-5 | Medium | M | Move to nonce-based CSP; drop `script-src 'unsafe-inline'`; self-host CDN assets |
| SEC-6 | Low | S | CI gate against shipping DEBUG builds; startup assertion for dev-only endpoints |
| SEC-7 | Low | S | Return generic credentials error for unconfirmed accounts on the Blazor login path |
| SEC-8 | Low | S | Strengthen password policy + compromised-password screening |
| SEC-9 | Low | S | Set admin-backup cookie to `SameSite=Strict` |
| SEC-10 | Info | S | Neutralise placeholder secrets in `.env.template` |

## 5. Related Findings Elsewhere

- **REL (26):** `DeleteUser` throws an unhandled exception (Restrict FKs) — same code path this report treats for authz; REL owns the crash/failure-mode angle.
- **REL (26):** SSR loopback consumers (`ApiClientBase`) swallow non-success responses → silent blank pages; interacts with SEC-2's asset flow.
- **BIZ (27):** Webhook event dispatch is unimplemented (why SEC-4 is not yet exploitable) and mock payment grants plans without charge.
- **COMP (29):** GDPR erasure completeness, special-category (psychosocial) data at rest, and minor-consent enforcement — the regulatory counterparts to the account/data handling reviewed here.
- **DQ (28):** DTO/column `MaxLength` mismatch on testimonials can 500 on save (input-validation integrity).
- **CFG (39):** `AllowedHosts` restricted to localhost, HSTS toggle, and production secret provisioning.
- **DEP (43):** Ganss.Xss / MailKit / QuestPDF / OpenIddict-adjacent JWT library versions and CVE status.
