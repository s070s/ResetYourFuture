# docs/plans — Open Audit Backlog Index

Created: 2026-07-11 · Codebase snapshot: commit `b2dd9bd` (master) · Pruned to open findings: 2026-07-14

This folder holds the **open backlog** from the 2026-07-11 audit suite (originally 4 implementation plans,
25 audit reports, a Critical/High synthesis, and 255 findings). Everything completed has been removed:
all four plans are implemented, and every Critical/High finding is fixed, downgraded after a partial fix,
or consciously accepted as out of scope (the four "Accepted since audit" banners in reports 21/34/35/36
record the accepted Redis/architecture cluster: `ARCH-1`, `PERF-1`, `SCALE-1/2/3`, `AVAIL-4`). What remains
is the Medium/Low/Info backlog below — "worth scheduling" / "fix opportunistically" / "observation only" —
plus the deferred cosmetic polish note. Full history of what was fixed lives in git (`git log docs/plans/`).

## Reports

| # | Document | Cluster | Finding prefix |
|---|----------|---------|----------------|
| 21 | [21-audit-architecture.md](21-audit-architecture.md) | Foundation | ARCH |
| 22 | [22-audit-code-quality.md](22-audit-code-quality.md) | Foundation | CQ |
| 23 | [23-audit-maintainability.md](23-audit-maintainability.md) | Foundation | MAINT |
| 24 | [24-audit-testability.md](24-audit-testability.md) | Foundation | TEST |
| 25 | [25-audit-security.md](25-audit-security.md) | Security & correctness | SEC |
| 26 | [26-audit-reliability.md](26-audit-reliability.md) | Security & correctness | REL |
| 27 | [27-audit-business-logic.md](27-audit-business-logic.md) | Security & correctness | BIZ |
| 28 | [28-audit-data-quality.md](28-audit-data-quality.md) | Security & correctness | DQ |
| 29 | [29-audit-compliance.md](29-audit-compliance.md) | Security & correctness | COMP |
| 30 | [30-audit-database.md](30-audit-database.md) | Data & API | DB |
| 31 | [31-audit-api.md](31-audit-api.md) | Data & API | API |
| 32 | [32-audit-user-interface.md](32-audit-user-interface.md) | Frontend | UI |
| 33 | [33-audit-ux.md](33-audit-ux.md) | Frontend | UX |
| 34 | [34-audit-performance.md](34-audit-performance.md) | Runtime | PERF |
| 35 | [35-audit-scalability.md](35-audit-scalability.md) | Runtime | SCALE |
| 36 | [36-audit-availability.md](36-audit-availability.md) | Runtime | AVAIL |
| 37 | [37-audit-logging-monitoring.md](37-audit-logging-monitoring.md) | Operations | LOG |
| 38 | [38-audit-observability.md](38-audit-observability.md) | Operations | OBS |
| 39 | [39-audit-configuration.md](39-audit-configuration.md) | Operations | CFG |
| 40 | [40-audit-build-deployment.md](40-audit-build-deployment.md) | Operations | BUILD |
| 41 | [41-audit-cloud-infrastructure.md](41-audit-cloud-infrastructure.md) | Operations | CLOUD |
| 42 | [42-audit-operational-readiness.md](42-audit-operational-readiness.md) | Operations | OPS |
| 43 | [43-audit-dependency-management.md](43-audit-dependency-management.md) | Governance | DEP |
| 44 | [44-audit-documentation.md](44-audit-documentation.md) | Governance | DOC |
| 45 | [45-audit-project-governance.md](45-audit-project-governance.md) | Governance | GOV |

