# Audit: Operational Readiness

| | |
|---|---|
| Finding prefix | OPS |
| Created | 2026-07-11 |
| Scope | Running the system as an operator: runbook/procedures, backup & restore, seeding behavior per environment, admin bootstrap and recovery, migration/update procedure, maintenance mode, incident path |
| Delegated | Alerting mechanism on logged errors → 37 (LOG-1). Health/metrics instruments → 38 (OBS). Config keys and fail-fast validation → 39 (CFG). Deployment artifact/pipeline → 40 (BUILD). Host provisioning/topology → 41 (CLOUD). Uptime/failure-mode analysis → 36 (AVAIL). |

## 1. Methodology

Read in full: `src/ResetYourFuture.Web/Startup/DatabaseSeedingExtensions.cs`, `src/ResetYourFuture.Infrastructure/Seeding/BulkStudentSeedingService.cs`, `BlogArticleSeeder.cs`, `SubscriptionPlanSeeder.cs` (headers), README production checklist and Email/Configuration sections, `.env.template`, `.gitignore` (state paths all ignored), `appsettings*.json`, `Controllers/SubscriptionController.cs` (webhook path), `Application/ApiServices/SubscriptionService.cs` (mock-payment path), `Application/ApiServices/AdminUserService.cs` (admin lifecycle operations). NOT examined: `CourseSeeder`/`AssessmentSeeder`/`StudentSeeder` internals beyond their Development-only call sites, and no runtime verification (no build/run per repo constraints).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 4 |
| Low | 2 |
| Info | 1 |

> **Fixed since audit:** OPS-1 (High — blog seeder ran unconditionally and could delete operator-authored articles) — the delete/reseed branch is gone (`BlogArticleSeeder.SeedAsync` now only checks "skip if any articles exist"), and the call site moved inside the Development + `SeedData:Enabled` gate alongside the other content seeders. A regression test (`BlogArticleSeeder_NeverDeletesExistingEnglishOnlyArticles`) proves pre-existing English-only articles survive a reseed attempt.

Overall: for a project that has never been operated, the operational *design instincts* are good — startup fails fast on missing admin password and email transport instead of limping, all content seeding (including blog, now) is double-gated (environment AND config flag), the bulk seeder runs as a background service so it can't block boot, and the README production checklist is a real (if incomplete) operator document, which is more than most student projects have. What's missing is everything that only matters after day one: no backup or restore story for the two data stores, an auto-migration habit with no rollback procedure, and no runbook telling a future operator (including the author in six months) how to update, recover, or even notice a problem.

## 3. Findings

### OPS-2: No backup or restore procedure for either data store  [Medium] [Effort: S]
- **Evidence:** The system has exactly two stores of irreplaceable state: the SQL database (users, enrollments, certificates, chat history) and uploaded files under `src/ResetYourFuture.Web/App_Data/Uploads` (`LocalFileStorage.cs:38` — avatars, lesson videos, certificate PDFs). Both are gitignored (`.gitignore`: `App_Data/`, `*.db`); LocalDB's MDF lives in the user profile. No backup script, schedule, or restore test exists anywhere in the repo or README.
- **Impact:** Any disk failure, accidental `sqllocaldb delete`, or careless redeploy (uploads sit inside the deploy folder — CLOUD-1 in report 41) is unrecoverable data loss. Restore has never been rehearsed, so even an existing ad-hoc copy is of unknown value.
- **Recommendation:** Smallest real fix: a documented two-liner — `sqlcmd ... BACKUP DATABASE` (or `sqlpackage /a:Export`) plus a robocopy of `App_Data` — with a note that both must be taken together for consistency, and one rehearsed restore. Wire it to a Windows scheduled task if the demo machine matters.

