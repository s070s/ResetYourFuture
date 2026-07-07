# Categories for Courses & Assessments — chips, admin config, browse/filter

## Context

Course and Assessment CRUD is fully built end-to-end (Domain → EF → Application services → API controllers → Consumers → Blazor pages), but there is **no Category concept anywhere** — no entity, DTO, UI, or chip component. The goal: admins classify courses and assessments with categories (created inline while authoring content, plus a management page), each card shows a category chip, and students filter/search the public Courses and Assessments pages easily.

**Confirmed decisions:** one category per item (nullable FK), one shared category pool for both content types, inline create + dedicated admin page, enhance the existing public pages (no new catalog page).

## Key design decisions

- **D1 Entity:** `Category : AuditableEntity` — `Guid Id`, `required string NameEn` (max 100), `string? NameEl` (max 100), inverse navs to Courses/AssessmentDefinitions. Inheriting `AuditableEntity` gets the global soft-delete query filter for free. `IsPublished` is ignored — categories are always visible.
- **D2 Delete:** admin delete = soft delete; in the same save, explicitly null `CategoryId` on referencing courses/assessments (tracked-entity loop, not `ExecuteUpdate`, so SQLite tests behave identically). DB backstop: `OnDelete(DeleteBehavior.SetNull)` on both FKs. Content becomes "uncategorized", never hidden.
- **D3 Uniqueness:** no filtered unique DB index (`HasFilter` SQL is provider-specific and breaks SQLite tests). Plain index on `NameEn` + case-insensitive `AnyAsync` check among non-deleted rows in the admin service (same pattern as assessment `Key` uniqueness in `AdminAssessmentsController`).
- **D4 Inline create:** `SaveCourseRequest`/`SaveAssessmentDefinitionRequest` gain trailing defaulted params `Guid? CategoryId = null, [MaxLength(100)] string? NewCategoryName = null`. If `NewCategoryName` is non-blank it wins: server trims + get-or-creates by case-insensitive `NameEn`. One atomic request — no orphan categories, no two-step race. Shared helper `AdminCategoryService.ResolveCategoryAsync(db, categoryId, newCategoryName, ct)` reused by `AdminCourseService` and `AdminAssessmentsController`.
- **D5 Public chips endpoint:** `GET api/categories?scope=courses|assessments&lang=` (`[Authorize]`) → `List<CategoryDto>(Id, Name, Count)` — only categories with ≥1 published item in scope, language-resolved, ordered by name. "All" chip is client-side.
- **D6 Service style:** mirror the Course pattern — `CategoryService` (public) + `AdminCategoryService` (CRUD) in Application, thin controllers. Do NOT refactor `AdminAssessmentsController`'s controller-direct style; just thread category through it (~25 lines).
- **D7 Chip UI:** shared `CategoryFilterBar.razor` component (chips + search box; visuals cloned from `.tier-selector`/`.tier-option` in `AdminCourseEdit.razor.css`); per-card chip is CSS-only — `.category-chip` in `wwwroot/css/shared-components.css` (cross-page rule), modeled on `.tier-badge`.
- **D8 Search:** server-side `search` + `categoryId` query params on the existing public list endpoints, applied before `CountAsync` so pagination totals stay correct. 300 ms debounce in page code-behind (copy `AdminUsers.razor.cs` CTS pattern; implement `IDisposable`).

## Implementation steps (dependency order)

### Step 1 — Domain
- **Create** `src/ResetYourFuture.Domain/Domain/Entities/Category.cs` (namespace `ResetYourFuture.Domain.Entities` — repo quirk).
- **Modify** `Course.cs` and `AssessmentDefinition.cs` (same folder): add `Guid? CategoryId` + `Category? Category` nav.

### Step 2 — EF Core + migration
- **Create** `src/ResetYourFuture.Infrastructure/Data/Configurations/CategoryConfiguration.cs` — max lengths, non-unique index on `NameEn`.
- **Modify** `CourseConfiguration.cs`, `AssessmentDefinitionConfiguration.cs` — FK with `OnDelete(SetNull)`, index on `CategoryId`.
- **Modify** `src/ResetYourFuture.Infrastructure/Data/ApplicationDbContext.cs` **and** `src/ResetYourFuture.Application/Data/IApplicationDbContext.cs` — add `DbSet<Category> Categories`.
- `dotnet ef migrations add AddCategories --project src/ResetYourFuture.Infrastructure --startup-project src/ResetYourFuture.Web` (auto-applies at startup). **Immediately check `git status` on `Directory.Packages.props`/csproj** — the restore can silently pin an incompatible Microsoft.OpenApi; `git checkout` those files if touched.

