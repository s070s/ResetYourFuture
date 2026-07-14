# Deployment notes

Three facts about this app only exist as source comments today (MAINT-4). Anyone deploying
from the README alone would discover each one at runtime — the first as *every page silently
rendering empty*, the worst possible debugging entry point. This page hoists them out.

Required secrets are validated together at startup (`Startup/StartupConfigValidation.cs`,
MAINT-5): missing/invalid values are reported in one exception listing everything, not one
restart at a time. See `.env.template` for the full local-dev list.

## Required secrets

| Key | Notes |
|---|---|
| `Jwt:Key` | Must be ≥ 32 bytes (HMAC-SHA256). Env var: `Jwt__Key`. |
| `AdminUser:Password` | Seeds the admin account on first boot. Env var: `AdminUser__Password`. |
| `ConnectionStrings:DefaultConnection` | SQL Server connection string. |
| `Email:Smtp:Host` (+ credentials) | Required outside Development — see [Email transport](#email-transport) below. |
| `SeedData:StudentPassword` | Only required when `SeedData:Enabled=true` (Development sample-data seeding). |

## SelfBaseUrl and the loopback API call

Blazor Server renders pages by calling the app's **own** REST API over an in-process
`HttpClient` (`Startup/ServiceRegistrationExtensions.cs`, `ResolveSelfBaseUrl`) — a deliberate
architectural tradeoff (ARCH-1), not a bug. Two things follow:

1. **`SelfBaseUrl` must be set to the real bound base address outside Development.** The app
   fails fast at startup if it's unset or still points at `localhost` in a non-Development
   environment.
2. **The loopback HTTPS certificate must be trusted by that same in-process `HttpClient`.**
   This is *not* validated at startup — if the cert isn't trusted, every API-backed page
   renders silently empty (`ApiClientBase` swallows non-success responses) instead of
   erroring visibly.

## DataProtection key ring

Keys are persisted to the shared SQL database (`PersistKeysToDbContext<ApplicationDbContext>`),
not the local filesystem — this is what makes auth cookies and the `/auth/complete` handshake
ticket valid across a redeploy or container rebuild onto a fresh disk, and across more than one
instance reading the same database.

On Windows, DPAPI (`ProtectKeysWithDpapi()`) also encrypts the keys at rest. **DPAPI is
machine-locked** — fine for the single Windows host this runs on today, but before a genuine
multi-instance or cross-platform deployment, swap it for `ProtectKeysWithCertificate()` or a
key vault so every instance can decrypt the shared key ring.

## Email transport

`SmtpEmailService` (MailKit) is used whenever `Email:Smtp:Host` is configured — point it at
Papercut/Mailhog in Development or a real relay (SES/SendGrid SMTP/etc.) in production.
Development falls back to `StubEmailService` (logs only) when no SMTP host is configured; any
other environment **fails fast at startup** so emails are never silently swallowed in
production. Full SMTP options: `Email:Smtp:{Host,Port,UseStartTls,Username,Password,FromAddress,FromName}`.
