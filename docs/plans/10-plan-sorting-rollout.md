# Plan: Sorting Rollout — Every Admin & Student Table

| | |
|---|---|
| Status | Draft |
| Created | 2026-07-11 |
| Depends on | none |
| Related audits | UI (32), UX (33), API (31) — consistency findings reference this plan |

## 1. Context & Goals

- Only **AdminUsers** has end-to-end column sorting; every other Admin/Student table has plain `<th>` headers and a hardcoded server-side `OrderBy`.
- Goal: every important column in every Admin and Student table is sortable **using the established 6-layer pattern** — no new abstractions, no client-side sorting.
- "Done" = clicking any sortable header sorts server-side (correct across pagination pages), toggles asc/desc, shows the ▲/▼ indicator, and is covered by unit tests on the sort extension.

## 2. Current State

**The established pattern (reference implementation — copy this chain verbatim):**

| Layer | Reference file |
|---|---|
| 1. Header component | `src/ResetYourFuture.Web/Shared/Components/Data/SortableColumnHeader.razor` (stateless `<th>`; params `ColumnKey`, `Label`, `CurrentSortBy`, `CurrentSortDir`, `OnSort`) |
| 2. Page state | `src/ResetYourFuture.Web/Pages/AdminUsers.razor.cs:22-23` (`_sortBy = "email"`, `_sortDir = "asc"`), `OnSort` toggle at lines 40-51 (same column → flip dir; new column → asc; reset `currentPage = 1`; reload) |
| 3. Consumer | `src/ResetYourFuture.Web/Consumers/AdminUserConsumer.cs` `GetUsersAsync(page, pageSize, search, sortBy, sortDir)` appends `&sortBy=…&sortDir=…`; interface `IAdminUserConsumer.cs` |
| 4. Controller | `src/ResetYourFuture.Web/Controllers/AdminController.cs:26-40` — `[FromQuery] string sortBy = "email", string sortDir = "asc"` |
| 5. Service | `src/ResetYourFuture.Application/ApiServices/AdminUserService.cs:27-40` — `AsNoTracking()` → `.ApplySearch()` → `CountAsync()` → **`.ApplySort()` → `.Skip().Take()`** |
| 6. EF extension | `src/ResetYourFuture.Domain/Extensions/UserSearchExtensions.cs:13-30` — switch expression over lowered `(sortBy, sortDir)`, never boxes to `object` (keeps SQL translation), always appends a stable `ThenBy` tie-breaker |

Envelope: `src/ResetYourFuture.Application/DTOs/PagedResult.cs` already carries `SortBy`/`SortDir` — reused everywhere, no change needed.
Test pattern: `tests/ResetYourFuture.Domain.Tests/UserSearchExtensionsTests.cs`.
Headers are localized via existing `Col*` resx keys (`AdminRes`, `CategoryRes`, `AssessmentRes`, `BillingRes`) — `SortableColumnHeader` takes a plain `Label`, so **no new resx keys are needed**.

**Tables and their current hardcoded sorts:**

| Page | Service OrderBy today |
|---|---|
| AdminCourses | `AdminCourseService.cs:40` — `OrderByDescending(CreatedAt)` |
| AdminAssessments | `AssessmentService.cs:51` — `OrderBy(TitleEn)` |
| AdminBlog | `BlogArticleService.cs:80` — `OrderByDescending(CreatedAt)` |
| AdminTestimonials | `TestimonialService.cs:46` — `OrderBy(DisplayOrder)` |
| AdminCategories | `AdminCategoryService.cs:22` — `OrderBy(NameEn)` |
| AdminAssessmentSubmissions | `AssessmentService.cs:165` — `OrderByDescending(SubmittedAt)` |
| Student AssessmentHistory | client-side sort of the **full unpaged list** (`AssessmentHistory.razor.cs` `_sortedSubmissions`) |
| Student Billing | server-paged but **bespoke inline `<table>`** + hand-duplicated pagination toolbar (`Billing.razor:114-181`) |

## 3. Design Decisions