### OPS-3: Migrations auto-run at startup with no rollback or update procedure  [Medium] [Effort: M]
- **Evidence:** `Startup/DatabaseSeedingExtensions.cs:46-47` — `MigrateAsync()` on every boot of a relational host. README:406 mentions only that the DB user needs schema rights on first deploy. There is no documented update procedure at all (stop → backup → deploy → boot/migrate → verify), no rollback guidance, and migrations are never tested against real SQL Server before they run for real (BUILD-2 in report 40).
- **Impact:** The first failed migration bricks startup (app down until someone hand-fixes the schema), and because there is no backup-before-migrate habit (OPS-2), a *partially applied* or data-mangling migration has no undo. Auto-migrate is a fine choice for this project's scale — the missing procedure around it is the finding.
- **Recommendation:** Document the update runbook with "backup first" as step 1 (OPS-2's script); note the escape hatch (`dotnet ef migrations script --idempotent` via the pinned `dotnet-ef` tool in `.config/dotnet-tools.json`) for generating a reviewable SQL script when a migration looks risky.

### OPS-4: No runbook or incident procedure — operational knowledge lives only in code comments and one person's memory  [Medium] [Effort: M]
- **Evidence:** The only operator-facing documentation is README's Quickstart, Configuration table, and production checklist (lines 401-406). Nothing answers: where are the logs and what do errors look like (report 37), how do I restart the app/Ollama/LocalDB, what do I check when pages render empty (the known `SelfBaseUrl` failure, CFG-1), how do I disable a misbehaving feature, who is affected if I restart (Blazor Server: every active circuit drops).
- **Impact:** Any incident — including during a graded demo — is debugged from scratch. The bus factor is exactly 1, and even that 1 will lose context between now and the defense.
- **Recommendation:** One `docs/runbook.md` with the five most likely scenarios and their first three diagnostic steps each: app won't start (config fail-fasts and their messages), pages empty (SelfBaseUrl/loopback TLS), logins failing (key ring/cookies), assistant down (Ollama service + status ping), email not arriving (stub vs SMTP selection logic at `ServiceRegistrationExtensions.cs:40-53`). Half of it can be lifted from existing code comments, which are unusually good.

### OPS-5: README production checklist omits the payment keys and the known deployment traps  [Medium] [Effort: S]
- **Evidence:** README production checklist (lines 401-406) covers environment, JWT key, connection string, AllowedHosts, email, migration rights — but not `Payment:MockEnabled` (must be off; `appsettings.Development.json:11-13`) or `Payment__WebhookSecret` (without it webhook signature checks are skipped — `SubscriptionController.cs:110-113`), not `SelfBaseUrl` (CFG-1, the fail-silent one), and not forwarded-headers/reverse-proxy setup (CLOUD-2).
- **Impact:** The checklist is the closest thing to a deployment gate; the four omissions are precisely the items whose failure modes are silent (empty pages, unverified webhooks, mock payments) rather than fail-fast, i.e. the ones a checklist exists for.
- **Recommendation:** Add the four lines to the checklist. Longer-term,每 fail-fast added under CFG-1/CFG-5 removes a checklist line — prefer code enforcement over prose.

### OPS-6: Admin bootstrap has no rotation or recovery story  [Low] [Effort: S]
- **Evidence:** First admin is seeded from `AdminUser:Email`/`AdminUser:Password` (`DatabaseSeedingExtensions.cs:65-93`); startup throws if the password is unset (good fail-fast). But: the password lives permanently in `.env`; the seeder only *creates* (`FindByEmailAsync` guard at line 72), so changing the env value never updates the account and stale credentials linger in the file; and if the sole admin account is disabled/deleted (both possible via `AdminUserService`) or its password lost, recovery is manual DB surgery — no CLI/break-glass path.
- **Impact:** Low likelihood, high confusion: the operator's mental model ("the password is whatever `.env` says") diverges from reality after the first rotation. Lockout of the only admin is plausible via the app's own admin-management UI.
- **Recommendation:** Document that the env password is *initial-only*, and rotate it in-app then blank the env var. For break-glass, document the one-liner (temporarily set a new `AdminUser:Email` in env to seed a fresh admin) — it already works with the existing code and costs nothing.

### OPS-7: No maintenance mode or feature kill switches  [Low] [Effort: M]
- **Evidence:** The only runtime feature toggle is `Assistant:Enabled` (evaluated once at startup, `ServiceRegistrationExtensions.cs:94-113`). There is no way to put the site into a maintenance page during a risky migration (OPS-3), nor disable payments/calls/chat individually if one misbehaves.
- **Impact:** Operator's only lever for any problem is full shutdown; combined with auto-migration this means schema changes always happen with users (or graders) potentially connected.
- **Recommendation:** Proportionate version: a single `Maintenance:Enabled` config flag checked by a small middleware that returns a static page for non-admins. Skip per-feature flags unless a concrete need appears.

### OPS-8: Environment gating of seeding is otherwise correct (positive observation)  [Info] [Effort: —]
- **Evidence:** Course/assessment/student JSON seeds are gated to Development + `SeedData:Enabled` (`DatabaseSeedingExtensions.cs:96`); bulk students are additionally a non-blocking background service with the same double gate and a fail-safe skip when the seed password is unset (`BulkStudentSeedingService.cs:32-48`); roles/plans/admin are idempotent create-if-missing. The blog seeder (former OPS-1) now follows the same pattern too.
- **Impact:** None — positive observation, now uniform across every content seeder.
- **Recommendation:** None; preserve.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| OPS-2 | Medium | S | Document + schedule DB and App_Data backup; rehearse one restore |
| OPS-5 | Medium | S | Add MockEnabled, WebhookSecret, SelfBaseUrl, forwarded-headers to the production checklist |
| OPS-3 | Medium | M | Write the update/migration runbook (backup-first, idempotent-script escape hatch) |
| OPS-4 | Medium | M | Write docs/runbook.md covering the five likely incident scenarios |
| OPS-6 | Low | S | Document admin password rotation reality and the break-glass re-seed path |
| OPS-7 | Low | M | Optional Maintenance:Enabled middleware |
| OPS-8 | Info | — | Keep the existing seeding-gate pattern (now uniform across all content seeders) |

## 5. Related Findings Elsewhere

- **37 (LOG)** — no alerting on logged errors (LOG-1) is the tooling half of OPS-4's missing incident path; admin-action attribution (LOG-4) is what an incident review would need.
- **38 (OBS)** — health endpoints and metrics the runbook/monitoring would consume.
- **39 (CFG)** — fail-fast enforcement (CFG-1, CFG-5) that shrinks the OPS-5 checklist; `.env` loader behavior (CFG-3) behind OPS-6's rotation confusion.
- **40 (BUILD)** — CI migration testing (BUILD-2) de-risks OPS-3; publish artifact (BUILD-3) is a prerequisite for a clean update procedure.
- **41 (CLOUD)** — state locations that OPS-2 must back up (CLOUD-1) and the host/topology the runbook describes (CLOUD-5).
- **28 (DQ) / 26 (REL)** — data-integrity and startup-failure framing of the seeding/migration behaviors whose *procedures* are owned here.
- **27 (BIZ)** — checkout dead-end when mock payments are off; OPS-5 covers only the checklist omission.
