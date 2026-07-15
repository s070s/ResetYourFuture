# Audit: Project Governance

| | |
|---|---|
| Finding prefix | GOV |
| Created | 2026-07-11 |
| Scope | Git history and commit conventions, branching and merge workflow, review evidence, versioning/tagging/releases, repo health files (templates, CODEOWNERS, CONTRIBUTING), style/analyzer enforcement in the build, CI-as-gate discipline. Calibrated to a solo university certificate project — enterprise ceremony (mandatory reviews, CODEOWNERS, signed commits) is deliberately *not* demanded. |
| Delegated | CI pipeline contents and build hardening → BUILD (40). Automated dependency-update PRs (Dependabot) → DEP (43). CHANGELOG and CONTRIBUTING as documents → DOC (44). Micro-level style-drift instances in code → CQ (22). Test coverage gating → TEST (24). |

## 1. Methodology

Ran read-only git commands: `git log --oneline` (253 commits, 2025-12-26 → 2026-07-10), `git log --format="%an|%ae" | sort -u` (author identities), `git branch -a` (branches), `git tag` (empty), `git log --merges` (merge style), greps of subjects for conventional-commit prefixes, reverts, review-fix commits, and `Co-authored-by` trailers, plus `git log --follow` on `.github/workflows/tests.yml`. Read `.editorconfig`, both `Directory.Build.props` files, all csproj files (searched for `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel`, `Version` — none present), `.gitignore`, and `.github/` (contains only `workflows/tests.yml`; no templates, CODEOWNERS, or dependabot config).