> **Plan 13 — deferred cosmetic polish (optional, not started).** The visual-polish plan's core shipped
> (design-token system, global `:focus-visible` ring, button/table-row micro-interactions, scroll-reveal,
> `SkeletonBlock` on 8 representative pages, the `prefers-reduced-motion` kill-switch, tab-hidden animation
> pausing) and it also absorbed UI-1/UI-2. The plan file was removed as done; these optional items were
> consciously deferred to keep the phase bounded and are the only outstanding polish — zero-risk, pick up any time:
> - Mechanical sweep of hardcoded px/ms/hex literals → design tokens across the ~28 remaining `.razor.css`
>   files (plus `shared-components.css`); do it file-by-file with a visual diff.
> - Two micro-interactions: card hover (`translateY(-2px)` + shadow crossfade) and nav-link underline scale-in.
> - `SkeletonBlock` swap on the ~14 list pages still using `LoadingSpinner` (spinner is correct for sub-second
>   loads per the plan's Decision 5, but several are paged lists that would benefit); grep `LoadingSpinner` under `Pages/`.
> - Formal Lighthouse scoring + DevTools paint-flashing / FPS traces (never run; the transform/opacity-only
>   performance rule was instead verified by reading every animated CSS rule).

## Severity scale (audits)

| Severity | Meaning |
|----------|---------|
| Critical | Broken or exploitable **now**, in the app's actual (dev/demo) usage |
| High | Will bite before or immediately upon any real deployment |
| Medium | Ongoing friction / technical debt; worth scheduling |
| Low | Polish; fix opportunistically |
| Info | Observation, no action required |

Severity is judged in context: this is a university certificate project, not a production system
(e.g. "no CD pipeline" cannot be Critical here).

## Effort scale

| Effort | Meaning |
|--------|---------|
| S | Under ~2 hours |
| M | Half a day to 2 days |
| L | More than 2 days |

## Finding IDs and cross-referencing

- Every audit finding has a stable ID: `<PREFIX>-<n>` (e.g. `SEC-3`), heading format
  `### SEC-3: Title  [High] [Effort: S]`. Prefixes are assigned in the table above and are unique per report.
- **Primary-home rule:** each finding lives in exactly **one** report — the one whose kind of fix resolves it.
  Other reports reference it by ID with a one-line note, never duplicating the body.
  Every report ends with a "Related findings elsewhere" section.

| Overlap | Split |
|---------|-------|
| Logging & Monitoring vs Observability | LOG = current state of the custom file logger (coverage, quality, rotation, alerting gaps). OBS = forward-looking instrumentation (OTel, health checks, correlation IDs, metrics, dashboards). |
| Performance vs Scalability vs Availability | Fixed by code optimization → PERF. Blocker to >1 instance / user growth → SCALE. Failure modes, uptime, recovery → AVAIL. |
| Code Quality vs Maintainability | Micro (naming, duplication, dead code, warnings) → CQ. Macro (coupling, layering, change cost, onboarding) → MAINT. |
| UI vs UX | Component-level visual correctness / consistency / a11y → UI. Flow-level (navigation, feedback, empty states, bilingual experience) → UX. |
| Security vs Compliance | Technical vulnerabilities → SEC. Regulatory posture (GDPR, retention, consent) → COMP. |
| Database vs Data Quality | Schema / migrations / indexes / EF usage → DB. Constraint gaps, validation holes, seed & orphan integrity → DQ. |

## Finding counts

| Report | Critical | High | Medium | Low | Info | Total |
|--------|----------|------|--------|-----|------|-------|
| ARCH (21) | 0 | 0 | 0 | 3 | 2 | 5 |
| CQ (22) | 0 | 0 | 3 | 5 | 2 | 10 |
| MAINT (23) | 0 | 0 | 4 | 4 | 1 | 9 |
| TEST (24) | 0 | 0 | 2 | 4 | 1 | 7 |
| SEC (25) | 0 | 0 | 4 | 4 | 1 | 9 |
| REL (26) | 0 | 0 | 4 | 4 | 0 | 8 |
| BIZ (27) | 0 | 0 | 3 | 4 | 0 | 7 |
| DQ (28) | 0 | 0 | 4 | 3 | 1 | 8 |
| COMP (29) | 0 | 0 | 3 | 2 | 1 | 6 |
| DB (30) | 0 | 0 | 6 | 4 | 2 | 12 |
| API (31) | 0 | 0 | 9 | 4 | 3 | 16 |
| UI (32) | 0 | 0 | 6 | 4 | 1 | 11 |
| UX (33) | 0 | 0 | 9 | 2 | 1 | 12 |
| PERF (34) | 0 | 0 | 5 | 5 | 1 | 11 |
| SCALE (35) | 0 | 0 | 6 | 2 | 1 | 9 |
| AVAIL (36) | 0 | 0 | 4 | 2 | 1 | 7 |
| LOG (37) | 0 | 0 | 4 | 5 | 0 | 9 |
| OBS (38) | 0 | 0 | 3 | 3 | 1 | 7 |
| CFG (39) | 0 | 0 | 4 | 3 | 1 | 8 |
| BUILD (40) | 0 | 0 | 2 | 5 | 1 | 8 |
| CLOUD (41) | 0 | 0 | 2 | 4 | 1 | 7 |
| OPS (42) | 0 | 0 | 4 | 2 | 1 | 7 |
| DEP (43) | 0 | 0 | 3 | 4 | 2 | 9 |
| DOC (44) | 0 | 0 | 3 | 4 | 2 | 9 |
| GOV (45) | 0 | 0 | 2 | 3 | 3 | 8 |
| **Total** | **0** | **0** | **99** | **89** | **31** | **219** |

Counts reflect **open** findings only — fixed findings were removed from their reports as the work landed, and downgraded findings (still open, less severe) sit in their new severity row. Of the original 255 findings, all 31 Critical/High are resolved: fixed, downgraded after a partial fix, or consciously accepted (the Redis/architecture cluster — see the "Accepted since audit" banners in reports 21/34/35/36). The record of every fix lives in git history.
