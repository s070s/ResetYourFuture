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
| Medium | 0 |
| Low | 5 |
| Info | 1 |

Overall the security foundations are notably strong for a certificate project: HttpOnly + `SameSite=Strict` auth cookie (which neutralises CSRF on the cookie-authenticated API surface), security-stamp revalidation on every cookie request, every JWT validation, and now every refresh-token use, SHA-256-hashed rotating refresh tokens with reuse detection, HMAC-SHA256 Stripe signature verification with timestamp replay rejection, an HTML sanitizer applied at every rich-text write path, a hardened public media endpoint (`sandbox` CSP + extension allowlist), and DataProtection-signed sign-in tickets. All four Medium findings are now fixed or substantially addressed: the lesson-asset endpoint uses a scoped single-lesson token instead of the general access JWT, sensitive endpoints carry a per-user rate limiter, the Stripe webhook fails closed without a configured secret, and the CSP's `script-src` no longer needs `'unsafe-inline'` (its `style-src` counterpart and self-hosting the two CDN assets remain open — see SEC-5). None of the remaining Low findings are exploitable to a full compromise in the current dev/demo configuration.

## 3. Findings

### SEC-5: CSP `style-src` still relies on `'unsafe-inline'`; two CDN assets remain externally hosted  [Low] [Effort: M]
- **Evidence:** `Startup/SecurityHeadersMiddlewareExtensions.cs` — `script-src` no longer needs `'unsafe-inline'` (fixed: the app's only inline-script surface was two `<link onload="...">` lazy-CSS attributes in `App.razor`, now handled by an external `wwwroot/js/lazy-css.js`; verified live via the served CSP header and a clean browser console). `style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com` remains: dozens of components legitimately bind `style="@expr"`, a common Razor pattern, and the two CDN origins (bootstrap-icons, Font Awesome, Quill JS/CSS) are still allow-listed by hostname rather than self-hosted.
- **Impact:** `style-src 'unsafe-inline'` is a materially smaller risk than the `script-src` case that was fixed — CSS injection can exfiltrate via `:has()`/attribute selectors or deface, but cannot directly execute arbitrary script. The CDN dependency means a compromised CDN could serve malicious CSS/JS, mitigated somewhat by the existing SRI hash on the Font Awesome `<link>` (Quill's script/style tags have none).
- **Recommendation:** Auditing every inline `style="@expr"` binding across ~70 components to move to CSS custom properties/classes is a separate, much larger pass — do it opportunistically. Self-hosting the two CDN assets (vendor the files under `wwwroot/lib/`) is more contained and could land independently; add SRI to the Quill `<script>`/`<link>` tags either way.

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
| SEC-5 | Low | M | Audit `style="@expr"` bindings toward dropping `style-src 'unsafe-inline'`; self-host the two CDN assets |
| SEC-6 | Low | S | CI gate against shipping DEBUG builds; startup assertion for dev-only endpoints |
| SEC-7 | Low | S | Return generic credentials error for unconfirmed accounts on the Blazor login path |
| SEC-8 | Low | S | Strengthen password policy + compromised-password screening |
| SEC-9 | Low | S | Set admin-backup cookie to `SameSite=Strict` |
| SEC-10 | Info | S | Neutralise placeholder secrets in `.env.template` |

## 5. Related Findings Elsewhere

- **REL (26):** `DeleteUser` throws an unhandled exception (Restrict FKs) — same code path this report treats for authz; REL owns the crash/failure-mode angle.
- **REL (26):** SSR loopback consumers (`ApiClientBase`) swallow non-success responses → silent blank pages; interacted with the now-fixed lesson-asset flow (SEC-2).
- **BIZ (27):** Webhook event dispatch is unimplemented (why the pre-fix SEC-4 fail-open default wasn't yet exploitable) and mock payment grants plans without charge.
- **COMP (29):** GDPR erasure completeness, special-category (psychosocial) data at rest, and minor-consent enforcement — the regulatory counterparts to the account/data handling reviewed here.
- **DQ (28):** DTO/column `MaxLength` mismatch on testimonials can 500 on save (input-validation integrity).
- **CFG (39):** `AllowedHosts` restricted to localhost, HSTS toggle, and production secret provisioning.
- **DEP (43):** Ganss.Xss / MailKit / QuestPDF / OpenIddict-adjacent JWT library versions and CVE status.
