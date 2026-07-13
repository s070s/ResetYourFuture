# Audit: Missing / Not Working As Intended (Synthesis)

| | |
|---|---|
| Finding prefix | GAP |
| Created | 2026-07-11 |
| Scope | Cross-cutting rollup of every Critical/High finding from reports 21–45, plus items that are simply **broken today** regardless of deployment context |
| Delegated | Full evidence, impact detail, and the complete Medium/Low/Info finding sets live in their source reports (21–45) — this document summarizes and points, it does not duplicate |

This report is produced **last**, after all 25 other audits, by design (see [00-INDEX.md](00-INDEX.md)) — it is a synthesis, not an independent pass.

## 1. Methodology

Every finding heading across `21-audit-*.md` … `45-audit-*.md` was scanned for `[Critical]`/`[High]` severity tags. 255 findings were produced across the 25 reports; 31 were Critical or High (1 Critical, 30 High) — fixed findings are removed from this document and struck from the source reports as work lands, so the live count is lower. Each remaining item is summarized below with its source report and one-line impact; **file paths, line numbers, and full recommendations are in the source report** — follow the link, don't re-derive.

A second pass separates them into two buckets:
- **§2 Broken now** — genuinely wrong behavior today, independent of whether this ever gets deployed beyond localhost (crashes, silent failures, unreadable UI, data loss).
- **§3 Will bite before/at real deployment** — correct-ish in the current single-developer, single-machine, demo context, but a blocker the moment the app leaves that context (security, compliance, scalability, availability, config).

This split is judgment, not a formal rule — a few items (e.g. `CFG-1`) sit at the boundary and are cross-listed.

## 2. Summary Scorecard

| Severity | Count (all 25 reports) |
|----------|------------------------|
| Critical | 0 |
| High | 2 |
| Medium | 104 |
| Low | 89 |
| Info | 31 |
| **Total** | **226** |

> **Fixed since audit:** GAP-1 (Critical — admin "Delete User" crashed for any user with chat/call history; sources `DB-1`/`REL-1`/`DQ-1`) — `DeleteUserAsync` now removes dependent chat/call/certificate/enrollment rows in the same transaction as the user, maps `DbUpdateException` → 409, with SQLite FK tests. GAP-4 (High — unreadable submission panel + error banner; sources `UI-1`/`UI-2`) — fixed as part of `13-plan-visual-polish.md`'s WI-6 pass: dropped `bg-light` on `AdminAssessmentSubmissions.razor`'s answers panel, added a dark text color to `#blazor-error-ui`. GAP-8 (High — silent `SelfBaseUrl` misconfiguration; source `CFG-1`) — now fails fast outside Development via `ServiceRegistrationExtensions.ResolveSelfBaseUrl`. `AVAIL-1` (health/readiness endpoints) and `AVAIL-2` (unguarded startup migrate+seed) — `/health/live`+`/health/ready` mapped and the seed path now retries with backoff. GAP-3 (`UX-3`, blank NotFound page) — fixed via `Router.NotFoundPage` + a status-code re-execute dispatcher (the `<NotFound>` render fragment was removed in .NET 10). GAP-5 (`UX-4`, unconfirmed subscription downgrade) — Pricing now reuses Billing's `ConfirmModal`. GAP-7 (`OPS-1`, blog seeder could delete real content) — the delete/reseed branch is gone and the seeder is gated to Development like every other content seed. GAP-2 (`UX-2`, silent assessment failures) — `AssessmentForm` now surfaces load/submit errors and enforces required questions client-side. GAP-6 (`BIZ-1`, subscriptions never expired) — status/tier reads now exclude expired-but-unswept rows, and a `SubscriptionExpirySweeper` background service reverts them to Free and records the transaction. `SCALE-4` (DataProtection keys per-instance) — keys now persist to the shared SQL database via `PersistKeysToDbContext`, with a migration; also surfaced and fixed `DEP-3` (10.0.x package version skew) along the way — a live `dotnet list package --vulnerable` scan (not run at audit time; see DEP report's Methodology) turned up a real, currently-unpatched Critical CVE (CVE-2026-40372, DataProtection cookie/ticket forgery) against the pinned 10.0.5 line, fixed by bumping the whole `10.0.x` family to 10.0.9 in one commit. `SEC-1` (refresh tokens survived password reset, no reuse detection) — added a `SecurityStampAtIssuance` check on every refresh, bulk-revoke on every password-reset path, and chain-wide reuse detection; verified live against the real JWT API (rotation, replay rejection, chain revocation, admin-reset invalidation all confirmed). `TEST-1` (the integration suite ran only on EF InMemory, so relational behaviors were never integration-tested) — added a SQLite-backed `CustomWebAppFactory` variant plus a constraint-sensitive suite that proves the enrollment unique index is enforced relationally (InMemory silently allows the duplicate), and a `MigrationChainTests` pair (always-on model-drift guard + LocalDB-gated full-chain apply). `API-1` (at least five competing error body shapes) — every generic error is now a single RFC 7807 ProblemDetails body: `ToActionResult` routes all `ServiceResult` failures through `ControllerBase.Problem` (with `traceId`), and the scattered `text/plain` strings and anonymous `{ message }`/`{ error }` objects across ~11 controllers were rewritten to `Problem(...)`; downgraded to Medium for the residual `application/json` content-type header on `[Produces]` controllers and three deliberate business-outcome DTOs. `DB-2` (all `DateTimeOffset` columns stored as `nvarchar(48)` for a SQLite test workaround) — the `DateTimeOffsetToStringConverter` is now scoped to the SQLite provider only and a migration restored native `datetimeoffset` on SQL Server (verified converting the seeded dev database without loss); the same change also closed `TEST-5` (workaround no longer dictates production storage) and `PERF-2` (ordered date queries now use the native type). `AVAIL-3` (loopback consumers had no timeout/retry, so a transient blip crashed the Blazor circuit) — consumer `HttpClient`s now carry a 30s timeout and `ApiClientBase` degrades connection failures/timeouts to the empty state instead of propagating them, with idempotent-GET-only retries (verified by `ApiClientResilienceTests`). `UX-1` (~100 user-facing strings hardcoded in English so action feedback ignored the culture, plus `ex.Message` leaks) — swept the shared-component defaults, the literal `ItemLabel` nouns, and every hardcoded success/failure/validation message across the auth, student and admin code-behind onto the `GlobalRes`/`AdminRes`/`ErrorMessagesRes`/`SuccessMessagesRes` pattern (66 new keys, full EL parity); `ex.Message` leaks now show a generic localized message. `UI-3` (clickable course cards nested a `role="button"` upgrade badge inside a `role="button"` card, violating the ARIA content model) — `Courses.razor` now uses the card-link pattern (one real `<a>` in the `<h3>` with an `::after` stretched over the card, the upgrade badge demoted to a plain `<span>`), and `CourseDetail`'s lesson rows got an explicit `aria-label`; verified live with Playwright. `COMP-1` (registration consent pointed at no policy) — added bilingual `/privacy` and `/terms` pages (new `LegalRes` resource set, EN + EL) covering the data inventory, special-category assessment handling, retention, rights and essential-cookie posture; the consent checkbox now links both pages ("By registering, you agree to our Privacy Policy and our Terms of Service") and so does the landing footer; verified live in EN and EL. `COMP-2` (special-category assessment answers stored in plaintext) — `AssessmentSubmission.AnswersJson`/`SummaryJson` are now encrypted at rest via a transparent EF Core value converter over ASP.NET Core Data Protection (reusing the DB-persisted key ring), so plaintext never reaches the database file, backups, or an admin's raw table view; `string`→`string` kept the `nvarchar` column (no migration), reads tolerate legacy plaintext, and two SQLite integration tests plus a clean SQL Server startup confirm it. Details in the source reports' "Fixed since audit" notes.

