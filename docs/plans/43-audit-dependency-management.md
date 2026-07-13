# Audit: Dependency Management

| | |
|---|---|
| Finding prefix | DEP |
| Created | 2026-07-11 |
| Scope | NuGet central package management (`Directory.Packages.props`), SDK/tool pins (`global.json`, `.config/dotnet-tools.json`), version currency and known-advisory posture, transitive-dependency risk, vendored and CDN-served frontend assets, dependency update strategy and automation, license posture of third-party packages. |
| Delegated | Build-breakage/recovery angle of the Microsoft.OpenApi auto-pin trap → BUILD (40). Supply-chain/SRI security of CDN-loaded assets → SEC (25). Missing repo LICENSE file and third-party notices as documentation → DOC (44). Absence of a PR workflow that automated update PRs would need → GOV (45). |

## 1. Methodology

Read `Directory.Packages.props`, all ten `.csproj` files under `src/` and `tests/`, both `Directory.Build.props` files, `global.json`, `.config/dotnet-tools.json`, `.github/workflows/tests.yml`, and `.gitignore`. Inventoried `src/ResetYourFuture.Web/wwwroot/lib/` (vendored Bootstrap) and the CDN `<link>`/`<script>` tags in `src/ResetYourFuture.Web/App.razor`. Verified the QuestPDF license selection in `src/ResetYourFuture.Infrastructure/ApiServices/CertificateService.cs`. Checked `.github/` for Dependabot/Renovate configuration (none).

**NOT examined:** no `dotnet restore`, `dotnet build`, or `dotnet list package [--vulnerable|--outdated]` was run — this repo has a documented trap where a plain restore can silently pin an incompatible `Microsoft.OpenApi` version and break the build (see the comment at `src/ResetYourFuture.Web/ResetYourFuture.Web.csproj:39` and BUILD 40). Consequently, the vulnerability/currency assessment below is from reviewer knowledge (cutoff early 2026), not a live scan, and is explicitly marked as needing confirmation (DEP-9).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 3 |
| Low | 4 |
| Info | 2 |

