# Audit: Build & Deployment

| | |
|---|---|
| Finding prefix | BUILD |
| Created | 2026-07-11 |
| Scope | Build reproducibility and CI/CD: the GitHub Actions workflow, SDK/package pinning, the known NuGet OpenApi auto-pin trap, coverage/quality gates, versioning/releases, container/publish artifacts |
| Delegated | Runtime configuration keys → 39 (CFG). Hosting topology and what a deployment target looks like → 41 (CLOUD). Deployment *procedure*/runbook → 42 (OPS). Package upgrade/vulnerability posture → 43 (DEP). Branch protection/review process → 45 (GOV). |

## 1. Methodology

Read in full: `.github/workflows/tests.yml` (the only workflow), `global.json`, `Directory.Build.props` (root and `tests/`), `Directory.Packages.props`, `.config/dotnet-tools.json`, `.gitignore`, README (Quickstart, Quality & Tests, Configuration/production checklist sections); searched the repo for `Dockerfile*` (none), lock files (none), and README mentions of the NuGet pinning trap (none). Deliberately did NOT run `dotnet build`/`restore` — that is precisely the operation known to trigger the auto-pin trap (BUILD-1).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 5 |
| Info | 1 |

Overall: the build foundation is genuinely solid — SDK pinned via `global.json`, central package management with every version pinned in `Directory.Packages.props`, `dotnet-ef` pinned in `.config/dotnet-tools.json`, and a clean CI workflow. Both Medium findings are now resolved: the Microsoft.OpenApi auto-pin trap is documented in-repo and guarded by committed `packages.lock.json` files plus CI `--locked-mode` (BUILD-1), and a new CI job applies the whole migration chain against a real SQL Server container so a SQL-Server-only migration break no longer ships green (BUILD-2). What remains is five Low items and one Info.

## 3. Findings

> The two Medium findings (BUILD-1 OpenApi pin trap, BUILD-2 SQL Server migration CI) are fixed — see git (`Fix BUILD-1 and BUILD-2`). The remaining open items are five Low and one Info.

### BUILD-3: No publish artifact, versioning, or release process  [Low] [Effort: M]
- **Evidence:** `.github/workflows/tests.yml` ends at TRX upload — no `dotnet publish`, no artifact of the app itself; `Directory.Build.props` sets no `Version`/`InformationalVersion`; no git tags or GitHub Releases conventions in evidence; nothing stamps a build with its commit.
- **Impact:** "What exactly is running?" is unanswerable for any deployed copy; a demo machine can only be updated by rebuilding from source on it. Context-capped severity: acceptable for a certificate project, but it is the first thing a real deployment needs.
- **Recommendation:** Add a `publish` job (on tag push) that runs `dotnet publish -c Release` and uploads the output as an artifact; stamp `InformationalVersion` with `$(GITHUB_SHA)` via `-p:`. Half a day including README notes.

### BUILD-4: CI hygiene: duplicate runs, no NuGet cache, SDK selection not tied to global.json  [Low] [Effort: S]
- **Evidence:** `.github/workflows/tests.yml` — `on: push` **and** `pull_request` with no `concurrency` group (every PR commit runs twice: branch push + PR event); `actions/setup-dotnet@v4` uses `dotnet-version: 10.0.x` (line 25) rather than `global-json-file: global.json`, so CI can float to a newer feature band than the pinned 10.0.100 resolves locally; no `cache: true`/`cache-dependency-path` on setup-dotnet, so every run re-downloads all packages.
- **Impact:** Wasted runner minutes and a small local/CI SDK drift window; none of it breaks anything today.
- **Recommendation:** Add `concurrency: { group: ${{ github.workflow }}-${{ github.ref }}, cancel-in-progress: true }`; restrict `push` to `master`; switch setup-dotnet to `global-json-file`; enable its NuGet cache (pairs naturally with the lock files from BUILD-1).