| # | Decision | Alternatives rejected | Rationale |
|---|----------|-----------------------|-----------|
| 1 | One `*SearchExtensions.cs` per entity in `src/ResetYourFuture.Domain/Extensions/`, switch-expression style copied from `UserSearchExtensions` | Generic `IQueryable<T>.OrderByProperty(string)` reflection/Expression helper | Reflection-based ordering boxes/breaks EF SQL translation and hides which keys are legal; the repo pattern is explicit and already tested |
| 2 | Every sort ends with a deterministic unique tie-breaker: `ThenBy(x => x.Id)` (users keep `ThenBy(Email)`) | none | Stable ordering across pagination pages — same reason documented in `UserSearchExtensions.cs:7-12` |
| 3 | Sort keys are lowercase-invariant strings matching column semantics (`"createdat"`, `"title"`, …); unknown key falls through to the current default sort | Enum-typed sort keys over the API | Matches the AdminUsers convention; default-fallthrough keeps old bookmarks/consumers working |
| 4 | Multi-valued or computed columns are **not sortable by design**: Roles (multi-valued), Status where it is derived client-side, Actions | Sorting by first role / computed CASE expressions | Low value, high SQL complexity; "important columns" ≠ every column |
| 5 | AdminTestimonials: default stays manual `DisplayOrder`; headers become sortable for viewing, and the manual ↑/↓ reorder buttons are disabled while a non-default sort is active (tooltip: reset sort to reorder) | Making reorder work under arbitrary sort | Reordering semantics only make sense in display order |
| 6 | Count columns (Enrollments, Submissions, CourseCount…) sort via `OrderBy(x => x.Children.Count())` inside the switch | Denormalized count columns | EF translates `Count()` to a correlated subquery — fine at this scale; no schema change |

## 4. Work Items

### ~~WI-1: AdminUsers gap-fill~~ ✅ DONE
Enabled (`isenabled`), EmailConfirmed (`emailconfirmed`), Online (`lastseenat`), and Tier (`tier`, active-subscription subquery) headers are sortable; extension tests cover every key both directions plus a SQL Server translation guard (`ToQueryString`).

### WI-2..7: Full 6-layer chain per admin table

Each item repeats the identical recipe: swap `<th>` → `SortableColumnHeader` + add `_sortBy`/`_sortDir`/`OnSort` to the page partial class; add `sortBy`/`sortDir` params to consumer method + interface; add `[FromQuery] string sortBy = "<default>", string sortDir = "<dir>"` to the controller action; thread through the service replacing the hardcoded `OrderBy` with `.ApplySort(sortBy, sortDir)` placed after `CountAsync` and before `Skip/Take`; create the entity's sort extension with the default matching today's behavior.

| WI | Page / chain | New extension | Sort keys (default bolded) |
|----|--------------|---------------|----------------------------|
| ~~WI-2~~ ✅ | `AdminCourses` — done | `CourseSearchExtensions.cs` | title, category, tier, status, enrollments, **createdat desc** |
| ~~WI-3~~ ✅ | `AdminAssessments` — done (list query lives inline in the controller, not `AssessmentService`; actual pre-existing default was **createdat desc**, preserved) | `AssessmentSearchExtensions.cs` | title, key, category, tier, status, submissions, **createdat desc** |
| ~~WI-4~~ ✅ | `AdminBlog` — done (search param preserved) | `BlogArticleSearchExtensions.cs` | title, slug, author, status, **createdat desc**, publishedat |
| ~~WI-5~~ ✅ | `AdminCategories` — done (count keys exclude soft-deleted children, matching displayed counts) | `CategorySearchExtensions.cs` | **nameen asc**, nameel, coursecount, assessmentcount, createdat |
| WI-6 | `AdminTestimonials.razor` → `IAdminTestimonialConsumer` → `AdminTestimonialsController.cs:35-38` → `TestimonialService.cs:46` — plus Decision 5 (disable ↑/↓ under non-default sort) | `TestimonialSearchExtensions.cs` | **displayorder asc**, name, status, createdat |
| WI-7 | `AdminAssessmentSubmissions.razor` → `IAdminAssessmentConsumer` (submissions method) → `AdminAssessmentsController.cs:314-318` → `AssessmentService.cs:165` | `AssessmentSubmissionSearchExtensions.cs` | user (lastname), email, **submittedat desc** |