### Step 3 — DTOs (`src/ResetYourFuture.Application/DTOs/`)
- **Create** `Categories/CategoryDtos.cs`: `CategoryDto(Guid Id, string Name, int Count)`; `AdminCategoryDto(Id, NameEn, NameEl, CourseCount, AssessmentCount, CreatedAt)`; `SaveCategoryRequest([Required, MaxLength(100)] NameEn, [MaxLength(100)] NameEl)`.
- **Modify** `Courses/CourseListItemDto.cs`: append `Guid? CategoryId = null, string? CategoryName = null`.
- **Modify** `AdminDtos.cs`: `AdminCourseDto` + category fields; `SaveCourseRequest` + the two D4 params (trailing, defaulted — then grep `new SaveCourseRequest(` and update call sites deliberately).
- **Modify** `AssessmentDtos.cs`: same for `AssessmentDefinitionDto` (public: `CategoryId`/`CategoryName`), `AdminAssessmentDefinitionDto`, list-item DTO (`CategoryNameEn` for admin table), `SaveAssessmentDefinitionRequest`.
- **Modify** `Seed/` course + assessment seed DTOs: add `string? Category`.

### Step 4 — Application services
- **Create** `ApiInterfaces/ICategoryService.cs`, `IAdminCategoryService.cs`; `ApiServices/CategoryService.cs` (D5 grouped counts query), `ApiServices/AdminCategoryService.cs` (paged list with counts, create/rename with D3 check, soft-delete per D2, static `ResolveCategoryAsync` per D4).
- **Modify** `ApiServices/CourseService.cs` `GetPublishedCoursesAsync`: add `Guid? categoryId, string? search` (filter base query before `CountAsync`; search over TitleEn/El + DescriptionEn/El), project `CategoryId` + language-resolved `CategoryName`.
- **Modify** `ApiServices/AssessmentService.cs` `GetPublishedAssessmentsAsync`: same; also thread category through `GetAssessmentAsync` (shared DTO).
- **Modify** `ApiServices/AdminCourseService.cs`: create/update call `ResolveCategoryAsync`; `MapToDto` + list/get projections gain category fields.
- **Modify** matching interfaces `ICourseService.cs`, `IAssessmentService.cs`, `IAdminCourseService.cs`.

### Step 5 — Controllers (`src/ResetYourFuture.Web/Controllers/`)
- **Create** `CategoriesController.cs` — `[Authorize]`, `api/categories`, single GET (D5).
- **Create** `AdminCategoriesController.cs` — `[Authorize(Policy = "AdminOnly")]`, `api/admin/categories`: GET paged, GET all (lightweight, for editor dropdowns), POST, PUT `{id}`, DELETE `{id}`; `ServiceResult` + `ToActionResult()` so duplicate-name 400 flows the RFC 7807 path.
- **Modify** `CoursesController.cs` + `AssessmentsController.cs` public list actions: add `[FromQuery] Guid? categoryId`, `[FromQuery] string? search`.
- **Modify** `AdminAssessmentsController.cs`: create/update call `ResolveCategoryAsync`; add category to its DTO mapping sites and list projection. `AdminCoursesController.cs` needs no changes.

### Step 6 — Consumers + DI (`src/ResetYourFuture.Web/`)
- **Create** `Consumers/ICategoryConsumer.cs`/`CategoryConsumer.cs` and `IAdminCategoryConsumer.cs`/`AdminCategoryConsumer.cs`, both `: ApiClientBase`.
- **Modify** `Consumers/ICourseConsumer.cs`/`CourseConsumer.cs`, `IAssessmentConsumer.cs`/`AssessmentConsumer.cs`: optional `categoryId`/`search` params (`Uri.EscapeDataString` the search).
- **Modify** `Startup/ServiceRegistrationExtensions.cs`: register both services + two consumer HttpClients with `.AddHttpMessageHandler<SsrApiHandler>()`.

### Step 7 — Admin UI
- **Create** `Pages/AdminCategories.razor` + `.razor.cs` + `.razor.css` — `/admin/categories`, `[Authorize(Roles = "Admin")]`; reuse `ScrollableTable` + `SortableColumnHeader` + `AdminPaginationToolbar` + `ConfirmModal` + `DismissibleAlert` (template: `AdminUsers` incl. debounced search). Columns: NameEn, NameEl, CourseCount, AssessmentCount, Created, actions. Delete confirm warns "N courses and M assessments will become uncategorized."
- **Modify** `Pages/AdminCourseEdit.razor(.cs)`: inject `IAdminCategoryConsumer`; category `<select>` below the tier selector — "— None —", each category, "+ New category…" revealing a text input; extend the `SaveCourseRequest` construction; after save, reload the list and select the returned `CategoryId`.
- **Modify** `Pages/AdminAssessmentEdit.razor(.cs)`: identical picker on `SaveAssessmentDefinitionRequest`.

