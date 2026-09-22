# Audit: Dependency Management

| | |
|---|---|
| Finding prefix | DEP |
| Created | 2026-07-11 |
| Scope | NuGet central package management (`Directory.Packages.props`), SDK/tool pins (`global.json`, `.config/dotnet-tools.json`), version currency and known-advisory posture, transitive-dependency risk, vendored and CDN-served frontend assets, dependency update strategy and automation, license posture of third-party packages. |
| Delegated | Build-breakage/recovery angle of the Microsoft.OpenApi auto-pin trap → BUILD (40). Supply-chain/SRI security of CDN-loaded assets → SEC (25). Missing repo LICENSE file and third-party notices as documentation → DOC (44). Absence of a PR workflow that automated update PRs would need → GOV (45). |

## 1. Methodology

Read `Directory.Packages.props`, all ten `.csproj` files under `src/` and `tests/`, both `Directory.Build.props` files, `global.json`, `.config/dotnet-tools.json`, `.github/workflows/tests.yml`, and `.gitignore`. Inventoried `src/ResetYourFuture.Web/wwwroot/lib/` (vendored Bootstrap) and the CDN `<link>`/`<script>` tags in `src/ResetYourFuture.Web/App.razor`. Verified the QuestPDF license selection in `src/ResetYourFuture.Infrastructure/ApiServices/CertificateService.cs`. Checked `.github/` for Dependabot/Renovate configuration (none).

**NOT examined:** no `dotnet restore`, `dotnet build`, or `dotnet list package [--vulnerable|--outdated]` was run — this repo has a documented trap where a plain restore can silently pin an incompatible `Microsoft.OpenApi` version and break the build (see the comment at `src/ResetYourFuture.Web/ResetYourFuture.Web.csproj:39` and BUILD 40). Consequently, the vulnerability/currency assessment below is from reviewer knowledge (cutoff early 2026), not a live scan.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 2 |
| Info | 1 |

The dependency foundation is genuinely good for a solo project: central package management is on with every version pinned in one file, the SDK is pinned via `global.json`, and `dotnet-ef` is pinned in lockstep with EF Core. The three Medium findings are now resolved: Dependabot (nuget + github-actions) plus `NuGetAuditMode=all` originally gave ongoing update PRs and transitive vulnerability auditing (DEP-1); the vendored Bootstrap now has provenance and an update path via `libman.json`, with the 42 unused dist files trimmed (DEP-2); and the Microsoft.OpenApi pin rationale (CVE + 3.x-breaking) now lives beside the version (DEP-4). Committed lock files with CI `--locked-mode` also landed under BUILD-1 (closing DEP-7). DEP-3 and DEP-10 were closed the same way on 2026-09-05. **Dependabot and its supporting CI mechanisms (DEP-9, DEP-10) were subsequently removed by deliberate decision on 2026-09-05** — see the superseding-decision note opening section 3. What remains is two Low items and one Info.

## 3. Findings

> **Superseding decision — Dependabot removed, 2026-09-05.** The project is a diploma submission with a
> defined end date, not a service with an operational life, so continuous dependency automation costs more
> attention than it returns. After merging the last outstanding bumps, `.github/dependabot.yml` was deleted
> along with the two CI mechanisms that existed only to serve it: the DEP-10 lock-file refresh and the
> DEP-9 vulnerable-package gate. CI is back to a single unconditional `--locked-mode` restore on every
> branch, which is BUILD-1's original design.
>
> This does not un-resolve DEP-1, DEP-9 or DEP-10 — each was genuinely fixed; the full record of what was
> built and what was learned lives in git history. It does mean the mechanisms they describe are no longer
> present in the repository. **What remains as the dependency-security signal is `NuGetAuditMode=all` in
> `Directory.Build.props`**, which still surfaces known advisories as NU190x restore warnings; nothing now
> fails the build on them, and nothing now opens update PRs. Dependency currency is a manual act from here:
> bump the pin in `Directory.Packages.props`, run `dotnet restore ResetYourFuture.sln --force-evaluate`, and
> commit the regenerated lock files.

> The three Medium findings are resolved — DEP-1 (Dependabot + `NuGetAuditMode=all`), DEP-2 (Bootstrap provenance via `libman.json` + trimmed dist), DEP-4 (OpenApi pin rationale in `Directory.Packages.props`); see git (`Fix DEP-1`, `Fix DEP-2 and DEP-4`). DEP-7 (lock files) was closed under BUILD-1, and DEP-3 / DEP-10 on 2026-09-05. The remaining open items are two Low and one Info.

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

## 4. Prioritized Action List

All three Medium items (DEP-1, DEP-2, DEP-4) are resolved, as is DEP-7 (under BUILD-1). DEP-3 and DEP-10 were closed on 2026-09-05 by merging the whole Dependabot backlog (#6, #11, #14, #15, #16, #17) and automating the lock-file refresh that had been blocking it. The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| DEP-6 | Low | S | Comment the QuestPDF Community-license eligibility; README third-party note |
| DEP-5 | Low | M | Vendor bootstrap-icons / Font Awesome / Quill via libman; drop CDN coupling |
| DEP-8 | Info | S | Document the dotnet-ef ↔ EF Core version coupling |

## 5. Related Findings Elsewhere

- **BUILD (40):** owns the build-breakage and recovery procedure for the Microsoft.OpenApi auto-pin trap; DEP-4/DEP-7 here address only the hygiene/visibility angle.
- **SEC (25):** missing SRI hashes on two of the three CDN assets and CDN supply-chain exposure — security angle of DEP-5.
- **DOC (44):** missing repo LICENSE file and third-party license documentation (companion to DEP-6); `.env.template` coverage of config keys.
- **GOV (45):** no PR-based workflow — Dependabot PRs (DEP-1) would land into the direct-push flow described there; no analyzer/warning gating in build.
- **AVAIL (36):** runtime availability impact of CDN outage on icons/editor (operational angle of DEP-5).
