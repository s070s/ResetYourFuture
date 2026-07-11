# Template: Implementation Plan

Copy this structure for every `1x-plan-*.md` document.

---

# Plan: <Title>

| | |
|---|---|
| Status | Draft / Approved / In progress / Done |
| Created | YYYY-MM-DD |
| Depends on | <document numbers / none> |
| Related audits | <finding IDs, e.g. PERF-2, UX-4> |

## 1. Context & Goals

2–5 bullets: why this work exists and what "done" observably means.

## 2. Current State

What exists today, with repo-relative file paths. Name the pattern files explicitly
(the files a work item copies or extends).

## 3. Design Decisions

| # | Decision | Alternatives rejected | Rationale |
|---|----------|-----------------------|-----------|

## 4. Work Items

### WI-1: <name>
- **Files:** paths created / modified
- **Change:** precise description — signatures, config keys, entity fields where load-bearing
- **Acceptance criteria:** observable checks

(repeat; keep each work item independently completable)

## 5. Implementation Order & Dependencies

Ordered list. Call out which items are parallelizable and which are structural prerequisites.

## 6. Verification

Build/test commands, manual test script (**both EN and EL** where UI-facing), regression checks.

## 7. Out of Scope

Explicit non-goals to prevent creep.