The former single Critical (`DB-1`) and its two siblings (`REL-1`, `DQ-1`) were the same root cause — admin user deletion — found independently by three different audit passes; that is now fixed. No other finding reached Critical: the suite's overall picture is "a lot of High-value, low-effort fixes" rather than "the app is on fire."

## 3. Broken Now — genuinely wrong behavior today

## 4. Will Bite Before / At Real Deployment

Grouped by theme; each line is `ID (report) — one-liner`. Full evidence/recommendation in the linked report.

**Testing blind spots**
- `TEST-2` (24) — zero end-to-end/browser tests, on an app whose riskiest logic (auth redirect chain, blank-render-on-API-failure, WebRTC) is circuit-only and invisible to unit tests.

**Maintainability cost**
- `MAINT-1` (23) — adding one field to an entity touches 7–9 artifacts across four projects; individually trivial steps, collectively the biggest ongoing maintenance cost, and the silent-`default` consumer behavior turns a missed step into a blank UI value instead of an error.

**Accepted limitations — out of scope, will not implement**

Real findings, now consciously accepted rather than fixed: each only bites when the app runs as more than one instance (which would need Redis or other new infrastructure this single-instance project deliberately avoids), or would require reversing the loopback self-API architecture — whose own verdict is "accept as a documented tradeoff." Each stays documented in its source report's "Accepted since audit" note; they are excluded from the open-finding counts.
- `ARCH-1` (21) — the SSR-calls-its-own-API-over-loopback design; it buys a real, tested, single-authorization API surface. `MAINT-1` (still open) and `PERF-1` (accepted) are its cost centers.
- `PERF-1` (34) — the per-interaction cost of `ARCH-1` (JWT mint, middleware, per-request DB auth lookup, double JSON); only removable by ARCH-1's in-process redesign.
- `SCALE-1` (35) — call/presence state is a process-local singleton; a second instance would need a shared (Redis) registry.
- `SCALE-2` (35) — no SignalR backplane; cross-instance real-time delivery would need a Redis backplane.
- `SCALE-3` (35) — the loopback self-call topology assumes one addressable self (downstream of `ARCH-1`).
- `AVAIL-4` (36) — no failover is possible because no second instance can run correctly; closes with the accepted SCALE cluster.


## 5. Prioritized Action List

Ordered severity-desc, then "fixes the most other findings" first within a tier. The Redis/architecture cluster (`ARCH-1`, `PERF-1`, `SCALE-1`, `SCALE-2`, `SCALE-3`, `AVAIL-4`) has been accepted as out of scope — see §4's "Accepted limitations" block — and is no longer listed here.

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| MAINT-1 | High | L | Adopt mapping helpers for entity→DTO; keep an "adding a field" checklist |
| TEST-2 | High | L | Commit a small Playwright smoke suite (login, one data page, one two-context call) |

## 6. Related Findings Elsewhere

Every ID above links to its full evidence/recommendation in its source report (21–45). The four implementation plans (10–13) were written *before* this synthesis and do not yet incorporate these findings — when scheduling work, note the overlaps:
- `10-plan-sorting-rollout.md` touches the same admin table pages as `UI`/`UX` findings — worth combining passes.
- `11-plan-ollama-agent.md`'s bootstrap workstream (health/readiness state for the Ollama sidecar) is architecturally the same pattern `AVAIL-1` asks for at the app level — consider one health-check subsystem serving both.
