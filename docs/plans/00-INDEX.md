# docs/plans — Plan & Audit Suite Index

> **Temporary folder.** This entire directory is gitignored (see the `docs/plans/` entry in `.gitignore`)
> and exists only until its plans are implemented and its findings addressed. Delete the folder and the
> `.gitignore` entry when done.

Created: 2026-07-11 · Codebase snapshot: commit `b2dd9bd` (master)

## Reading order

Numeric prefix = recommended reading order. Production status is tracked in the last column.

| # | Document | Cluster | Finding prefix | Status |
|---|----------|---------|----------------|--------|
| 00 | [00-INDEX.md](00-INDEX.md) | Meta | — | ✅ |
| 01 | [01-TEMPLATE-plan.md](01-TEMPLATE-plan.md) | Meta | — | ✅ |
| 02 | [02-TEMPLATE-audit.md](02-TEMPLATE-audit.md) | Meta | — | ✅ |
| 10 | ~~10-plan-sorting-rollout.md~~ | Plans | — | 🏁 implemented 2026-07-12, file removed |
| 11 | ~~11-plan-ollama-agent.md~~ | Plans | — | 🏁 implemented 2026-07-13, file removed |
| 12 | ~~12-plan-five-features.md~~ | Plans | — | 🏁 implemented 2026-07-13, file removed |
| 13 | [13-plan-visual-polish.md](13-plan-visual-polish.md) | Plans | — | 🔶 core implemented 2026-07-13; mechanical sweep + 2 micro-interactions deferred |
| 20 | [20-audit-gaps.md](20-audit-gaps.md) — missing / not working as intended | Synthesis | GAP | ✅ |
| 21 | [21-audit-architecture.md](21-audit-architecture.md) | Foundation | ARCH | ✅ |
| 22 | [22-audit-code-quality.md](22-audit-code-quality.md) | Foundation | CQ | ✅ |
| 23 | [23-audit-maintainability.md](23-audit-maintainability.md) | Foundation | MAINT | ✅ |
| 24 | [24-audit-testability.md](24-audit-testability.md) | Foundation | TEST | ✅ |
| 25 | [25-audit-security.md](25-audit-security.md) | Security & correctness | SEC | ✅ |
| 26 | [26-audit-reliability.md](26-audit-reliability.md) | Security & correctness | REL | ✅ |
| 27 | [27-audit-business-logic.md](27-audit-business-logic.md) | Security & correctness | BIZ | ✅ |
| 28 | [28-audit-data-quality.md](28-audit-data-quality.md) | Security & correctness | DQ | ✅ |
| 29 | [29-audit-compliance.md](29-audit-compliance.md) | Security & correctness | COMP | ✅ |
| 30 | [30-audit-database.md](30-audit-database.md) | Data & API | DB | ✅ |
| 31 | [31-audit-api.md](31-audit-api.md) | Data & API | API | ✅ |
| 32 | [32-audit-user-interface.md](32-audit-user-interface.md) | Frontend | UI | ✅ |
| 33 | [33-audit-ux.md](33-audit-ux.md) | Frontend | UX | ✅ |
| 34 | [34-audit-performance.md](34-audit-performance.md) | Runtime | PERF | ✅ |
| 35 | [35-audit-scalability.md](35-audit-scalability.md) | Runtime | SCALE | ✅ |
| 36 | [36-audit-availability.md](36-audit-availability.md) | Runtime | AVAIL | ✅ |
| 37 | [37-audit-logging-monitoring.md](37-audit-logging-monitoring.md) | Operations | LOG | ✅ |
| 38 | [38-audit-observability.md](38-audit-observability.md) | Operations | OBS | ✅ |
| 39 | [39-audit-configuration.md](39-audit-configuration.md) | Operations | CFG | ✅ |
| 40 | [40-audit-build-deployment.md](40-audit-build-deployment.md) | Operations | BUILD | ✅ |
| 41 | [41-audit-cloud-infrastructure.md](41-audit-cloud-infrastructure.md) | Operations | CLOUD | ✅ |
| 42 | [42-audit-operational-readiness.md](42-audit-operational-readiness.md) | Operations | OPS | ✅ |
| 43 | [43-audit-dependency-management.md](43-audit-dependency-management.md) | Governance | DEP | ✅ |
| 44 | [44-audit-documentation.md](44-audit-documentation.md) | Governance | DOC | ✅ |
| 45 | [45-audit-project-governance.md](45-audit-project-governance.md) | Governance | GOV | ✅ |

