# Audit: Code Quality

| | |
|---|---|
| Finding prefix | CQ |
| Created | 2026-07-11 |
| Scope | Micro-level quality of the production source: naming, duplication, dead/vestigial code, nullability, style consistency, file/namespace organization, small API-shape inconsistencies |
| Delegated | Macro coupling & cost-of-change → MAINT (23); structural decisions → ARCH (21); test-code quality → TEST (24); wire-format/API conventions → API (31); security → SEC (25); performance → PERF (34) |

## 1. Methodology

Examined: all 297 non-generated `.cs` files under `src/` were surveyed by targeted greps (namespace declarations, style patterns, TODO/HACK markers, `#pragma` suppressions, duplication signatures) with full reads of the files findings cite: `Application/ApiServices/CourseService.cs`, `Application/ApiServices/SubscriptionService.cs` (head), `Application/ApiServices/AuthApiService.cs` (head), `Infrastructure/ApiServices/TokenService.cs`, `Web/Consumers/ApiClientBase.cs`, `Web/Consumers/CourseConsumer.cs`, `Web/Extensions/ServiceResultExtensions.cs`, `Web/Controllers/CoursesController.cs`, `Web/Controllers/AdminController.cs` (head), `Web/Startup/InfrastructureEndpointsExtensions.cs`, `Web/Pages/Billing.razor`, `Web/Shared/Components/Data/AdminPaginationToolbar.razor`, `Web/Pages/Home.razor.cs` (head), `Application/DTOs/PagedResult.cs`, `Application/DTOs/AdminDtos.cs` (head), `Infrastructure/Data/DesignTimeDbContextFactory.cs`, `.editorconfig`. Style prevalence was quantified (spaced-paren regex across src+tests; block-scoped namespace scan). Razor file inventory (70 files) and code-behind pairing checked.

NOT examined: compiler-warning output — the solution was deliberately not built (a known NuGet restore behavior can rewrite `ResetYourFuture.Web.csproj`/`Directory.Packages.props`; this audit must not modify tracked files). Warning posture is inferred from `<Nullable>enable</Nullable>` solution-wide, the absence of `#pragma warning disable` outside generated migrations, and the narrow `NoWarn` (CS1591 only, documented in csproj comments). Generated files (`*.Designer.cs`, `Data/Migrations/**`) were excluded from style findings.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 5 |
| Info | 2 |

Micro-level quality is high and — more unusually — *improving on record*: a root `.editorconfig` (added 2026-07) now pins standard .NET formatting, the once-dominant spaced-paren call style has been swept away to a 4-line residue, namespaces are file-scoped and correct per project, nullable reference types are enabled solution-wide with no suppression sprawl (zero `#pragma warning disable` in hand-written code, exactly one TODO in `src/`), and XML doc comments consistently explain *why* rather than *what* — several (SsrApiHandler, AuthService, CustomWebAppFactory) read like miniature ADRs. All three Medium findings — the bilingual-fallback ternary, Billing's pagination toolbar, and the two unnamed ServiceResult conventions — are fixed. What remains is minor: a residual sliver of duplication in TokenService, DTO file/namespace organization, and small formatting/placement stragglers.

## 3. Findings

### CQ-4: TokenService's two mint methods still duplicate the signing/token-construction boilerplate  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Infrastructure/ApiServices/TokenService.cs:43-62` (`GenerateAccessTokenAsync`) vs `:76-95` (`GenerateImpersonationTokenAsync`) — the nine-claim list itself is no longer duplicated (both now call the shared `UserClaimsBuilder.Build`, ARCH-2's fix), but the surrounding signing-credentials/expiration/`JwtSecurityToken` construction (~8 lines) is still repeated verbatim in both methods.
- **Impact:** Low now that the claim-drift risk is gone — a change to signing algorithm or token construction still needs two edits, but that's boilerplate, not business-significant data.
- **Recommendation:** Fold into one method with an optional `adminId` parameter, or extract a private `BuildToken(IEnumerable<Claim>)` helper. Opportunistic.