NOT examined: GitHub server-side settings — branch protection rules, required checks, and whether the historical `copilot-swe-agent[bot]` commits arrived via PRs cannot be determined from a local clone (no `Merge pull request` commits exist in master's history, which is evidence but not proof). Noted explicitly in GOV-4.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 3 |
| Info | 3 |

Governance here is better than the checklist suggests. The headline absences — no PRs, no tags, no templates, no CODEOWNERS — are the *expected* shape of a solo certificate project and are graded accordingly. What stands out instead is an unusually disciplined working method: features are executed from written work-package plans with per-WP commits ("Add CallHub, CallRegistry, and ring-monitor background service (WP3)"), review findings are recorded and fixed as dedicated commits ("Emit CallAccepted event on call acceptance (WP3 review fix)"), plan docs are deleted on completion, CI runs the full test suite on every push, and local tooling config was deliberately untracked. Both Medium findings are now resolved: the build stamps a `VersionPrefix` (0.9.0) so the artifact is identifiable, to be tagged per milestone (GOV-1); and the `.editorconfig` is no longer advisory — `EnforceCodeStyleInBuild` plus a CI `dotnet format style --verify-no-changes` gate now fail the build on style drift, the exact condition under which the previous drift happened (GOV-2). What remains is three Low items and three Info.

## 3. Findings

> The two Medium findings are resolved (see git: `Fix GOV-1 and GOV-2`). **GOV-1** — `Directory.Build.props` now sets `<VersionPrefix>0.9.0</VersionPrefix>`, so every built assembly carries an identity; the accompanying comment establishes the convention (bump alongside a matching git tag per milestone, cut `v1.0.0` at submission). The submission tag itself is a release action left to the author (pushing tags is out of an automated pass's scope); the CHANGELOG stays deferred to DOC-8. **GOV-2** — the `.editorconfig` is now enforced, not advisory: `EnforceCodeStyleInBuild` + `AnalysisLevel=latest-recommended` run style (IDExxxx) and the recommended analyzers in every build, the lone block-scoped namespace was converted to file-scoped, scaffolded EF migrations are marked `generated_code` so their style/analyzer noise is skipped, test method names are exempted from CA1707 (underscores are the deliberate xUnit convention, ~1500 false positives removed), and a CI **Verify code style** step (`dotnet format style --verify-no-changes --severity warn`) now fails the build on style drift. The gate is scoped to the currently-clean warning-level style rules; escalating the remaining analyzer warnings to errors and the whitespace/paren reformatting are consciously deferred (the finding's own staged "once warning-clean" step). The remaining open items are three Low and three Info.

### GOV-3: Commit-message conventions are inconsistent, though recent history is strong  [Low] [Effort: S]
- **Evidence:** 253 commits; 9 use conventional-commit prefixes (`feat:`, `fix(tests):` — e.g. `e78bd1b`, `c53909b`), the rest freeform. Early history includes non-descriptive subjects ("updated styles", "change on palette", "-url in a button" and its revert `8513eb5`, revert/reapply pairs `93c7ed8`/`1c82f42`). Recent history (video-call and assistant eras) is consistently imperative, scoped, and WP-tagged ("Add assistant chat/status/reindex API endpoints (WP5)"). No convention is documented anywhere.
- **Impact:** Low and shrinking — the current de-facto convention (imperative subject + WP/context suffix) is good; the cost is only that it lives in nobody's head but the author's, and mixed tooling (bot/agent commits) won't converge on it unprompted.
- **Recommendation:** Write the convention down in one paragraph (README dev section or CONTRIBUTING stub, see GOV-6): imperative mood, why-not-what body when nontrivial, `(WPn)`/`(review fix)` suffixes as practiced. Retro-fitting conventional-commits across history is not worth it.

### GOV-4: No PR-based merge gate; branch protection unverifiable locally  [Low] [Effort: S]
- **Evidence:** Zero `Merge pull request` commits in master's history. Feature work does use branches — merge commits `a1a53e0` (feature/ai-assistant), `d708023` (feature/categories), `5ae9477` (feature/admin-users-tier-column) — but merges are performed locally and branches deleted (only `master` survives locally and on origin). CI triggers on `push` and `pull_request` (`tests.yml:3-5`), so the PR path is wired but unused; on direct pushes CI runs *after* the code is already on master. Branch protection / required checks are GitHub server-side settings that cannot be read from the clone: **status unknown**.
- **Impact:** For one developer, mandatory PRs are ceremony; the real costs are (a) nothing prevents pushing to master with a red build — CI is a notifier, not a gate; and (b) review evidence (which demonstrably exists — "WP3 review fix" commits, `Co-Authored-By: Claude` trailers) is only recoverable by commit archaeology rather than attached to a reviewable unit.
- **Recommendation:** Right-sized: enable branch protection on `master` requiring the `tests` check (works with direct pushes via `--force-with-lease`-free flow, or flip to merging the existing feature branches through PRs — zero extra work beyond opening them). Templates/mandatory reviews remain unnecessary (GOV-6).

### GOV-5: Four author identities for one developer muddy history attribution  [Low] [Effort: S]
- **Evidence:** `git log --format="%an|%ae" | sort -u`: `George Sotiropoulos`, `s070s`, and `Γιώργος Σωτηρόπουλος` — all `gsotiro@hotmail.com` — plus `copilot-swe-agent[bot]`. Co-author trailers add a fourth human alias (`s070s <23123850+s070s@users.noreply.github.com>`).
- **Impact:** Cosmetic but persistent: `git shortlog`/blame statistics fragment, and GitHub contribution attribution depends on which email/name pair a commit carries. History rewriting to fix it is not worth the churn.
- **Recommendation:** Set `git config --global user.name`/`user.email` once to the preferred identity for all future commits, and add a 4-line `.mailmap` so `shortlog`/`blame -e` consolidate the existing aliases.

### GOV-6: No repo health files (CONTRIBUTING, issue/PR templates, CODEOWNERS, SECURITY.md) — acceptable, but record the decision  [Info] [Effort: S]
- **Evidence:** `.github/` contains only `workflows/tests.yml`; no CONTRIBUTING, CODE_OF_CONDUCT, ISSUE_TEMPLATE/, PULL_REQUEST_TEMPLATE, CODEOWNERS, or SECURITY.md anywhere.
- **Impact:** None today — there are no external contributors, CODEOWNERS is meaningless with one owner, and issue templates gate a queue that doesn't exist. Flagged so the absence reads as a scoped decision rather than an oversight, and because GOV-3's convention paragraph needs *somewhere* to live if the README shouldn't grow further.
- **Recommendation:** No action for the certificate timeline. If the repo ever solicits contributions, start with CONTRIBUTING.md (setup + conventions) only.

### GOV-7: No .gitattributes — line-ending normalization depends on each clone's autocrlf  [Info] [Effort: S]
- **Evidence:** No `.gitattributes` in the repo. `.editorconfig:7-10` documents a deliberate decision to leave line endings and charset unenforced because the repo has pre-existing mixed CRLF/LF files and BOM'd EF migration files, and normalizing would destroy `git blame`.
- **Impact:** The decision is reasonable and — unusually — actually documented at the point of effect. Residual risk is confined to cross-machine work (a second machine with different `core.autocrlf` produces noisy diffs) which is currently hypothetical.
- **Recommendation:** None now. If a second dev machine or contributor appears, do the one-time normalization (`* text=auto` + `git add --renormalize .`) as the dedicated cleanup the `.editorconfig` comment anticipates.

### GOV-8: Strength — plan-driven work packages with recorded review outcomes and CI on every push  [Info] [Effort: S]
- **Evidence:** Both large features were executed from written plans as numbered work packages with per-WP commits (video calls WP1–WP7: `a620fbf` → `9c410d9`; assistant WP1–WP7: `6fd3048` → `1306eab`); review findings were fixed in dedicated, labeled commits (`45ac04b` "WP3 review fix") with outcomes recorded (`da70fa9`); completed plan docs were removed to prevent staleness (`1730c4d`, `dff8e94`); progress was tracked in-repo while in flight (`d2c0526`, `320c7db`); local tool config was deliberately untracked (`df0ed5b`, `9527753`); CI has run the full suite on every push and PR since `c53909b`. Test-fix commits precede feature-complete claims (`9c410d9` "fill WP7 test gaps").
- **Impact:** Positive observation — this is a traceable, reviewable engineering process most solo projects lack, and it is the reason several other audits could reconstruct intent from history alone.
- **Recommendation:** Keep it. The two Medium findings above (tags, build-enforced style) are the only missing pieces of an otherwise coherent solo workflow.

## 4. Prioritized Action List

Both Medium items (GOV-1, GOV-2) are resolved. The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| GOV-3 | Low | S | Write down the (already practiced) commit convention |
| GOV-4 | Low | S | Enable branch protection requiring the tests check (or merge via PRs) |
| GOV-5 | Low | S | Fix git identity config; add .mailmap for existing aliases |
| GOV-6 | Info | S | Health files: consciously deferred |
| GOV-7 | Info | S | .gitattributes: consciously deferred (documented in .editorconfig) |
| GOV-8 | Info | S | (Strength) retain plan/WP/review-commit workflow |

## 5. Related Findings Elsewhere

- **BUILD (40):** owns CI pipeline contents (restore/build/test steps, artifact upload) that GOV-2/GOV-4 would extend with gates.
- **DEP (43):** DEP-1 — Dependabot/audit automation, whose PRs presuppose the GOV-4 merge flow; DEP-4 — governance-adjacent risk of editing the pinned Microsoft.OpenApi version without its rationale.
- **DOC (44):** DOC-8 — CHANGELOG (blocked on GOV-1 tagging); DOC-2 — the one plan doc that escaped the delete-on-completion convention celebrated in GOV-8.
- **CQ (22):** concrete instances of the style drift that GOV-2's enforcement gap permitted.
- **TEST (24):** absence of a coverage gate in CI — the quality-bar analogue of GOV-2's style gate.