> **Fixed since audit (partial):** DEP-3's ASP.NET Core / EF Core half — while implementing SCALE-4 (DataProtection key storage), a live `dotnet list package --vulnerable` scan (something this audit deliberately didn't run — see Methodology) turned up a real, currently-unpatched **Critical** CVE (CVE-2026-40372, DataProtection cookie/auth-ticket forgery) against the pinned `10.0.5` line. The whole `10.0.x` family in `Directory.Packages.props` was bumped to `10.0.9` (matching the installed runtime and `System.Numerics.Tensors`' existing pin) in one commit, and `.config/dotnet-tools.json`'s `dotnet-ef` pin was bumped alongside it per DEP-8. This validates DEP-3's own predicted impact almost exactly. The `Microsoft.NET.Test.Sdk`/`xunit.runner.visualstudio` skew DEP-3 also flagged is unrelated to this CVE and remains open.

The dependency foundation is genuinely good for a solo project: central package management is on (`ManagePackageVersionsCentrally=true`) with every version pinned in one file, csproj files carry no version attributes, the SDK is pinned via `global.json` with a sensible `rollForward`, `dotnet-ef` is pinned in a tool manifest in lockstep with the EF Core packages, and two known CVEs (Microsoft.OpenApi, and now the DataProtection cookie-forgery CVE caught mid-session) have been handled. What is missing is everything *ongoing*: there is no automated update or vulnerability alerting, no recorded update strategy for the vendored Bootstrap or the three runtime CDN dependencies, no lock file, and a smaller version skew remains in the test-tooling packages. Nothing here is broken today; all findings are about the maintenance treadmill silently stopping.

## 3. Findings

### DEP-1: No automated dependency updates or vulnerability alerting  [Medium] [Effort: S]
- **Evidence:** `.github/` contains only `workflows/tests.yml` — no `dependabot.yml`, no Renovate config. All 25 versions in `Directory.Packages.props` are maintained by hand. The CI restore step (`tests.yml:28`) will print NuGet audit warnings (NU1901–NU1904) for known-vulnerable packages, but nothing fails or surfaces them — they scroll by in a green build.
- **Impact:** A published advisory against any pinned package (auth stack: `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`; input handling: `HtmlSanitizer`) goes unnoticed until manually looked for. The repo already demonstrates the cost: the Microsoft.OpenApi CVE was caught and pinned manually (DEP-5), which does not scale past the packages one happens to read about.
- **Recommendation:** Add `.github/dependabot.yml` covering the `nuget` and `github-actions` ecosystems (weekly is fine; grouped updates keep PR noise down for a solo maintainer). GitHub's Dependabot *alerts* work even without auto-PRs. Additionally consider `<NuGetAuditMode>all</NuGetAuditMode>` and promoting NU190x warnings to errors in `Directory.Build.props` so CI fails on a known-vulnerable restore — this runs on a clean CI clone, so the local auto-pin trap (BUILD 40) does not apply there.

### DEP-2: Vendored Bootstrap 5.3.3 has no recorded provenance or update path  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/wwwroot/lib/bootstrap/dist/` contains the full Bootstrap 5.3.3 distribution (version confirmed in the `bootstrap.min.css` header) — 32 CSS files including all RTL variants and source maps, plus 12 JS files — while `App.razor:22` references exactly one of them (`bootstrap.min.css`; not even the bundle JS is used). There is no `libman.json`, `package.json`, or README note recording where it came from or how to update it.
- **Impact:** Bootstrap 5.3.3 shipped February 2024 — roughly two and a half years old at audit time, with multiple 5.3.x patch releases since (bugfixes and at least one security fix in the 5.3.x line to reviewer knowledge). Because the copy is an anonymous vendored blob, no tool will ever flag it as outdated, and a future maintainer cannot tell whether local modifications were made (diffing against upstream is the only way). The ~40 unused dist files also bloat the repo and the published output.
- **Recommendation:** Add a `libman.json` (`cdnjs`/`jsdelivr` provider) declaring `twbs/bootstrap@5.3.x` with only the files actually referenced — this records provenance, enables `libman update`, and trims the unused RTL/map files. Bump to the latest 5.3.x while doing it.

### DEP-3: Test-tooling packages have drifted from their current patch releases  [Low] [Effort: S]
- **Evidence:** Narrowed from the original finding (the ASP.NET Core/EF Core `10.0.x` half is fixed — see the "Fixed since audit" note). `Microsoft.NET.Test.Sdk` is at `17.12.0` (17.13/17.14 shipped in 2025) and `xunit.runner.visualstudio` at `2.8.2` against `xunit` `2.9.2`. Currency could not be confirmed live (no `dotnet list package --outdated` was run for this pass).
- **Impact:** Test-only packages — no production attack surface. Downgraded from Medium: the security-relevant half of the original skew (the framework family) is resolved; this remaining piece is pure staleness, not a known vulnerability.
- **Recommendation:** Bump `Microsoft.NET.Test.Sdk`/`xunit`/`xunit.runner.visualstudio` to current in the same pass as any other routine dependency maintenance. Adopt DEP-1's rule going forward: when touching any package family, bump the whole family to its current patch in one commit.

### DEP-4: The Microsoft.OpenApi transitive pin is load-bearing but its rationale lives in the wrong file  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/ResetYourFuture.Web.csproj:39-40` carries the explanation ("Pin transitive Microsoft.OpenApi to a patched 2.x release (CVE-2026-49451 fixed in 2.7.5+); avoids the breaking 3.x major") next to a bare `<PackageReference Include="Microsoft.OpenApi" />`. The actual version decision — `2.9.0` — sits in `Directory.Packages.props:23` with no comment at all. The repo has a documented history of tooling silently re-pinning this package to an incompatible version, recoverable only by `git checkout` of the csproj/props files (BUILD 40 owns that failure mode).
- **Impact:** The person (or update bot, per DEP-1) editing `Directory.Packages.props` is the one who needs the warning, and that file says nothing. A routine-looking bump of `2.9.0` → `3.x`, or an IDE auto-pin, discards a deliberate CVE mitigation and/or breaks the build, with the rationale two directories away.
- **Recommendation:** Duplicate a one-line comment beside `Directory.Packages.props:23`: *stay on latest 2.x — 2.7.5+ patches CVE-2026-49451; 3.x is a breaking major for Microsoft.AspNetCore.OpenApi (see Web.csproj)*. If DEP-1's Dependabot is added, add an `ignore` rule for `Microsoft.OpenApi` major updates.

### DEP-5: Three runtime CDN dependencies with no local fallback  [Low] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/App.razor` loads bootstrap-icons 1.11.3 from jsDelivr (line 27, render-blocking by design per its own comment), Font Awesome 6.5.1 from cdnjs (line 32, the only one with an SRI hash), and Quill 2.0.3 CSS+JS from jsDelivr (lines 36, 53). Versions are pinned in the URLs, but none of these appear in any manifest, and there is no local fallback.
- **Impact:** Dependency-hygiene angle (the missing-SRI/supply-chain security angle belongs to SEC 25): these four assets are invisible to every update/audit mechanism, the app's icon set and rich-text editor break offline or if a CDN is unreachable (relevant for a university demo on venue Wi-Fi), and the strategy is inconsistent with Bootstrap CSS being vendored — a maintainer has to discover two different asset regimes.
- **Recommendation:** Fold these into the same `libman.json` as DEP-2 and serve them from `wwwroot/lib/`, eliminating the CDN coupling and unifying the update path. (Quill JS is `defer`-loaded, so vendoring does not change load behavior.)

### DEP-6: QuestPDF Community-license condition is relied on but recorded nowhere  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Infrastructure/ApiServices/CertificateService.cs:26` sets `QuestPDF.Settings.License = LicenseType.Community;` with no comment. QuestPDF (`2026.2.4` in `Directory.Packages.props:26`) is dual-licensed: the free Community tier is conditional on staying under the vendor's annual-revenue threshold (historically <$1M USD gross revenue).
- **Impact:** Zero problem for a university certificate project — the condition is trivially met. But the eligibility assumption is invisible: nothing in the README or the code states that commercial deployment of this platform would require re-evaluating (and possibly purchasing) a QuestPDF license. Every other runtime dependency reviewed is permissive (MailKit MIT, HtmlSanitizer MIT, OllamaSharp MIT, OllamaSharp/Microsoft.* MIT, Bootstrap MIT — header retained in the vendored files, satisfying its notice requirement; test stack Apache-2.0/BSD). QuestPDF is the sole conditional license in the graph.
- **Recommendation:** Add a one-line comment above the `LicenseType.Community` assignment stating the eligibility basis, and a "Third-party licenses" note in the README (DOC 44 owns the broader repo-license gap).

### DEP-7: No NuGet lock file — transitive graph changes are invisible in diffs  [Low] [Effort: S]
- **Evidence:** No `packages.lock.json` exists in any project; `RestorePackagesWithLockFile` appears nowhere in the props/csproj files (verified by search). CI restores unlocked (`tests.yml:28`).
- **Impact:** Central pinning fixes *direct* versions, but the resolved transitive closure is only implicit. A lock file would (a) make any unexpected resolution change — including the Microsoft.OpenApi auto-pin trap's effects — show up as a reviewable diff, and (b) allow `--locked-mode` restore in CI for tamper/repeatability guarantees.
- **Recommendation:** Set `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in `Directory.Build.props`, commit the generated lock files, and add `--locked-mode` to the CI restore. Caveat: generate the lock files from a known-good state, since creating them requires a restore (do it right after a clean `git status`, and diff the props files afterward per the BUILD 40 recovery note).

### DEP-8: SDK and tool pinning is done right — keep dotnet-ef in lockstep when bumping EF  [Info] [Effort: S]
- **Evidence:** `global.json` pins SDK `10.0.100` with `rollForward: latestFeature` (works with CI's `dotnet-version: 10.0.x`); `.config/dotnet-tools.json` pins `dotnet-ef` at `10.0.9` with `rollForward: false`, exactly matching the EF Core package versions in `Directory.Packages.props` (both bumped together when the CVE-2026-40372 fix landed — the coupling this finding warns about was exercised for real and held).
- **Impact:** Positive observation. The undocumented coupling is still undocumented (only proven correct once, by hand) — still worth writing down for the next person who bumps just one side.
- **Recommendation:** Mention the coupling in the README's migration troubleshooting row (`dotnet ef migrations add ...` is already documented there), or in a comment in the tool manifest.

### DEP-9: Known-advisory review at pinned versions — clean to reviewer knowledge, unverified live  [Info] [Effort: S]
- **Evidence:** Versions from `Directory.Packages.props` checked against advisories known to this reviewer (cutoff early 2026): `System.IdentityModel.Tokens.Jwt 8.3.1` (the 2024 padding-oracle-era CVEs affected 5.x–7.x; 8.x unaffected), `MailKit 4.17.0`, `HtmlSanitizer 9.0.892` (post-dates its 2023 XSS-bypass advisories), `SQLitePCLRaw.bundle_e_sqlite3 3.0.3`, `Swashbuckle.AspNetCore.SwaggerUI 10.2.1`, `OllamaSharp 5.4.25`, `Microsoft.Extensions.AI 10.7.0` — no known outstanding advisories. `Microsoft.OpenApi 2.9.0` post-dates the CVE-2026-49451 fix (2.7.5+).
- **Impact:** This is a point-in-time, knowledge-based review, **not** a scan — `dotnet list package --vulnerable` was deliberately not run locally (Methodology; auto-pin trap).
- **Recommendation:** Run the vulnerable-package check in CI (clean clone, trap-free) — either via DEP-1's audit-warnings-as-errors or an explicit scheduled `dotnet list package --vulnerable --include-transitive` job.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| DEP-1 | Medium | S | Add Dependabot (nuget + github-actions); gate NuGet audit warnings in CI |
| DEP-2 | Medium | S | Record Bootstrap in libman.json, trim unused dist files, bump 5.3.x |
| DEP-4 | Medium | S | Move/duplicate the Microsoft.OpenApi pin rationale into Directory.Packages.props |
| DEP-6 | Low | S | Comment the QuestPDF Community-license eligibility; README third-party note |
| DEP-7 | Low | S | Enable RestorePackagesWithLockFile + locked-mode CI restore |
| DEP-3 | Low | S | Bump Microsoft.NET.Test.Sdk / xunit.runner.visualstudio to current |
| DEP-5 | Low | M | Vendor bootstrap-icons / Font Awesome / Quill via libman; drop CDN coupling |
| DEP-8 | Info | S | Document the dotnet-ef ↔ EF Core version coupling |
| DEP-9 | Info | S | Add a live vulnerable-package scan in CI |

## 5. Related Findings Elsewhere

- **BUILD (40):** owns the build-breakage and recovery procedure for the Microsoft.OpenApi auto-pin trap; DEP-4/DEP-7 here address only the hygiene/visibility angle.
- **SEC (25):** missing SRI hashes on two of the three CDN assets and CDN supply-chain exposure — security angle of DEP-5.
- **DOC (44):** missing repo LICENSE file and third-party license documentation (companion to DEP-6); `.env.template` coverage of config keys.
- **GOV (45):** no PR-based workflow — Dependabot PRs (DEP-1) would land into the direct-push flow described there; no analyzer/warning gating in build.
- **AVAIL (36):** runtime availability impact of CDN outage on icons/editor (operational angle of DEP-5).