- **Acceptance criteria (each):** header click round-trips through the API (visible as `sortBy`/`sortDir` in the request); order correct on page ≥ 2; default sort unchanged when no params sent; extension unit-tested.

### WI-8: Student AssessmentHistory — structural prep, then sort
- **Files:** `src/ResetYourFuture.Web/Pages/AssessmentHistory.razor`(+`.razor.cs`), the mine-submissions consumer + `AssessmentsController` endpoint, `AssessmentService` (mine-submissions query), reuse `AssessmentSubmissionSearchExtensions` from WI-7
- **Change:** (a) convert `GetMySubmissionsAsync()` from full-list to `PagedResult` with `page/pageSize/sortBy/sortDir` (service filters to the authenticated user, then the WI-7 recipe); (b) delete the client-side `_sortedSubmissions`; (c) add `AdminPaginationToolbar` + `SortableColumnHeader`s for Assessment (`title`), Category (`category`), **Submitted (`submittedat` desc, default)**. Keep the "Latest submission" card — it can read the first row of the default sort or its own lightweight call.
- **Acceptance criteria:** history table pages server-side; sorting matches admin behavior; latest-submission card unaffected; EN + EL headers render from existing `AssessmentRes` keys.

### WI-9: Student Billing — migrate to shared table, then sort
- **Files:** `src/ResetYourFuture.Web/Pages/Billing.razor:104-181`(+ code-behind), billing/subscription consumer + controller for the transactions fetch, `BillingTransactionSearchExtensions.cs` (new)
- **Change:** (a) replace the bespoke inline `<table class="transactions-table">` and the hand-duplicated toolbar with `ScrollableTable` + `AdminPaginationToolbar` (keep the `tx-type`/amount cell templates inside `RowTemplate`); (b) thread `sortBy/sortDir` through the transactions endpoint; keys: **createdat desc (default)**, plan, amount. Type/Reference stay unsortable (Decision 4 — cosmetic/opaque values).
- **Acceptance criteria:** visual parity with today (sticky header, max-height scroll); paging + sorting server-side; no duplicated toolbar markup left in `Billing.razor`.

### WI-10: Unit tests for every new sort extension
- **Files:** `tests/ResetYourFuture.Domain.Tests/` — one test class per new `*SearchExtensions`, mirroring `UserSearchExtensionsTests`
- **Change:** for each key: asc + desc ordering asserted on an in-memory list-as-queryable; equal-value case proves the `ThenBy(Id)` tie-breaker; unknown key falls back to the default.
- **Acceptance criteria:** `dotnet test ResetYourFuture.sln` green; each extension file has a matching test class.

## 5. Implementation Order & Dependencies

1. ~~WI-1~~ done.
2. **WI-2 → WI-7** — independent of each other; parallelizable; each is one page's full chain. Do WI-7 before WI-8 (shared extension).
3. **WI-8, WI-9** — structural preps last; they change page composition, not just headers.
4. **WI-10** — written alongside each extension (not deferred to the end).

## 6. Verification

- `dotnet build ResetYourFuture.sln` and `dotnet test ResetYourFuture.sln` green.
- Manual script per table (run once in **EN**, once in **EL** via the culture selector):
  1. Click every sortable header — order flips asc/desc, ▲/▼ indicator moves, `aria-sort` updates.
  2. Navigate to page 2 — ordering continues correctly (tie-breaker check: create two rows with identical sort values).
  3. AdminUsers + AdminBlog: combine active search with sorting — both apply.
  4. AdminTestimonials: apply a name sort → ↑/↓ buttons disable; reset to default order → buttons re-enable and reordering persists.
  5. Billing + AssessmentHistory: confirm paging toolbar counts match totals after the migration.
- Regression: no endpoint changed shape for callers that omit `sortBy`/`sortDir` (defaults reproduce today's ordering).

## 7. Out of Scope

- `AdminAnalytics.razor` summary tables (non-paged aggregates) and `AdminCourseEdit.razor` lesson list (manual curriculum ordering).
- Card grids (`Courses`, `Assessments`, `MyCertificates`) — not tables.
- Any generic/reflection-based sorting abstraction (Decision 1).
- URL/query-string persistence of sort state (would be a new pattern; the established one keeps state in the circuit).
