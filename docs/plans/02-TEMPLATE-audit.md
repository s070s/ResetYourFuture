# Template: Audit Report

Copy this structure for every `NN-audit-*.md` document. Severity/effort scales and the
primary-home rule are defined in [00-INDEX.md](00-INDEX.md).

---

# Audit: <Area>

| | |
|---|---|
| Finding prefix | XXX |
| Created | YYYY-MM-DD |
| Scope | what this report covers |
| Delegated | topics owned by other reports per the primary-home rule, with report numbers |

## 1. Methodology

What was examined (files, searches, config, git history) — and what was NOT examined, and why.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | |
| High | |
| Medium | |
| Low | |
| Info | |

One-paragraph overall assessment.

## 3. Findings

Ordered by severity, descending. Heading format is load-bearing (used for automated ID checks):

### XXX-1: <title>  [Severity] [Effort: S/M/L]
- **Evidence:** file paths (+ line refs where pinpointed)
- **Impact:** what goes wrong / what it costs
- **Recommendation:** concrete fix; reference an existing repo pattern where one exists

(repeat)

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|

Sorted severity descending, then effort ascending.

## 5. Related Findings Elsewhere

Bullet list of finding IDs in other reports that touch this area, each with a one-line note.
