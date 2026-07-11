# Audit: Cloud & Infrastructure

| | |
|---|---|
| Finding prefix | CLOUD |
| Created | 2026-07-11 |
| Scope | Hosting/topology posture: what is machine-local today (database, file storage, DataProtection keys, logs, Ollama, SMTP), reverse-proxy and TLS readiness, IaC, and what a minimal single-VM/cloud deployment would require |
| Delegated | Multi-instance/scale-out blockers (circuits, SignalR backplane, in-memory state) → 35 (SCALE). Failure/recovery consequences → 36 (AVAIL). Config keys and validation → 39 (CFG). Container build artifact → 40 (BUILD-7). Backup/restore and deployment procedure → 42 (OPS). |

## 1. Methodology

Read: `src/ResetYourFuture.Web/Startup/ServiceRegistrationExtensions.cs` (DataProtection lines 171-186, Ollama registration 96-109, self-clients 211-253), `Startup/AuthenticationSetupExtensions.cs` (SQL Server + retry), `src/ResetYourFuture.Infrastructure/ApiServices/LocalFileStorage.cs`, `src/ResetYourFuture.Infrastructure/ApiServices/SmtpEmailService.cs` registration path, `appsettings.json`/`appsettings.Development.json`, `Program.cs` (HTTPS redirect/HSTS lines 68-74), `.gitignore`; searched `src/` for `UseForwardedHeaders`/`ForwardedHeadersOptions`/`KnownProxies` (zero hits) and the repo for Dockerfile/compose/IaC files (none). On-disk state verified: `src/ResetYourFuture.Web/DataProtection-Keys/` (one key XML), `App_Data/Uploads/`, `Logs/`. No build/run performed.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 2 |
| Low | 4 |
| Info | 1 |

Overall: the project is a deliberate single-Windows-machine development artifact, and — to its credit — it *knows* it: the DataProtection registration carries an accurate in-code NOTE about multi-instance and ephemeral-host breakage, storage is behind an `IFileStorage` interface, email is behind `IEmailService` with a real SMTP implementation ready, and the AI dependency is a cleanly isolated localhost sidecar that is off by default. Nothing here is broken in the app's actual usage, so nothing rates above Medium. The two Medium findings are the ones that would sting *immediately* on any real host: every stateful surface (DB, uploads, keys, logs) lives in the application folder on one machine, and the app assumes it terminates TLS itself — put it behind any reverse proxy and scheme detection, HTTPS redirects, and Secure cookies misbehave because forwarded headers are never processed.

## 3. Findings

### CLOUD-1: Every stateful surface is machine-local, most of it inside the application folder  [Medium] [Effort: L]
- **Evidence:** Database: LocalDB connection (`appsettings.Development.json:9`), production string blank-by-design (`appsettings.json:17-19`). Uploads: `LocalFileStorage` writes to `<ContentRoot>/App_Data/Uploads` (`Infrastructure/ApiServices/LocalFileStorage.cs:38`). DataProtection key ring: `<ContentRoot>/DataProtection-Keys` (`ServiceRegistrationExtensions.cs:178-181`, with its own NOTE at 171-177 admitting this breaks on multi-instance/ephemeral hosts). Logs: `Logs/` relative directory (`Program.cs:12`). All four paths are gitignored siblings of the binaries.
- **Impact:** A minimal real deployment must relocate four kinds of state at once; forgetting any one produces a distinct failure (lost uploads on redeploy, all users signed out and impersonation/auth-completion tickets invalidated when the key ring vanishes, logs wiped). Because state lives *inside* the deploy folder, the natural "delete and re-copy" update procedure is destructive (procedure → OPS in report 42; scale-out variant → report 35).
- **Recommendation:** Minimal single-VM path, in dependency order: (1) SQL Server (container or managed) via `ConnectionStrings__DefaultConnection`; (2) move `App_Data` and `Logs` to paths *outside* the deploy folder (both are already configurable-in-principle: `LocalFileStorage` base and `AddFileLogger` argument — make them config keys); (3) `PersistKeysToFileSystem` to a durable non-deploy path (or DB via `PersistKeysToDbContext`); (4) blob-storage `IFileStorage` implementation only when leaving a single VM.

