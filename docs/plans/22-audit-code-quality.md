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
| Medium | 3 |
| Low | 5 |
| Info | 2 |

Micro-level quality is high and — more unusually — *improving on record*: a root `.editorconfig` (added 2026-07) now pins standard .NET formatting, the once-dominant spaced-paren call style has been swept away to a 4-line residue, namespaces are file-scoped and correct per project, nullable reference types are enabled solution-wide with no suppression sprawl (zero `#pragma warning disable` in hand-written code, exactly one TODO in `src/`), and XML doc comments consistently explain *why* rather than *what* — several (SsrApiHandler, AuthService, CustomWebAppFactory) read like miniature ADRs. What remains is a modest set of duplications (the bilingual-fallback ternary ×30, one hand-rolled pagination toolbar, twin token-mint methods, triple returnUrl sanitization) and two conventions that never got unified: DTO file/namespace organization and the ServiceResult→ActionResult mapping, which exists in two competing dialects.

## 3. Findings

### CQ-1: The bilingual fallback ternary `isEl ? (El ?? En) : En` is hand-repeated ~30 times across 7 files  [Medium] [Effort: S]
- **Evidence:** 30 grep hits in `src/ResetYourFuture.Application/ApiServices/CourseService.cs` (e.g. lines 89-90, 129-130, 138-143, 265-278), `AssessmentService.cs`, `CategoryService.cs`, `BlogArticleService.cs`, `AssistantService.cs`, `AssistantRetrievalService.cs`, and `src/ResetYourFuture.Web/Controllers/CertificatesController.cs`.
- **Impact:** The En/El selection rule (Greek preferred, English fallback) is business-significant and exists only as repeated inline expressions. A policy change (e.g. "fall back to empty, not English" or a third culture) is a ~30-site edit; a single-site typo (swapping El/En) is invisible in review. Each service also re-derives `isEl` from the `lang` string independently (`CourseService.cs:25`, `:103`, `:226`).
- **Recommendation:** Add one static helper in `ResetYourFuture.Application.Common` — e.g. `Localized.Pick(bool isEl, string en, string? el)` (plus a `bool IsEl(string lang)`) — and mechanically replace the 30 sites. Pure find-and-replace refactor, no behavior change.

### CQ-2: Billing.razor hand-rolls the pagination toolbar that AdminPaginationToolbar already encapsulates  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Pages/Billing.razor:161-181` reproduces the exact markup (same `pagination-toolbar-admin` class, same `GlobalRes.PaginationShowingFormat` / `PaginationRows` / `PaginationPrev` strings, same select+buttons structure) that `src/ResetYourFuture.Web/Shared/Components/Data/AdminPaginationToolbar.razor` provides as a parameterized component — and which seven other pages (`AdminAssessments`, `AdminAssessmentSubmissions`, `AdminBlog`, `AdminCategories`, `AdminCourses`, `AdminTestimonials`, `AdminUsers`) already use.
- **Impact:** Any toolbar change (a11y fix, styling, new page-size option) now has two homes and Billing will silently miss it. Grep confirms Billing is the *only* hand-rolled copy — this is one component-swap away from full consistency.
- **Recommendation:** Replace `Billing.razor:161-181` with `<AdminPaginationToolbar CurrentPage=... />` wired to the existing `GoToPage`/`OnPageSizeChanged` handlers, mirroring `AdminUsers.razor`.

### CQ-3: Two competing ServiceResult→response conventions, mixed within single controllers  [Medium] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/Extensions/ServiceResultExtensions.cs:8-13` — the helper's own doc comment concedes the schism: "Only apply this where the service genuinely puts the failure detail in ErrorMessage — some services (e.g. enrollment) embed the outcome in Value on both success and failure instead, which this helper is not a drop-in replacement for." `src/ResetYourFuture.Web/Controllers/CoursesController.cs` uses both dialects in one class: `result.ToActionResult()` at line 77 vs manual `StatusCode(result.StatusCode, result.Value)` at lines 67 and 87.
- **Impact:** Error payload shape differs by endpoint (bare error string via `ObjectResult(ErrorMessage)` vs a full DTO with `Success=false` inside `Value`), so consumers and the OpenAPI document cannot rely on a single failure contract (API 31 owns the wire-format consequence). For contributors, "how do I return a failure?" has two answers, and picking the wrong one changes behavior silently.
- **Recommendation:** Pick one convention — the `ErrorMessage`-carrying variant plus `ToActionResult()` is the majority — and migrate the Value-embedding services (enrollment/lesson-completion results in `CourseService`) to it, or formalize the second pattern as a distinct result type so the type system distinguishes them.