### CQ-5: returnUrl sanitization copy-pasted three times in one file  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Startup/InfrastructureEndpointsExtensions.cs:34-41` (`/culture/set`), `:179-186` (`/auth/complete`), `:201-208` (`/auth/signout`) — identical absolute-URL→PathAndQuery / leading-slash logic.
- **Impact:** This is security-adjacent input handling (open-redirect defense; SEC 25 owns the vulnerability analysis) — exactly the kind of logic that must not drift between copies. A hardening fix applied to one endpoint would leave the other two stale.
- **Recommendation:** Extract `private static string ToLocalRedirect(string? returnUrl)` in the same class; three call sites.

### CQ-6: DTO organization: folders don't match the flat namespace, and one-type-per-file vs grab-bag files coexist  [Low] [Effort: S]
- **Evidence:** All 36 DTO files under `src/ResetYourFuture.Application/DTOs/**` (including subfolders `Auth/`, `Chat/`, `Courses/`, …) declare the single flat namespace `ResetYourFuture.Application.DTOs` (verified for `Auth/LoginRequestDto.cs`, `Chat/ChatDtos.cs`, `Courses/LessonDetailDto.cs`). Granularity is mixed: `DTOs/AdminDtos.cs` packs 11 types; `DTOs/Blog/AdminBlogArticleDto.cs` holds one.
- **Impact:** IDE navigate-by-namespace shows one undifferentiated 100+-type bucket; the folder taxonomy is cosmetic. Finding a DTO requires knowing which grab-bag file it lives in. Mild, but it is the largest single namespace in the solution and grows with every feature.
- **Recommendation:** Either adopt folder-matching namespaces (`ResetYourFuture.Application.DTOs.Courses` etc. — mechanical, but touches many usings) or, cheaper, keep the flat namespace and standardize on feature-grouped files (one `XyzDtos.cs` per feature folder, dissolving the loose top-level files) so at least file placement is predictable.

### CQ-7: Generic PagedResult<T> hardcodes a domain-specific default, `SortBy = "email"`  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Application/DTOs/PagedResult.cs:6-12` — the reusable envelope ("Reusable across all list endpoints", per its own doc) defaults `SortBy` to `"email"` and `SortDir` to `"asc"`; only the admin-users list actually sorts by email (courses sort by `TitleEn`, e.g. `CourseService.cs:46`).
- **Impact:** Every non-user paged response silently reports `SortBy="email"` unless the constructor caller remembers to override — a small lie in the payload that consumers might one day trust. A generic type carrying a leaked domain default is also a confusing precedent.
- **Recommendation:** Default `SortBy`/`SortDir` to `null`/omit them from the generic record (move them to a derived or wrapper type for the sortable admin lists).

### CQ-8: Residual spaced-paren formatting in 3 files, and no automated format gate to prevent regression  [Low] [Effort: S] — RESIDUE FIXED 2026-09-05, GATE STILL ABSENT
- **Evidence:** The legacy `Method( arg )` style — once dominant — survives only at `src/ResetYourFuture.Web/OpenApi/OpenApiExtensions.cs:126-127`, `tests/ResetYourFuture.Web.Tests/ConsumerTests.cs:118`, and `tests/ResetYourFuture.Web.Tests/SiteSettingsIntegrationTests.cs:28`. `.editorconfig` (root, lines 18-27) now mandates no-space parens, but nothing runs `dotnet format` in CI (`.github/workflows/tests.yml` has restore/build/test only).
- **Impact:** Trivial today (4 lines), but the `.editorconfig` rules are advisory — IDE-dependent — so the style can drift back without anyone noticing. The cleanup investment already made deserves a lock.
- **Recommendation:** Fix the 4 residual lines; optionally add a `dotnet format --verify-no-changes` step to the existing workflow (BUILD 40 owns CI design — coordinate there).
- **Update (2026-09-05):** two corrections to the evidence above, both found by re-running the grep rather than trusting the count. (1) The residue was never 3 files / 4 lines — it was **4 files / 9 lines**. `src/ResetYourFuture.Infrastructure/Data/EncryptedStringConverter.cs` (6 lines) was missed when this finding was written; it was added by e19387e under COMP-2 with the spaced style already in it, a day before this document's last edit. (2) All 9 lines are now fixed, so the residue is zero — verified by grep, plus a Release build and the full 974-test suite.
- **Still open:** the *gate*, not the residue. CI's `dotnet format style --verify-no-changes --severity warn` (added under GOV-2) covers the warning-level IDE style rules; whitespace and paren formatting live in the separate `dotnet format whitespace` subcommand, which is consciously deferred per GOV-2. So this style can still drift back unnoticed. Closing this finding means adding that gate, not editing more source.

