# Design: Real email service + confirmation/reset auth flows

**Date:** 2026-06-25
**Status:** Approved (pending spec review)
**Area:** `src/ResetYourFuture.Web` (Blazor Server), `src/ResetYourFuture.Infrastructure`, `src/ResetYourFuture.Application`

## Problem

Email-dependent auth flows are stubbed and block real functionality:

- Only `StubEmailService` implements `IEmailService`, registered in Development only. `Program.cs`
  fail-fasts in production if no real `IEmailService` is registered.
- `AuthService.RegisterAsync` and `AuthService.ForgotPasswordAsync` (the Blazor **cookie** path used
  by the UI) generate tokens but send **no** email.
- There is no `/reset-password` Blazor page and no production resend-confirmation endpoint.

This blocks two audit UI/UX issues:
1. Self-service email-confirmation resend on the Login page (guidance message exists; no resend button
   because nothing can send the email).
2. The Forgot Password flow: in production the user submits but no reset email is sent and there is no
   reset page to land on.

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

- **Provider:** MailKit/MimeKit SMTP — provider-agnostic, testable against Papercut/Mailhog locally and
  any prod relay (SES, SendGrid SMTP, Mailgun, O365). One implementation, no vendor lock-in.
- **Confirmation landing:** add a friendly `/confirm-email` Blazor page (symmetric with
  `/reset-password`), backed by a new `IAuthService.ConfirmEmailAsync`.
- **Resend:** production controller endpoint `POST api/auth/resend-confirmation`, rate-limited via the
  existing `"auth"` policy, called from `Login.razor` through the existing `"SelfClient"` HttpClient.
  Rationale: resend is an email-bombing vector and needs the ASP.NET rate limiter, which a circuit-side
  service method would not get.

## Components

### 1. `SmtpEmailService` — `src/ResetYourFuture.Infrastructure/ApiServices/SmtpEmailService.cs`

- Implements `IEmailService` (`SendEmailConfirmationAsync`, `SendPasswordResetAsync`).
- Depends on `IOptions<EmailOptions>` and `ILogger<SmtpEmailService>`.
- Builds a `MimeMessage` via a `static MimeMessage BuildMessage(EmailOptions, string to, string subject,
  string htmlBody, string textBody)` seam (unit-testable without a server). HTML body + plaintext
  alternative; subjects are simple ("Confirm your email", "Reset your password"). Bodies include the
  link as a clickable anchor and as raw text.
- Sends via `MailKit.Net.Smtp.SmtpClient`: `ConnectAsync(host, port, SecureSocketOptions)` where the
  option is `StartTls` when `UseStartTls` is true, else `None` (Papercut); authenticate only when
  `Username` is non-empty; `SendAsync`; `DisconnectAsync(true)`. Honors `CancellationToken`.
- Logs Information on success, Error on failure, and **rethrows** — the caller (`AuthService`) decides
  whether to swallow (see §4).

### 2. `EmailOptions` — `src/ResetYourFuture.Infrastructure/ApiServices/EmailOptions.cs`

```csharp
public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@reset-your-future.com";
    public string FromName { get; set; } = "Reset Your Future";
}
```

`appsettings.json` ships empty/placeholder values (matching `Jwt:Key`, `ConnectionStrings`); real
secrets come from User Secrets / env. `App:BaseUrl` is added for link generation:

```jsonc
"Email": {
  "Smtp": { "Host": "", "Port": 587, "UseStartTls": true, "Username": "", "Password": "",
            "FromAddress": "no-reply@reset-your-future.com", "FromName": "Reset Your Future" }
},
"App": { "BaseUrl": "https://reset-your-future.com" }
```

`appsettings.Development.json` may set `App:BaseUrl` to the dev origin (e.g. `https://localhost:7090`).
Papercut testing via User Secrets: `Email:Smtp:Host=localhost`, `Port=25`, `UseStartTls=false`.

### 3. `Program.cs` registration (replaces the fail-fast block, ~lines 240-257)

```csharp
builder.Services.Configure<EmailOptions>(config.GetSection(EmailOptions.SectionName));

var smtpHost = config["Email:Smtp:Host"];
if (!string.IsNullOrWhiteSpace(smtpHost))
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();      // dev (Papercut) or prod
else if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<IEmailService, StubEmailService>();      // dev default: logs only
else
    throw new InvalidOperationException(
        "No email transport configured. Set Email:Smtp:Host (and credentials) for production.");
```

Prod fail-safe is preserved: production with no SMTP configured still throws.

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

## Package changes

- Add `MailKit` PackageReference to `ResetYourFuture.Infrastructure.csproj` (pulls in MimeKit).

## Testing

- **`AuthServiceTests`**: extend the `Build()` harness with a mocked `IEmailService` (NSubstitute) and
  `App:BaseUrl` in the in-memory config; add it to the `Harness` record.
  - `RegisterAsync` success → `Received().SendEmailConfirmationAsync(email, url, …)` where url contains
    `/confirm-email` and the token.
  - `RegisterAsync` with email send throwing → still `Success == true` (account not rolled back).
  - `ForgotPasswordAsync` confirmed user → `Received().SendPasswordResetAsync(...)`, generic success.
  - `ForgotPasswordAsync` unknown/unconfirmed → `DidNotReceive()` email; generic success.
  - `ConfirmEmailAsync` success and failure paths.
- **`SmtpEmailService`**: unit-test `BuildMessage` (From address/name, To, Subject, HTML + text body
  contain the link). No live SMTP in unit tests.
- **`AuthController`** resend: if a controller test fixture exists, add tests (generic response always;
  email sent for unconfirmed user; not sent for unknown or already-confirmed).

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
