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
| Medium | 0 |
| Low | 2 |
| Info | 2 |

The dependency foundation is genuinely good for a solo project: central package management is on with every version pinned in one file, the SDK is pinned via `global.json`, and `dotnet-ef` is pinned in lockstep with EF Core. The three Medium findings are now resolved: Dependabot (nuget + github-actions) plus `NuGetAuditMode=all` give ongoing update PRs and transitive vulnerability auditing (DEP-1); the vendored Bootstrap now has provenance and an update path via `libman.json`, with the 42 unused dist files trimmed (DEP-2); and the Microsoft.OpenApi pin rationale (CVE + 3.x-breaking) now lives beside the version (DEP-4). Committed lock files with CI `--locked-mode` also landed under BUILD-1 (closing DEP-7). DEP-10 (added and resolved 2026-09-05) fixed the flip side of that guard: it made every Dependabot PR fail, so the whole DEP-1 pipeline had stalled with five PRs unmerged. What remains is two Low items and two Info.

## 3. Findings

> The three Medium findings are resolved — DEP-1 (Dependabot + `NuGetAuditMode=all`), DEP-2 (Bootstrap provenance via `libman.json` + trimmed dist), DEP-4 (OpenApi pin rationale in `Directory.Packages.props`); see git (`Fix DEP-1`, `Fix DEP-2 and DEP-4`). DEP-7 (lock files) was closed under BUILD-1, and DEP-3 / DEP-10 on 2026-09-05. The remaining open items are two Low and two Info.

