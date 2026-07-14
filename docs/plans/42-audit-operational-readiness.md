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
| Medium | 0 |
| Low | 2 |
| Info | 1 |

Overall: for a project that has never been operated, the operational *design instincts* are good — startup fails fast on missing config instead of limping, all content seeding is double-gated (environment AND config flag), and the bulk seeder runs as a background service so it can't block boot. The four Medium findings — all documentation gaps — are now resolved: a new [operations runbook](../runbook.md) covers backup & restore of the two state stores taken together (OPS-2), the backup-first update/migration procedure with the idempotent-script escape hatch and restore-based rollback (OPS-3), and first-response steps for the likely incident scenarios (OPS-4); and the README production checklist now names the payment keys, `SelfBaseUrl`, and the forwarded-headers/reverse-proxy setup (OPS-5, completed alongside the CFG-5/CLOUD-2 fixes). What remains is two Low items and one Info.

## 3. Findings

> The four Medium findings — all documentation gaps — are resolved: OPS-2 (backup/restore), OPS-3 (update/migration procedure) and OPS-4 (incident scenarios) are covered by the new [operations runbook](../runbook.md); OPS-5 (checklist keys) was completed via the CFG-5/CLOUD-2 README additions. See git (`Fix OPS-2, OPS-3, OPS-4`). The remaining open items are two Low and one Info.

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

All four Medium items (OPS-2 through OPS-5) are resolved. The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
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
