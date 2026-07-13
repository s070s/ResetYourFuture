# Design: Confirmation/reset auth flow completion (email transport already implemented)

**Date:** 2026-06-25 · **Trimmed to remaining work:** 2026-07-14
**Status:** Partially implemented — the transport half (former §§1–3: `SmtpEmailService`, `EmailOptions`, registration) is done and removed from this document; the flow half (§§4–9 below) remains.
**Area:** `src/ResetYourFuture.Web` (Blazor Server), `src/ResetYourFuture.Infrastructure`, `src/ResetYourFuture.Application`

## Problem (remaining)

The email transport exists (`SmtpEmailService`/MailKit, selected whenever `Email:Smtp:Host` is configured; `StubEmailService` stays the Development default; production fails fast when nothing is configured), and the JSON API path already sends confirmation/reset emails — but the flows are incomplete:

- `AuthService.RegisterAsync` and `AuthService.ForgotPasswordAsync` (the Blazor **cookie** path used
  by the UI) generate tokens but send **no** email.
- There is no `/reset-password` Blazor page — the reset links the API path emails
  (`{host}/reset-password?email=…&token=…`, built in `AuthController`) land on the NotFound page.
- There is no `/confirm-email` landing page (the emailed confirmation link hits the raw JSON API
  action) and no production resend-confirmation endpoint.

This blocks two audit UI/UX issues:
1. Self-service email-confirmation resend on the Login page (guidance message exists; no resend button
   because nothing can send the email).
2. The Forgot Password flow: the user submits from the Blazor page but no reset email is sent, and the
   API-path reset email points at a page that does not exist.

## Context / current state

- **Two parallel auth paths.** The Blazor UI (`Register.razor`, `Login.razor`, `ForgotPassword.razor`)
  calls `IAuthService` (`AuthService`, cookie path). The JSON `AuthController` is a separate API
  surface. `AuthController` already injects `IEmailService` and calls it; `AuthService` does **not**
  inject `IEmailService` at all. This work targets the Blazor/`AuthService` path.
- `AuthService.ResetPasswordAsync` and `AuthController` `POST /api/auth/reset-password` already exist —
  the `/reset-password` page has an endpoint to post to.
- `Login.razor` already detects unconfirmed email (`result.Message.Contains("email not confirmed")`)
  and sets `unconfirmedEmailPending` + `pendingUnconfirmedEmail`. Only the resend endpoint + button are
  missing. A dev-only self-confirm button already exists, gated by `Env.IsDevelopment()`.
- `AuthService` runs inside the Blazor circuit where `HttpContext` is null, so it cannot derive
  scheme/host for links the way `AuthController` does (`Request.Scheme`/`Request.Host`). Links must come
  from configuration.
- `IEmailService` lives in `ResetYourFuture.Application/ApiInterfaces` (namespace
  `ResetYourFuture.Web.ApiInterfaces`). `StubEmailService` lives in
  `ResetYourFuture.Infrastructure/ApiServices` (namespace `ResetYourFuture.Web.ApiServices`). The Web
  project references Infrastructure (Program.cs already uses `StubEmailService`).

## Decisions

- **Confirmation landing:** add a friendly `/confirm-email` Blazor page (symmetric with
  `/reset-password`), backed by a new `IAuthService.ConfirmEmailAsync`.
- **Resend:** production controller endpoint `POST api/auth/resend-confirmation`, rate-limited via the
  existing `"auth"` policy, called from `Login.razor` through the existing `"SelfClient"` HttpClient.
  Rationale: resend is an email-bombing vector and needs the ASP.NET rate limiter, which a circuit-side
  service method would not get.
- *(Implemented earlier, retained for context:)* the transport is MailKit/MimeKit SMTP behind
  `IEmailService`, configured by `EmailOptions` (`Email:Smtp:*`), selected whenever `Email:Smtp:Host`
  is set, with `StubEmailService` as the Development default and a production fail-fast when nothing
  is configured. Papercut testing via User Secrets: `Email:Smtp:Host=localhost`, `Port=25`,
  `UseStartTls=false`.

## Components (§§1–3, the transport, are implemented and removed; original numbering kept)

### 4. `AuthService` wiring — `src/ResetYourFuture.Infrastructure/Services/AuthService.cs`

- Add `IEmailService _emailService` constructor parameter (after `subscriptionService`, before
  `context`, or wherever cleanest — update all call sites incl. tests).
- Read `App:BaseUrl` from `IConfiguration` in the constructor into a field; trim trailing `/`. Fall back
  to a sane dev default (`https://localhost:7090`) if absent.
- Private helpers:
  - `BuildConfirmUrl(userId, token)` → `{baseUrl}/confirm-email?userId={esc}&token={esc}`
  - `BuildResetUrl(email, token)` → `{baseUrl}/reset-password?email={esc}&token={esc}`
  (use `Uri.EscapeDataString`).
- `RegisterAsync`: after `GenerateEmailConfirmationTokenAsync`, build the confirm URL and
  `await _emailService.SendEmailConfirmationAsync(user.Email!, url, ct)` inside a try/catch that logs and
  swallows — a transient SMTP failure must not roll back the created account. Update the NOTE comment.