### CQ-4: TokenService's two mint methods are ~95% identical  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Infrastructure/ApiServices/TokenService.cs:43-75` (`GenerateAccessTokenAsync`) vs `:89-121` (`GenerateImpersonationTokenAsync`) — identical signing, expiration, role/tier lookups, and nine-claim list; the only delta is one extra `impersonatedBy` claim.
- **Impact:** Claim-list edits must be made twice; the file is one forgotten edit away from impersonation tokens lacking a claim regular tokens carry (the cross-*site* version of this problem is ARCH-2 in report 21; this is the within-file instance).
- **Recommendation:** `GenerateImpersonationTokenAsync(user, adminId)` → build claims via a shared private method taking `IEnumerable<Claim> extraClaims`, or fold into one method with an optional `adminId` parameter.

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

### CQ-8: Residual spaced-paren formatting in 3 files, and no automated format gate to prevent regression  [Low] [Effort: S]
- **Evidence:** The legacy `Method( arg )` style — once dominant — survives only at `src/ResetYourFuture.Web/OpenApi/OpenApiExtensions.cs:126-127`, `tests/ResetYourFuture.Web.Tests/ConsumerTests.cs:118`, and `tests/ResetYourFuture.Web.Tests/SiteSettingsIntegrationTests.cs:28`. `.editorconfig` (root, lines 18-27) now mandates no-space parens, but nothing runs `dotnet format` in CI (`.github/workflows/tests.yml` has restore/build/test only).
- **Impact:** Trivial today (4 lines), but the `.editorconfig` rules are advisory — IDE-dependent — so the style can drift back without anyone noticing. The cleanup investment already made deserves a lock.
- **Recommendation:** Fix the 4 residual lines; optionally add a `dotnet format --verify-no-changes` step to the existing workflow (BUILD 40 owns CI design — coordinate there).

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
| CQ-1 | Medium | S | Extract `Localized.Pick(isEl, en, el)` helper; replace ~30 inline ternaries |
| CQ-2 | Medium | S | Swap Billing.razor's hand-rolled toolbar for `AdminPaginationToolbar` |
| CQ-3 | Medium | M | Unify on one ServiceResult failure convention; migrate the Value-embedding services |
| CQ-4 | Low | S | Merge TokenService's twin mint methods via a shared claims builder |
| CQ-5 | Low | S | Extract `ToLocalRedirect()` for the 3 duplicated returnUrl blocks |
| CQ-6 | Low | S | Standardize DTO file grouping (and optionally folder-matching namespaces) |
| CQ-7 | Low | S | Remove the `SortBy="email"` default from generic `PagedResult<T>` |
| CQ-8 | Low | S | Fix 4 residual spaced-paren lines; add a format verification step to CI |
| CQ-9 | Info | S | (Opportunistic) unify interface placement in Web |
| CQ-10 | Info | S | (Opportunistic) file-scope DesignTimeDbContextFactory; clean Home.razor.cs field |

## 5. Related Findings Elsewhere

- **ARCH (21)** owns the cross-file JWT-mint duplication (four sites, drifted claims) of which CQ-4 is the within-file instance, and the two-dialect ServiceResult issue's structural root (services disagreeing on where failure lives).
- **MAINT (23)** owns the macro duplication engine: 18 hand-written consumer+interface pairs mirroring controllers, and 18 hand-maintained resx `Designer.cs` files — file-level repetition that is architectural, not micro.
- **TEST (24)** owns test-code duplication (CustomWebAppFactory's triplicated user-provisioning block) and test-side style.
- **API (31)** owns the wire-format inconsistency that CQ-3 produces (error body shape varying per endpoint).
- **SEC (25)** owns the open-redirect analysis of the returnUrl logic deduplicated in CQ-5.
- **UI (32)** owns visual/a11y correctness of the pagination toolbar component that CQ-2 consolidates.