### BUILD-5: No code-coverage collection or gate  [Low] [Effort: S]
- **Evidence:** README line 96 states it plainly: "There is no coverage gate or coverage artifact yet". `tests/Directory.Build.props` includes no `coverlet.collector`; the CI test step (`.github/workflows/tests.yml:34`) collects TRX only.
- **Impact:** Coverage can silently erode; the substantial test suite (five test projects) has no visible number to defend.
- **Recommendation:** Add `coverlet.collector` to `Directory.Packages.props` + `tests/Directory.Build.props`, run `dotnet test --collect:"XPlat Code Coverage"`, upload the Cobertura file as an artifact. A hard threshold gate is optional; visibility first.

### BUILD-6: No build-quality gate (warnings-as-errors / analyzers) in CI  [Low] [Effort: S]
- **Evidence:** `Directory.Build.props` sets no `TreatWarningsAsErrors`, `AnalysisLevel`, or `EnforceCodeStyleInBuild`; CI builds with plain `dotnet build -c Release --no-restore` (`.github/workflows/tests.yml:31`).
- **Impact:** New warnings accumulate invisibly (the warning inventory itself is report 22, CQ, territory); nullable-annotation regressions — the repo enables `Nullable` solution-wide — never fail anything.
- **Recommendation:** Add `-warnaserror` to the CI build step only (keeps local dev friction low), or `<TreatWarningsAsErrors Condition="'$(ContinuousIntegrationBuild)'=='true'">true</TreatWarningsAsErrors>` in `Directory.Build.props` once the existing warning count is at/near zero.

### BUILD-7: No Dockerfile or container build target  [Low] [Effort: M]
- **Evidence:** No `Dockerfile`/`compose.*` anywhere in the repo (verified glob). The stack is container-unfriendly in specific, known ways (LocalDB, DPAPI, local key ring — inventoried in report 41, CLOUD).
- **Impact:** No reproducible runtime environment exists; "works on my machine" is currently the *only* deployment story. Also blocks the SQL Server CI job in BUILD-2 from doubling as a local parity check.
- **Recommendation:** When needed, prefer `dotnet publish /t:PublishContainer` (built into the SDK, no Dockerfile to maintain) plus a compose file wiring SQL Server and optionally Ollama. Do it after, not before, the CLOUD-1 storage decisions (report 41).

### BUILD-8: Pinning baseline is strong (positive observation)  [Info] [Effort: S]
- **Evidence:** `global.json` (SDK 10.0.100, `rollForward: latestFeature`); `Directory.Packages.props` with `ManagePackageVersionsCentrally=true` and every package version explicit; `.config/dotnet-tools.json` pinning `dotnet-ef` 10.0.5 with `rollForward: false`; `tests/Directory.Build.props` correctly importing the root props.
- **Impact:** None — this is the strength that makes BUILD-1's lock-file recommendation a small step rather than a project.
- **Recommendation:** Keep; the only missing piece is the lock files (BUILD-1).

## 4. Prioritized Action List

Both Medium items (BUILD-1, BUILD-2) are resolved. The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| BUILD-4 | Low | S | Concurrency group, push-to-master only, `global-json-file`, NuGet cache |
| BUILD-5 | Low | S | Collect and upload code coverage |
| BUILD-6 | Low | S | Warnings-as-errors in CI builds |
| BUILD-3 | Low | M | Tag-triggered publish artifact with commit-stamped version |
| BUILD-7 | Low | M | SDK container publish + compose, after CLOUD storage decisions |
| BUILD-8 | Info | — | Keep the pinning baseline as-is |

## 5. Related Findings Elsewhere

- **43 (DEP)** — package currency/vulnerability posture of the versions pinned in `Directory.Packages.props`; BUILD owns only the pinning *mechanics*.
- **41 (CLOUD)** — what a deployment target actually looks like (BUILD-7 sequencing depends on it).
- **42 (OPS)** — startup auto-migration procedure risk (OPS-3) that BUILD-2's CI job partially de-risks.
- **24 (TEST)** — test-suite composition and provider-fidelity topics adjacent to BUILD-2.
- **45 (GOV)** — branch protection/required checks that would make the CI gates here enforceable.
- **22 (CQ)** — the current compiler-warning inventory that BUILD-6 would freeze.