### Step 8 — Public UI
- **Create** `Shared/Components/CategoryFilterBar.razor` + `.razor.css` (D7): params `Categories`, `SelectedCategoryId`, `OnCategorySelected`, `SearchText`, `OnSearchChanged`, `SearchPlaceholder`; accessible chips (`.selected`, `:focus-within`).
- **Modify** `Pages/Courses.razor(.cs)(.css)`: load categories alongside existing `Task.WhenAll` in `OnInitializedAsync`; chip click → `_page = 1` + reload; 300 ms debounced search; pass both to the consumer; `IDisposable` for the CTS. **Restructure the `TotalCount == 0` branch**: when a filter/search is active show the filter bar + "no matches" message (otherwise the chip bar disappears and traps the user); keep the full empty state only with no active filter. Card header gets `<span class="category-chip">` when non-null.
- **Modify** `Pages/Assessments.razor(.cs)`: same (filter bar only when the user has assessment access; chip on cards).
- **Modify** `wwwroot/css/shared-components.css`: add `.category-chip` (tier-badge-style pill, theme tokens only).

### Step 9 — Nav + localization
- **Modify** `Layout/NavMenu.razor`: `<li><NavLink href="admin/categories">` in the Admin `AuthorizeView` block; new `Categories` key in `NavMenuRes.resx`/`.el.resx`/`.Designer.cs`.
- **Create** `src/ResetYourFuture.Shared/Resources/CategoryRes.resx` + `.el.resx` + **hand-written** `CategoryRes.Designer.cs` (copy `CourseRes.Designer.cs` shape; keys: AllCategories, SearchPlaceholder, NoMatchingResults, NewCategory, NoneCategory, name labels, DeleteConfirmFormat, count headers…). Register in `ResetYourFuture.Shared.csproj` like the others.
- **Modify** `Messages/ErrorMessagesRes.*` (+Designer): `CategoryNameExists`, `CategoryNotFound`.

### Step 10 — Seed data (Dev-only)
- **Modify** `src/ResetYourFuture.Infrastructure/Seeding/CourseSeeder.cs` + `AssessmentSeeder.cs`: get-or-create category by `NameEn` from the seed DTO, cached in a run-local dictionary to avoid duplicate adds.
- **Modify** the JSON files in `src/ResetYourFuture.Shared/JSON/Courses/` + `JSON/Assessments/` with a `"category"` field (e.g. Career, Mindset, Skills, Finance, Wellbeing). Note: seeders skip when data exists, so existing dev DBs won't pick these up.

### Step 11 — Tests (`tests/`)
- **Create** `ResetYourFuture.Application.Tests/AdminCategoryServiceTests.cs` (create, case-insensitive duplicate rejection, rename, soft-delete nulls FKs, get-or-create reuse) and `CategoryServiceTests.cs` (scope filter, published-only counts, language resolution, zero-count exclusion).
- **Modify** `CourseServiceTests.cs` (+ assessment equivalent): `categoryId`/`search`/combined filters, `TotalCount` correctness, DTO carries `CategoryName`. `AdminCourseServiceTests.cs`: save with `CategoryId` / with `NewCategoryName` / with neither.
- **Create** `ResetYourFuture.Web.Tests/CategoriesIntegrationTests.cs` via `CustomWebAppFactory`; add `api/admin/categories` routes to the admin auth-matrix test.

## Risks / gotchas
- **OpenApi pin trap** after `dotnet ef`/restore — check `git status`, revert csproj/props if touched.
- **SQL Server vs SQLite duality**: no `HasFilter`, no `ExecuteUpdate` in delete path; don't test Greek case-insensitive search (SQLite `Contains` is ASCII-only case-insensitive).
- **Positional record appends**: defaulted trailing params compile everywhere, but grep all construction sites anyway — a forgotten site silently saves null category.
- **Prerender double-fetch**: keep consistent with neighboring pages (Courses currently has no guard — don't invent a new pattern mid-feature).
- **Hand-edited Designer.cs**: add resx key + Designer property in the same commit; alphabetical order.
- Keep all files under 500 lines (largest riser: `AdminAssessmentEdit.razor.cs` ~301 → ~340, fine).

## Verification
1. `dotnet build ResetYourFuture.sln` and `dotnet test` from repo root.
2. `dotnet run --project src/ResetYourFuture.Web` once — confirm `AddCategories` migration applies and seeders run in logs.
3. Manual E2E: admin creates "Mindset" on `/admin/categories` → edits a course, inline-creates "Career Skills", saves, publishes → categories page shows CourseCount 1 → assessment editor picks the *existing* "Career Skills" (no duplicate) → as student, `/courses` shows chip bar "All / Career Skills (1)", chip click filters + resets to page 1, debounced search works, combined filter+search works, pagination inside a filter works; Greek culture shows `NameEl` fallback → `/assessments` same; Free student sees upgrade prompt, no filter bar → admin renames then deletes the category: confirm-modal warns, content becomes uncategorized but stays visible under "All".