### DEP-3: Test-tooling packages have drifted from their current patch releases  [Low] [Effort: S] — RESOLVED 2026-09-05
- **Evidence:** Narrowed from the original finding (the ASP.NET Core/EF Core `10.0.x` half is fixed — the whole family in `Directory.Packages.props` is pinned at `10.0.9`, a bump that also patched the Critical CVE-2026-40372 present in the previous `10.0.5` pin). `Microsoft.NET.Test.Sdk` is at `17.12.0` (17.13/17.14 shipped in 2025) and `xunit.runner.visualstudio` at `2.8.2` against `xunit` `2.9.2`. Currency could not be confirmed live (no `dotnet list package --outdated` was run for this pass).
- **Impact:** Test-only packages — no production attack surface. Downgraded from Medium: the security-relevant half of the original skew (the framework family) is resolved; this remaining piece is pure staleness, not a known vulnerability.
- **Recommendation:** Bump `Microsoft.NET.Test.Sdk`/`xunit`/`xunit.runner.visualstudio` to current in the same pass as any other routine dependency maintenance. Adopt DEP-1's rule going forward: when touching any package family, bump the whole family to its current patch in one commit.
- **Resolution (2026-09-05):** closed by merging the Dependabot `test-tooling` group PR (#15): `Microsoft.NET.Test.Sdk` `17.12.0` → `18.9.0`, `xunit` `2.9.2` → `2.9.3`, `xunit.runner.visualstudio` `2.8.2` → `4.0.0`, plus `bunit` `2.7.2` → `2.9.0` and `NSubstitute` `5.3.0` → `6.2.0`. All 974 tests pass on the new tooling.

### DEP-10: Dependabot PRs always failed CI because it never regenerates the lock files  [Medium] [Effort: S] — RESOLVED 2026-09-05
- **Evidence:** Every open Dependabot NuGet PR (#11, #14, #15, #16, #17) sat red. Dependabot updates `Directory.Packages.props` and at most one project's `packages.lock.json`, but never the `CentralTransitive` entries in the other nine, so the BUILD-1 `--locked-mode` restore failed with `NU1004` ("Mistmatch between the requestedVersion of a lock file dependency marked as CentralTransitive"). It also never rebases, so a branch opened before any lock-file commit on `master` failed on the stale entry too.
- **Impact:** The dependency-update pipeline set up under DEP-1 was effectively inert — every PR needed a manual `restore --force-evaluate` and a hand-pushed commit, which for a solo maintainer means bumps quietly pile up unmerged. That is a security exposure, not just friction: it is the same pipeline that is supposed to deliver CVE patches.
- **Resolution:** `.github/workflows/tests.yml` now branches on the ref. On `dependabot/*` only, both jobs restore with `--force-evaluate`, and the `test` job commits the refreshed lock files back to the PR branch **after** the build, style gate and tests pass — so the run that regenerates them is the run that verifies them, and a bump that breaks the suite leaves no commit behind. Every other branch keeps the strict `--locked-mode` guard, so BUILD-1's drift protection is intact where it matters. The job takes `permissions: contents: write` because Dependabot-triggered runs get a read-only token by default.
- **Known limits:** a `GITHUB_TOKEN` push does not retrigger workflows, so the refreshed lock files are covered by the run that made them rather than a fresh run — acceptable, since that run tested exactly that tree. A bump that exposes a genuine version conflict still fails loudly and needs a human: PR #14's `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.11` required `System.IdentityModel.Tokens.Jwt` >= `8.19.2` against a direct `8.3.1` pin (`NU1605`), which `--force-evaluate` reports rather than fixes.

### DEP-5: Three runtime CDN dependencies with no local fallback  [Low] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/App.razor` loads bootstrap-icons 1.11.3 from jsDelivr (line 27, render-blocking by design per its own comment), Font Awesome 6.5.1 from cdnjs (line 32, the only one with an SRI hash), and Quill 2.0.3 CSS+JS from jsDelivr (lines 36, 53). Versions are pinned in the URLs, but none of these appear in any manifest, and there is no local fallback.
- **Impact:** Dependency-hygiene angle (the missing-SRI/supply-chain security angle belongs to SEC 25): these four assets are invisible to every update/audit mechanism, the app's icon set and rich-text editor break offline or if a CDN is unreachable (relevant for a university demo on venue Wi-Fi), and the strategy is inconsistent with Bootstrap CSS being vendored — a maintainer has to discover two different asset regimes.
- **Recommendation:** Fold these into the same `libman.json` as DEP-2 and serve them from `wwwroot/lib/`, eliminating the CDN coupling and unifying the update path. (Quill JS is `defer`-loaded, so vendoring does not change load behavior.)

### DEP-6: QuestPDF Community-license condition is relied on but recorded nowhere  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Infrastructure/ApiServices/CertificateService.cs:26` sets `QuestPDF.Settings.License = LicenseType.Community;` with no comment. QuestPDF (`2026.2.4` in `Directory.Packages.props:26`) is dual-licensed: the free Community tier is conditional on staying under the vendor's annual-revenue threshold (historically <$1M USD gross revenue).
- **Impact:** Zero problem for a university certificate project — the condition is trivially met. But the eligibility assumption is invisible: nothing in the README or the code states that commercial deployment of this platform would require re-evaluating (and possibly purchasing) a QuestPDF license. Every other runtime dependency reviewed is permissive (MailKit MIT, HtmlSanitizer MIT, OllamaSharp MIT, OllamaSharp/Microsoft.* MIT, Bootstrap MIT — header retained in the vendored files, satisfying its notice requirement; test stack Apache-2.0/BSD). QuestPDF is the sole conditional license in the graph.
- **Recommendation:** Add a one-line comment above the `LicenseType.Community` assignment stating the eligibility basis, and a "Third-party licenses" note in the README (DOC 44 owns the broader repo-license gap).

### DEP-8: SDK and tool pinning is done right — keep dotnet-ef in lockstep when bumping EF  [Info] [Effort: S]
- **Evidence:** `global.json` pins SDK `10.0.100` with `rollForward: latestFeature` (works with CI's `dotnet-version: 10.0.x`); `.config/dotnet-tools.json` pins `dotnet-ef` at `10.0.9` with `rollForward: false`, exactly matching the EF Core package versions in `Directory.Packages.props` (both bumped together when the CVE-2026-40372 fix landed — the coupling this finding warns about was exercised for real and held).
- **Impact:** Positive observation. The undocumented coupling is still undocumented (only proven correct once, by hand) — still worth writing down for the next person who bumps just one side.
- **Recommendation:** Mention the coupling in the README's migration troubleshooting row (`dotnet ef migrations add ...` is already documented there), or in a comment in the tool manifest.

### DEP-9: Known-advisory review at pinned versions — clean to reviewer knowledge, unverified live  [Info] [Effort: S]
- **Evidence:** Versions from `Directory.Packages.props` checked against advisories known to this reviewer (cutoff early 2026): `System.IdentityModel.Tokens.Jwt 8.3.1` (the 2024 padding-oracle-era CVEs affected 5.x–7.x; 8.x unaffected), `MailKit 4.17.0`, `HtmlSanitizer 9.0.892` (post-dates its 2023 XSS-bypass advisories), `SQLitePCLRaw.bundle_e_sqlite3 3.0.3`, `Swashbuckle.AspNetCore.SwaggerUI 10.2.1`, `OllamaSharp 5.4.25`, `Microsoft.Extensions.AI 10.7.0` — no known outstanding advisories. `Microsoft.OpenApi 2.9.0` post-dates the CVE-2026-49451 fix (2.7.5+).
- **Impact:** This is a point-in-time, knowledge-based review, **not** a scan — `dotnet list package --vulnerable` was deliberately not run locally (Methodology; auto-pin trap).
- **Recommendation:** Run the vulnerable-package check in CI (clean clone, trap-free) — either via DEP-1's audit-warnings-as-errors or an explicit scheduled `dotnet list package --vulnerable --include-transitive` job.

## 4. Prioritized Action List

All three Medium items (DEP-1, DEP-2, DEP-4) are resolved, as is DEP-7 (under BUILD-1). DEP-3 and DEP-10 were closed on 2026-09-05 by merging the whole Dependabot backlog (#6, #11, #14, #15, #16, #17) and automating the lock-file refresh that had been blocking it. The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| DEP-6 | Low | S | Comment the QuestPDF Community-license eligibility; README third-party note |
| DEP-5 | Low | M | Vendor bootstrap-icons / Font Awesome / Quill via libman; drop CDN coupling |
| DEP-8 | Info | S | Document the dotnet-ef ↔ EF Core version coupling |
| DEP-9 | Info | S | Add a live vulnerable-package scan in CI |

## 5. Related Findings Elsewhere

- **BUILD (40):** owns the build-breakage and recovery procedure for the Microsoft.OpenApi auto-pin trap; DEP-4/DEP-7 here address only the hygiene/visibility angle.
- **SEC (25):** missing SRI hashes on two of the three CDN assets and CDN supply-chain exposure — security angle of DEP-5.
- **DOC (44):** missing repo LICENSE file and third-party license documentation (companion to DEP-6); `.env.template` coverage of config keys.
- **GOV (45):** no PR-based workflow — Dependabot PRs (DEP-1) would land into the direct-push flow described there; no analyzer/warning gating in build.
- **AVAIL (36):** runtime availability impact of CDN outage on icons/editor (operational angle of DEP-5).