### CLOUD-2: No forwarded-headers handling — the app assumes it terminates TLS itself  [Medium] [Effort: S]
- **Evidence:** Zero `UseForwardedHeaders`/`ForwardedHeadersOptions` in `src/` (verified search). Meanwhile the pipeline hard-depends on correct scheme detection: `app.UseHttpsRedirection()` (`Program.cs:68`), `UseHsts()` in non-Development (`Program.cs:70-74`), `CookieSecurePolicy.Always` in non-Development (`AuthenticationSetupExtensions.cs:98-100`).
- **Impact:** Behind any TLS-terminating reverse proxy (nginx, Caddy, IIS ARR, a cloud load balancer) requests arrive as `http`; the app then issues redirect loops via HTTPS redirection and refuses to send the Secure auth cookie — total login breakage that looks like a mysterious cookie bug. This is the classic first-hour-of-deployment failure for this stack.
- **Recommendation:** Either set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (zero code; enables the default X-Forwarded-For/Proto processing) and document it in the production checklist, or add explicit `UseForwardedHeaders` with `KnownProxies` configuration before `UseHttpsRedirection`. Pair with a documented sample nginx/Caddy block (OPS runbook territory).

### CLOUD-3: DataProtection at-rest encryption is Windows-only; Linux hosts get plaintext keys silently  [Low] [Effort: S]
- **Evidence:** `ServiceRegistrationExtensions.cs:183-186` — `if (OperatingSystem.IsWindows()) dpBuilder.ProtectKeysWithDpapi();` with a comment telling future maintainers to use a certificate/KeyVault on Linux, but no enforcement: on Linux the condition simply skips protection and the key ring is stored unencrypted, with no warning logged.
- **Impact:** A Linux/container deployment quietly downgrades key protection; whoever reads the key files can forge auth-completion tickets and decrypt protected payloads. Low in context (no Linux deployment exists or is planned), but the *silence* is the defect.
- **Recommendation:** Log a prominent startup Warning when keys are persisted unprotected outside Development; wire `ProtectKeysWithCertificate` from config when a cert path/thumbprint is provided.

### CLOUD-4: Ollama sidecar topology is undefined beyond localhost  [Low] [Effort: M]
- **Evidence:** `Assistant:BaseUrl` defaults to `http://localhost:11434` (`appsettings.json:66`, `AssistantOptions.cs:13`); registration creates plain `OllamaApiClient` instances (`ServiceRegistrationExtensions.cs:98-101`) — no auth, no TLS, no timeout/resilience policy on that HttpClient path. Models `gemma3:4b` + `bge-m3` imply multi-GB RAM on the host. Feature is `Enabled: false` by default.
- **Impact:** On any real host the assistant needs a co-located Ollama service (systemd/container), host sizing, and — if ever moved off-box — transport security that the plain-HTTP client doesn't provide. None of this is documented; README covers local dev setup only.
- **Recommendation:** Keep localhost-sidecar as the blessed topology and say so: one README paragraph (run Ollama as a service on the same host, keep 11434 firewalled to loopback), plus a note that `Assistant:BaseUrl` must remain loopback until TLS/auth is added.