### CQ-9: Interface placement in the Web project follows three different conventions  [Info]
- **Evidence:** `src/ResetYourFuture.Web/Interfaces/` holds exactly two interfaces (`ICallService`, `IChatService`); the 18 consumer interfaces are co-located with implementations in `src/ResetYourFuture.Web/Consumers/`; Application keeps all interfaces in `ApiInterfaces/` separate from `ApiServices/` — and that folder also hosts non-API abstractions (`IEmailService`, `IFileStorage`, `ITokenService`).
- **Impact:** None functionally; minor "where do I put/find the interface?" friction, and the `ApiInterfaces` name under-describes its contents.
- **Recommendation:** Fold `Web/Interfaces` into the folders of their implementations (matching the Consumers convention), or leave as-is — only worth doing opportunistically.

### CQ-10: Isolated style stragglers: one block-scoped namespace, one unprefixed private field  [Info]
- **Evidence:** `src/ResetYourFuture.Infrastructure/Data/DesignTimeDbContextFactory.cs:4` is the only hand-written file using a block-scoped namespace (everything else is file-scoped; `.editorconfig:30` sets `file_scoped:warning`). `src/ResetYourFuture.Web/Pages/Home.razor.cs:33` declares `private string? backgroundImageUrl` (no `_` prefix, unlike sibling fields `_isAuthenticated`, `_blogSummaries` in the same class); it is also assigned a constant and never mutated, making the null-check in `heroBackgroundStyle` (line 34-36) a dead branch.
- **Impact:** Cosmetic; listed for completeness because the codebase is otherwise consistent enough that these stand out.
- **Recommendation:** File-scope the factory namespace; rename to `_backgroundImageUrl` or inline the constant and delete the dead conditional.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| CQ-4 | Low | S | Merge TokenService's twin mint methods' remaining signing/construction boilerplate |
| CQ-5 | Low | S | Extract `ToLocalRedirect()` for the 3 duplicated returnUrl blocks |
| CQ-6 | Low | S | Standardize DTO file grouping (and optionally folder-matching namespaces) |
| CQ-7 | Low | S | Remove the `SortBy="email"` default from generic `PagedResult<T>` |
| CQ-8 | Low | S | Fix 4 residual spaced-paren lines; add a format verification step to CI |
| CQ-9 | Info | S | (Opportunistic) unify interface placement in Web |
| CQ-10 | Info | S | (Opportunistic) file-scope DesignTimeDbContextFactory; clean Home.razor.cs field |

## 5. Related Findings Elsewhere

- **ARCH (21)** owns the cross-file JWT-mint duplication that CQ-4 was the within-file instance of — both fixed via the shared `UserClaimsBuilder` (ARCH-2).
- **MAINT (23)** owns the macro duplication engine: 18 hand-written consumer+interface pairs mirroring controllers, and 18 hand-maintained resx `Designer.cs` files — file-level repetition that is architectural, not micro.
- **TEST (24)** owns test-code duplication (CustomWebAppFactory's triplicated user-provisioning block) and test-side style.
- **API (31)** owns the wire-format consistency of the now-named `ToActionResult`/`ToEmbeddedActionResult` conventions (CQ-3, fixed).
- **SEC (25)** owns the open-redirect analysis of the returnUrl logic deduplicated in CQ-5.
- **UI (32)** owns visual/a11y correctness of the pagination toolbar component (CQ-2, fixed).
