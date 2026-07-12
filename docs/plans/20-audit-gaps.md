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
| High | 28 |
| Medium | 105 |
| Low | 88 |
| Info | 31 |
| **Total** | **252** |

> **Fixed since audit:** GAP-1 (Critical — admin "Delete User" crashed for any user with chat/call history; sources `DB-1`/`REL-1`/`DQ-1`) — `DeleteUserAsync` now removes dependent chat/call/certificate/enrollment rows in the same transaction as the user, maps `DbUpdateException` → 409, with SQLite FK tests. Details in the source reports' "Fixed since audit" notes.

The former single Critical (`DB-1`) and its two siblings (`REL-1`, `DQ-1`) were the same root cause — admin user deletion — found independently by three different audit passes; that is now fixed. No other finding reached Critical: the suite's overall picture is "a lot of High-value, low-effort fixes" rather than "the app is on fire."

## 3. Broken Now — genuinely wrong behavior today

### GAP-2: Assessment submission can fail with zero feedback, and required questions aren't enforced [High]
- **Source:** `UX-2` (33-audit-ux.md)
- A failed submission (network blip, 403, server error) just re-enables the button with no error shown; "required" questions can be submitted empty. A student cannot tell whether their work was recorded.

### GAP-3: Any mistyped or dead URL renders a fully blank page [High]
- **Source:** `UX-3` (33-audit-ux.md)
- `Routes.razor`'s `<NotFound>` has no layout, no message, no navigation — indistinguishable from a crash, and the page title still says "Home Page".

### GAP-4: Admin's submission-review panel is unreadable; the circuit-crash banner is unreadable [High]
- **Sources:** `UI-1`, `UI-2` (32-audit-user-interface.md)
- Two separate dark-theme contrast breaks: a Bootstrap `.bg-light` panel makes assessment answers ~1.9:1 contrast in `AdminAssessmentSubmissions.razor`, and `#blazor-error-ui` (the "an unhandled error occurred" banner — precisely the moment a user needs to read it) is near-white text on yellow, ~1.2:1.

### GAP-5: One misclick cancels a paid subscription [High]
- **Source:** `UX-4` (33-audit-ux.md)
- `Pricing.razor`'s "Downgrade to Free" calls `CancelSubscription` directly with no confirmation — the equivalent button on the Billing page already has a proper `ConfirmModal`; Pricing was never brought up to the same standard.

### GAP-6: Paid subscriptions never actually expire [High]
- **Source:** `BIZ-1` (27-audit-business-logic.md)
- `ExpiresAt` is computed at purchase time but never checked on read and never swept by a background job — once `IsActive=true`, access is permanent regardless of the term the user paid for.

### GAP-7: The blog seeder can silently delete an operator's real articles on restart [High]
- **Source:** `OPS-1` (42-audit-operational-readiness.md)
- Unlike every other content seeder (gated to Development), the blog seeder runs unconditionally and, if no article has a Greek title, wipes the table and reseeds demo content — a heuristic left over from a bilingual migration that can't distinguish "legacy seed data" from "real English-only content someone just wrote."

### GAP-8: Wrong `SelfBaseUrl` (or any host misconfiguration) renders every page empty with no error [High]
- **Source:** `CFG-1` (39-audit-configuration.md)
- Because SSR calls its own API over loopback HTTP (the `ARCH-1` architecture), a bad `SelfBaseUrl` doesn't throw — it silently falls back to `localhost` and every data-backed page just shows nothing. Straddles "broken now" (it's a bad failure mode today) and "will bite at deployment" (it only manifests once `SelfBaseUrl` needs to be something other than the default).

## 4. Will Bite Before / At Real Deployment

Grouped by theme; each line is `ID (report) — one-liner`. Full evidence/recommendation in the linked report.

**Refresh-token & session lifecycle**
- `SEC-1` (25) — refresh tokens survive password reset / security-stamp rotation and have no reuse detection; a stolen token outlives the "fix."

**Testing blind spots**
- `TEST-1` (24) — the entire integration suite runs on EF InMemory, so relational behaviors (constraints, migrations, provider-specific translation) are never actually tested.
- `TEST-2` (24) — zero end-to-end/browser tests, on an app whose riskiest logic (auth redirect chain, blank-render-on-API-failure, WebRTC) is circuit-only and invisible to unit tests.

**API error-shape inconsistency**
- `API-1` (31) — error responses have at least five competing body shapes and two content types; no client can write one error handler.

**Localization gaps in dynamic feedback**
- `UX-1` (33) — ~100 user-facing strings (toasts, validation, confirmations) are hardcoded in English even though the app is otherwise fully bilingual; raw exception text leaks to users in places.

**Compliance**
- `COMP-1` (29) — no privacy policy or terms page; the registration consent checkbox has nothing to link to, so consent isn't informed.
- `COMP-2` (29) — psychosocial assessment answers (likely GDPR Art. 9 special-category data) are stored in plaintext with no special handling.

**Architecture / maintainability cost**
- `ARCH-1` (21) — the SSR-calls-its-own-API-over-loopback design is the root cause behind several other findings (`PERF-1`, `SCALE-3`, `CFG-1`); the tradeoff is real but its costs are systemic.
- `MAINT-1` (23) — adding one field to an entity touches 7–9 artifacts across four projects; individually trivial steps, collectively the biggest ongoing maintenance cost, and the silent-`default` consumer behavior turns a missed step into a blank UI value instead of an error.