### CLOUD-5: No infrastructure definition of any kind (IaC, compose, provisioning notes)  [Low] [Effort: M]
- **Evidence:** Repo contains no terraform/bicep/ansible/compose/cloud-init or equivalent (verified searches); the closest artifact is the README production checklist (lines 401-406), which covers app config only — nothing about provisioning a host, SQL Server, TLS certificates, or the Ollama service.
- **Impact:** The environment is unreproducible; standing up a second machine (new demo laptop, examiner's environment, a real VM) is an archaeology exercise. For this project's grading context, a *description* matters more than automation.
- **Recommendation:** Lowest-effort fix is a `docs/deployment.md` describing the single-VM reference topology (OS, SQL Server, reverse proxy + cert, service definitions, env vars, state paths from CLOUD-1). A compose file (BUILD-7 in report 40) can follow and make the doc executable.

### CLOUD-6: No production TLS/endpoint configuration for Kestrel  [Low] [Effort: S]
- **Evidence:** No `Kestrel` section in `appsettings.json`; HTTPS exists only via the dev certificate and `launchSettings.json` (`https://localhost:7090`). Production TLS is thus entirely dependent on a fronting proxy — which CLOUD-2 shows the app isn't prepared for either. `AllowedHosts` ships as `localhost;127.0.0.1` (`appsettings.json:9`; documented override in `.env.template` and README:397).
- **Impact:** Neither of the two possible TLS termination points (Kestrel direct or reverse proxy) is currently configured/supported; a deployer must invent one. The AllowedHosts default at least fails *loudly* (400s) and is documented, so it stays a footnote here (key mechanics → report 39, CFG).
- **Recommendation:** Decide and document the blessed option: reverse proxy termination (then CLOUD-2's forwarded headers are mandatory) — recommended — or Kestrel-direct with a `Kestrel:Endpoints:Https:Certificate` config block documented in the checklist.

### CLOUD-7: SMTP egress is the one external dependency that is deployment-ready  [Info] [Effort: —]
- **Evidence:** `SmtpEmailService` (MailKit, STARTTLS by default per `Infrastructure/Configuration/EmailOptions.cs:16`) is auto-selected whenever `Email:Smtp:Host` is set, and non-Development startup fails fast without it (`ServiceRegistrationExtensions.cs:40-53`).
- **Impact:** None — positive observation: pointing at any relay (SES/SendGrid SMTP) is pure configuration. Remaining gap is only the missing template keys (CFG-2 in report 39) and the cloud-provider reality that VMs often block port 25/587 egress by default — worth one checklist line.
- **Recommendation:** Add "verify SMTP egress from the host (587 STARTTLS)" to the deployment doc from CLOUD-5.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| CLOUD-2 | Medium | S | Enable forwarded-headers processing; document reverse-proxy assumption |
| CLOUD-1 | Medium | L | Externalize state: SQL Server, out-of-deploy-folder App_Data/Logs/keys, then blob storage when >1 host |
| CLOUD-3 | Low | S | Warn loudly when DataProtection keys persist unencrypted; cert-based protection from config |
| CLOUD-6 | Low | S | Choose and document the TLS termination story |
| CLOUD-4 | Low | M | Document the Ollama sidecar topology and loopback-only constraint |
| CLOUD-5 | Low | M | Write the single-VM reference deployment doc (then compose via BUILD-7) |
| CLOUD-7 | Info | — | One checklist line on SMTP egress verification |

## 5. Related Findings Elsewhere

- **35 (SCALE)** — owns the multi-instance blockers (Blazor circuits, SignalR without a backplane, `CallRegistry`/caches in memory, key-ring sharing); CLOUD-1 deliberately stops at the single-VM view.
- **36 (AVAIL)** — failure/recovery consequences of single-machine state (what happens when this box dies).
- **39 (CFG)** — `SelfBaseUrl` fail-silent (CFG-1), `.env.template` gaps (CFG-2), `AllowedHosts` mechanics; also where CLOUD-1's new path keys would be documented.
- **40 (BUILD)** — container publish target (BUILD-7) that would make CLOUD-5's doc executable; CI SQL Server container (BUILD-2).
- **42 (OPS)** — backup/restore of the state inventoried in CLOUD-1, and the deployment/update procedure.
- **25 (SEC)** — security consequences of plaintext key rings and unauthenticated sidecar transport.