- `ForgotPasswordAsync`: after `GeneratePasswordResetTokenAsync`, build the reset URL and
  `await _emailService.SendPasswordResetAsync(user.Email!, url, ct)` inside try/catch (log + swallow);
  keep the existing generic success message and the early generic return for unknown/unconfirmed users
  (no enumeration, no email). Update the NOTE comment.
- New `ConfirmEmailAsync(string userId, string token)`: `FindByIdAsync` → `ConfirmEmailAsync` → map to
  `AuthResponseDto`. Used by the `/confirm-email` page.

### 5. `IAuthService` — `src/ResetYourFuture.Application/Interfaces/IAuthService.cs`

Add:
```csharp
Task<AuthResponseDto> ConfirmEmailAsync(string userId, string token);
```
(Resend stays on the controller, not here.)

### 6. `/reset-password` Blazor page — `src/ResetYourFuture.Web/Pages/ResetPassword.razor(.cs)`

- `@page "/reset-password"`. `[SupplyParameterFromQuery] Email`, `[SupplyParameterFromQuery] Token`.
- Form: new password + confirm (reuse `Label_NewPassword`, `Label_Password`/confirm patterns,
  show/hide toggle consistent with Login/Register). Binds a `ResetPasswordRequestDto` (Email/Token from
  query, NewPassword/ConfirmPassword from inputs).
- Submit → `IAuthService.ResetPasswordAsync`. Success → success alert + link to `/login`. Failure →
  error alert with returned errors. Guard against missing token (show error, no form).

### 7. `/confirm-email` Blazor page — `src/ResetYourFuture.Web/Pages/ConfirmEmail.razor(.cs)`

- `@page "/confirm-email"`. `[SupplyParameterFromQuery] UserId`, `[SupplyParameterFromQuery] Token`.
- `OnInitializedAsync` → `IAuthService.ConfirmEmailAsync(UserId, Token)`. Show success (+ login link) or
  failure (+ hint to request a new link / resend on login). Guard missing params.

### 8. Resend endpoint + Login button

- `AuthController`: new `POST api/auth/resend-confirmation` `[EnableRateLimiting("auth")]`, body = email
  (string, like `dev/confirm-email`). Find user; **always** return generic
  `{ Success = true, Message = "If an account with that email exists and is unconfirmed, a new link has
  been sent." }`. Only when the user exists **and** is not yet confirmed: generate token, build
  `{Request.Scheme}://{Request.Host}/confirm-email?userId=…&token=…`, send via `_emailService`
  (try/catch log). No enumeration.
- `Login.razor`: in the existing `unconfirmedEmailPending` warning block, add a production "Resend
  confirmation email" button (alongside, not replacing, the dev-gated self-confirm button).
- `Login.razor.cs`: `ResendConfirmation()` posts `pendingUnconfirmedEmail` to
  `api/auth/resend-confirmation` via `HttpClientFactory.CreateClient("SelfClient")` (mirrors
  `DevConfirmPendingEmail`); on success show a confirmation message, on failure show a generic error.

### 9. Resource strings

Add new keys to `GlobalRes` / `SuccessMessagesRes` / `ErrorMessagesRes` as needed for the two pages and
the resend button (English `.resx`, Greek `.el.resx`, and `.Designer.cs`), following the existing
localization pattern. Reuse existing keys (`Label_NewPassword`, `Label_Password`, `Label_Login`,
`BackToLogin`, etc.) where they already exist. Candidate new keys: `ResetPasswordTitle`,
`ResetPasswordSuccess`, `ConfirmEmailTitle`, `ConfirmEmailSuccess`, `ConfirmEmailError`,
`Label_ResendConfirmation`, `ResendConfirmationSent`.

## Testing

- **`AuthServiceTests`**: extend the `Build()` harness with a mocked `IEmailService` (NSubstitute) and
  `App:BaseUrl` in the in-memory config; add it to the `Harness` record.
  - `RegisterAsync` success → `Received().SendEmailConfirmationAsync(email, url, …)` where url contains
    `/confirm-email` and the token.
  - `RegisterAsync` with email send throwing → still `Success == true` (account not rolled back).
  - `ForgotPasswordAsync` confirmed user → `Received().SendPasswordResetAsync(...)`, generic success.
  - `ForgotPasswordAsync` unknown/unconfirmed → `DidNotReceive()` email; generic success.
  - `ConfirmEmailAsync` success and failure paths.
- **`AuthController`** resend: if a controller test fixture exists, add tests (generic response always;
  email sent for unconfirmed user; not sent for unknown or already-confirmed).
- (`SmtpEmailServiceTests` for the transport's `BuildMessage` seam already exists from the implemented half.)

## Verification

- `dotnet build` and `dotnet test ResetYourFuture.sln` must pass.
- Manual live-send check against Papercut/Mailhog: set User Secrets
  (`Email:Smtp:Host=localhost`, `Port=25`, `UseStartTls=false`), run the app, register a user and use
  forgot-password / resend, observe the messages and follow the links to `/confirm-email` and
  `/reset-password`.

## Out of scope / non-goals

- Consolidating the two auth paths (`AuthService` cookie path vs `AuthController` JSON API) into one.
- Changing `AuthController.Register`/`ForgotPassword` link targets (they already send; API consumers
  keep their current behavior).
- Additional email types (welcome, receipts, etc.) named as future work in the `IEmailService` doc.
- Background/queued email delivery and retry — sends are inline with try/catch + log; resend covers
  transient failures.