**Runtime — performance**
- `PERF-1` (34) — every data fetch pays the full loopback HTTP pipeline (JWT mint, middleware, a per-request DB auth lookup, double JSON serialization) — the per-interaction cost of `ARCH-1`.

**Runtime — scalability (all four are the same underlying constraint: this app cannot run as more than one instance)**
- `SCALE-1` (35) — call/presence state is a process-local singleton; a second instance splits reality in two.
- `SCALE-2` (35) — no SignalR backplane; cross-instance real-time delivery silently drops.
- `SCALE-3` (35) — the loopback self-call topology assumes one addressable self.
- `SCALE-4` (35) — DataProtection keys are per-instance (filesystem + DPAPI); auth cookies/tickets don't survive a second node.

**Runtime — availability**
- `AVAIL-1` (36) — no health/readiness endpoints anywhere.
- `AVAIL-2` (36) — startup migrate+seed has no guard; an unreachable DB crashes the process before it binds a port.
- `AVAIL-3` (36) — loopback consumers have no timeout/retry; a transient network blip crashes the Blazor circuit.
- `AVAIL-4` (36) — direct consequence of `SCALE-1/2/4`: no failover is possible because no second instance can run correctly.

**Data / schema**
- `DB-2` (30) — every `DateTimeOffset` column is stored as `nvarchar(48)` (a SQLite-test workaround that leaked into the production schema), degrading sort/range-scan performance and index usefulness (`PERF-2` is the performance-side consequence).

## 5. Prioritized Action List

Ordered severity-desc, then "fixes the most other findings" first within a tier.

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| CFG-1 | High | S | Fail fast on bad `SelfBaseUrl` outside Development, matching the existing `Jwt:Key` fail-fast pattern |
| AVAIL-2 | High | S | Bound-retry the startup migrate+seed instead of a hard crash |
| AVAIL-1 | High | S | Add liveness/readiness health-check endpoints |
| UX-3 | High | S | Give `<NotFound>` a real layout and message |
| UX-4 | High | S | Reuse Billing's `ConfirmModal` on the Pricing downgrade button |
| UI-1 | High | S | Drop `bg-light` on the submission-answers panel |
| UI-2 | High | S | Add a dark text color to `#blazor-error-ui` |
| SCALE-4 | High | S | Move DataProtection keys off local filesystem/DPAPI before any multi-instance target |
| OPS-1 | High | S | Remove the blog seeder's delete/reseed branch; gate it like every other content seeder |
| UX-2 | High | M | Surface submission errors; enforce required questions client-side |
| BIZ-1 | High | M | Enforce `ExpiresAt` on read + add an expiry sweep job |
| SEC-1 | High | M | Reject refresh on security-stamp mismatch; bulk-revoke on password reset; add reuse detection |
| TEST-1 | High | M | Add a SQLite-backed `CustomWebAppFactory` variant for constraint-sensitive suites |
| API-1 | High | M | Make `ProblemDetails` the single error envelope via `ServiceResultExtensions` |
| DB-2 | High | M | Fix the `DateTimeOffset` → `nvarchar` conversion; migrate to a real datetime column type |
| AVAIL-3 | High | M | Add timeout + retry policy to loopback `HttpClient`s |
| SCALE-2 | High | M | Add a SignalR backplane (Redis) alongside `SCALE-1`'s shared registry |
| COMP-1 | High | M | Publish bilingual Privacy Policy / Terms and link them from consent |
| UX-1 | High | M | Sweep hardcoded English strings into the existing `ErrorMessagesRes`/`SuccessMessagesRes` pattern |
| UI-3 | High | M | Restructure course cards off nested `role="button"` |
| ARCH-1 | High | L | Accept as a documented tradeoff for now; `MAINT-1`/`PERF-1`/`SCALE-3` are its cost centers if revisited |
| MAINT-1 | High | L | Adopt mapping helpers for entity→DTO; keep an "adding a field" checklist |
| TEST-2 | High | L | Commit a small Playwright smoke suite (login, one data page, one two-context call) |
| COMP-2 | High | L | Classify assessment answers as special-category; encrypt at rest; restrict/audit admin access |
| SCALE-1 | High | L | Re-back `CallRegistry`/presence with a shared store (Redis) |
| SCALE-3 | High | L | Remove the loopback self-call topology (downstream of `ARCH-1`) |
| AVAIL-4 | High | L | Consequence of `SCALE-1/2/4` — no separate fix; closes when those do |

## 6. Related Findings Elsewhere

Every ID above links to its full evidence/recommendation in its source report (21–45). The four implementation plans (10–13) were written *before* this synthesis and do not yet incorporate these findings — when scheduling work, note the overlaps:
- `10-plan-sorting-rollout.md` touches the same admin table pages as `UI`/`UX` findings — worth combining passes.
- `11-plan-ollama-agent.md`'s bootstrap workstream (health/readiness state for the Ollama sidecar) is architecturally the same pattern `AVAIL-1` asks for at the app level — consider one health-check subsystem serving both.
- `13-plan-visual-polish.md` should absorb `UI-1`/`UI-2` (contrast fixes) rather than treating them as separate work.