Production order differs from reading order: plans (10–13) were written first from codebase exploration,
then audits 21–45 in seven grouped analysis passes (A: 21–24, B: 25–29, C: 30–31, D: 32–33, E: 34–36,
F: 37–42, G: 43–45), and 20-audit-gaps.md last as a synthesis of all Critical/High findings.

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
| ARCH (21) | 0 | 1 | 4 | 3 | 2 | 10 |
| CQ (22) | 0 | 0 | 3 | 5 | 2 | 10 |
| MAINT (23) | 0 | 1 | 4 | 4 | 1 | 10 |
| TEST (24) | 0 | 1 | 2 | 4 | 1 | 8 |
| SEC (25) | 0 | 0 | 4 | 4 | 1 | 9 |
| REL (26) | 0 | 0 | 4 | 4 | 0 | 8 |
| BIZ (27) | 0 | 0 | 3 | 4 | 0 | 7 |
| DQ (28) | 0 | 0 | 4 | 3 | 1 | 8 |
| COMP (29) | 0 | 2 | 3 | 2 | 1 | 8 |
| DB (30) | 0 | 0 | 6 | 4 | 2 | 12 |
| API (31) | 0 | 0 | 9 | 4 | 3 | 16 |
| UI (32) | 0 | 1 | 7 | 4 | 1 | 13 |
| UX (33) | 0 | 1 | 9 | 2 | 1 | 13 |
| PERF (34) | 0 | 1 | 5 | 5 | 1 | 12 |
| SCALE (35) | 0 | 3 | 6 | 2 | 1 | 12 |
| AVAIL (36) | 0 | 1 | 4 | 2 | 1 | 8 |
| LOG (37) | 0 | 0 | 4 | 5 | 0 | 9 |
| OBS (38) | 0 | 0 | 3 | 3 | 1 | 7 |
| CFG (39) | 0 | 0 | 4 | 3 | 1 | 8 |
| BUILD (40) | 0 | 0 | 2 | 5 | 1 | 8 |
| CLOUD (41) | 0 | 0 | 2 | 4 | 1 | 7 |
| OPS (42) | 0 | 0 | 4 | 2 | 1 | 7 |
| DEP (43) | 0 | 0 | 3 | 4 | 2 | 9 |
| DOC (44) | 0 | 0 | 3 | 4 | 2 | 9 |
| GOV (45) | 0 | 0 | 2 | 3 | 3 | 8 |
| **Total** | **0** | **12** | **104** | **89** | **31** | **236** |

Counts reflect **open** findings — fixed findings are removed from their report, and downgraded findings (still open, less severe) move to their new severity row (each report carries a "Fixed since audit" note either way). The original single Critical (`DB-1`) and two High siblings (`REL-1`, `DQ-1`) — admin user deletion — are fixed, and so are the two High UI findings (`UI-1`, `UI-2`, dark-theme contrast) and ten High findings from Phase 5 (`CFG-1` fail-fast SelfBaseUrl, `AVAIL-1` health endpoints, `AVAIL-2` bounded startup retry, `UX-3` blank NotFound page, `UX-4` unconfirmed subscription downgrade, `OPS-1` destructive blog reseed, `UX-2` silent assessment failures, `BIZ-1` subscriptions never expiring, `SEC-1` refresh-token lifecycle/reuse detection, `TEST-1` relational integration coverage via a SQLite-backed factory + migration-chain test, `DB-2` native `datetimeoffset` storage — which also closed `TEST-5` and `PERF-2`, `AVAIL-3` loopback timeout/retry + graceful circuit degradation). `SCALE-4` (DataProtection keys) is partially fixed and downgraded to Medium; `API-1` (error-body sprawl) is mostly fixed — all generic errors now share one ProblemDetails envelope — and downgraded to Medium for the residual content-type header and deliberate typed DTOs; `DEP-3` (`10.0.x` version skew) is partially fixed and downgraded to Low — fixing it surfaced and patched a real Critical CVE (CVE-2026-40372) along the way. See [20-audit-gaps.md](20-audit-gaps.md) §3 for the synthesis and the prioritized action list across the remaining Critical/High findings.
