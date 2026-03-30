# Blog Articles Feature — End-to-End Plan

A Blog Articles section on the public Home/Landing page, fully editable from the Admin Panel (CRUD), with example seed data defined in the Shared project.

---

## Architecture Overview

```
ResetYourFuture.Shared       → DTOs, request models, seed content definitions
ResetYourFuture.Api
  Domain/Entities            → BlogArticle entity
  Data/Configurations        → EF IEntityTypeConfiguration<BlogArticle>
  Data/ApplicationDbContext  → DbSet<BlogArticle>
  Interfaces                 → IBlogArticleService
  Services                   → BlogArticleService, BlogArticleSeeder
  Controllers                → BlogController (public), AdminController (blog routes added)
  Program.cs                 → DI + seeder wired
ResetYourFuture.Client
  Interfaces                 → IBlogConsumer, IAdminBlogConsumer
  Consumers                  → BlogConsumer, AdminBlogConsumer
  Pages                      → AdminBlog, AdminBlogEditor, BlogArticle
  Pages/Home.razor           → Blog section inserted
  Program.cs                 → consumers registered
```

Flow: Client consumers → API controllers → IBlogArticleService → ApplicationDbContext → BlogArticle entity

---

## Phase 1 — ResetYourFuture.Shared

> Contracts only: DTOs, request models, and seed content definitions.

### New Files

**`DTOs/Blog/BlogArticleSummaryDto.cs`**
Lightweight card DTO for the landing page grid.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | |
| `Title` | `string` | |
| `Slug` | `string` | URL-safe identifier |
| `Summary` | `string` | Truncated for card display |
| `CoverImageUrl` | `string?` | Optional |
| `AuthorName` | `string` | |
| `Tags` | `string[]` | Deserialized from JSON |
| `PublishedAt` | `DateTimeOffset?` | |

---

**`DTOs/Blog/BlogArticleDto.cs`**
Full article DTO for the single-article view page.

Extends `BlogArticleSummaryDto` fields and adds:

| Property | Type | Notes |
|---|---|---|
| `Content` | `string` | Full body — HTML or Markdown |

---

**`DTOs/Blog/AdminBlogArticleDto.cs`**
Admin read DTO — full article plus audit fields.

Extends `BlogArticleDto` fields and adds:

| Property | Type | Notes |
|---|---|---|
| `IsPublished` | `bool` | |
| `CreatedAt` | `DateTimeOffset` | |
| `UpdatedAt` | `DateTimeOffset?` | |

---

**`DTOs/Blog/SaveBlogArticleRequest.cs`**
Single request record for both create and update.

| Property | Type | Notes |
|---|---|---|
| `Title` | `string` | Required |
| `Slug` | `string` | Required, URL-safe, unique |
| `Summary` | `string` | Required |
| `Content` | `string` | Required |
| `CoverImageUrl` | `string?` | Optional |
| `AuthorName` | `string` | Required |
| `Tags` | `string[]?` | Optional |
| `IsPublished` | `bool` | |

---

**`DTOs/Blog/BlogSeedData.cs`**
Static class containing a hardcoded `IReadOnlyList<SaveBlogArticleRequest>` with 3–5 example articles.

Lives in `Shared` so seed content (titles, body text, cover URLs) is co-located with the contracts and requires no EF reference. The `BlogArticleSeeder` in the Api project reads from this list directly.

Example articles should cover topics relevant to the platform (career guidance, mindset, job search strategies) written in both English and Greek tones.

---

## Phase 2 — ResetYourFuture.Api — Domain

### New File: `Domain/Entities/BlogArticle.cs`

Inherits `AuditableEntity` (provides `CreatedAt`, `UpdatedAt`).

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `Title` | `string` | Required, max 200 |
| `Slug` | `string` | Required, max 220, unique index |
| `Summary` | `string` | Required, max 500 |
| `Content` | `string` | Full body, no length cap at DB level |
| `CoverImageUrl` | `string?` | Optional, max 500 |
| `AuthorName` | `string` | Required, max 100 |
| `Tags` | `string?` | JSON-serialized `string[]`, nullable |
| `IsPublished` | `bool` | Default `false` |
| `PublishedAt` | `DateTimeOffset?` | Stamped when first published |

---

## Phase 3 — ResetYourFuture.Api — Data / Infrastructure

### New File: `Data/Configurations/BlogArticleConfiguration.cs`

Implements `IEntityTypeConfiguration<BlogArticle>`:
- Primary key on `Id`
- Unique index on `Slug` — named `IX_BlogArticles_Slug`
- Index on `IsPublished` + `PublishedAt` (composite) for landing page query performance — named `IX_BlogArticles_Published_PublishedAt`
- Max-length constraints matching the entity field table above
- Default value `false` for `IsPublished`
- `PublishedAt` nullable

Picked up automatically by `builder.ApplyConfigurationsFromAssembly(...)` already in `OnModelCreating`.

### Changes to `ApplicationDbContext.cs`

Add one `DbSet`:

```
public DbSet<BlogArticle> BlogArticles => Set<BlogArticle>();
```

### EF Migration

After both changes above:

```
Add-Migration AddBlogArticles
Update-Database
```

---

## Phase 4 — ResetYourFuture.Api — Application Layer

### New File: `Interfaces/IBlogArticleService.cs`

| Method | Return type | Notes |
|---|---|---|
| `GetPublishedSummariesAsync(int count, CancellationToken)` | `IReadOnlyList<BlogArticleSummaryDto>` | Landing page cards — latest N published, ordered by `PublishedAt desc` |
| `GetPublishedBySlugAsync(string slug, CancellationToken)` | `BlogArticleDto?` | Single article view |
| `GetAllForAdminAsync(int page, int pageSize, string? search, CancellationToken)` | `PagedResult<AdminBlogArticleDto>` | Admin list — all articles, paginated |
| `GetByIdForAdminAsync(Guid id, CancellationToken)` | `AdminBlogArticleDto?` | Admin edit form prefill |
| `CreateAsync(SaveBlogArticleRequest, CancellationToken)` | `AdminBlogArticleDto` | Creates entity, checks slug uniqueness |
| `UpdateAsync(Guid id, SaveBlogArticleRequest, CancellationToken)` | `AdminBlogArticleDto?` | Updates entity, re-checks slug uniqueness |
| `PublishAsync(Guid id, CancellationToken)` | `bool` | Sets `IsPublished = true`, stamps `PublishedAt` if not already set |
| `UnpublishAsync(Guid id, CancellationToken)` | `bool` | Sets `IsPublished = false` |
| `DeleteAsync(Guid id, CancellationToken)` | `bool` | Hard delete |

### New File: `Services/BlogArticleService.cs`

Implements `IBlogArticleService`.

- Injected: `ApplicationDbContext`, `ILogger<BlogArticleService>`
- Uses `AsNoTracking()` on all read queries
- Mapping entity → DTO is explicit inline — no external mapper needed at this scope
- Slug uniqueness check on Create: query `BlogArticles.AnyAsync(x => x.Slug == request.Slug)` — service returns a signal for the controller to respond with `409 Conflict`
- Slug uniqueness check on Update: same query excluding the current article's own `Id`
- `Tags` field: serialized as JSON string in entity, deserialized to `string[]` in DTO mapping

---

## Phase 5 — ResetYourFuture.Api — API Endpoints

### New File: `Controllers/BlogController.cs`

Public routes — no `[Authorize]`. Thin controller, delegates all logic to `IBlogArticleService`.

| Method | Route | Description | Response |
|---|---|---|---|
| `GET` | `/api/blog/summaries?count=6` | Landing page cards | `200 IReadOnlyList<BlogArticleSummaryDto>` |
| `GET` | `/api/blog/{slug}` | Full article by slug | `200 BlogArticleDto` or `404` |

### Changes to `AdminController.cs`

Add blog endpoints inside the existing `[Authorize(Policy = "AdminOnly")]` class. Same injection pattern — add `IBlogArticleService` to the constructor.

| Method | Route | Description | Response |
|---|---|---|---|
| `GET` | `/api/admin/blog` | Paginated list (`page`, `pageSize`, `search`) | `200 PagedResult<AdminBlogArticleDto>` |
| `GET` | `/api/admin/blog/{id}` | Single article | `200 AdminBlogArticleDto` or `404` |
| `POST` | `/api/admin/blog` | Create | `201 AdminBlogArticleDto` or `409` on duplicate slug |
| `PUT` | `/api/admin/blog/{id}` | Update | `200 AdminBlogArticleDto` or `404` or `409` |
| `POST` | `/api/admin/blog/{id}/publish` | Publish | `204` or `404` |
| `POST` | `/api/admin/blog/{id}/unpublish` | Unpublish | `204` or `404` |
| `DELETE` | `/api/admin/blog/{id}` | Delete | `204` or `404` |

### Changes to `Program.cs`

Register the service:

```
services.AddScoped<IBlogArticleService, BlogArticleService>()
```

---

## Phase 6 — ResetYourFuture.Api — Seeder

### New File: `Services/BlogArticleSeeder.cs`

Static class, `SeedAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct)`:

- Idempotent: returns early if `await db.BlogArticles.AnyAsync(ct)` is true
- Reads seed articles from `BlogSeedData.SeedArticles` (defined in `ResetYourFuture.Shared`)
- Maps each `SaveBlogArticleRequest` to a `BlogArticle` entity
- Sets `IsPublished = true` and `PublishedAt = DateTimeOffset.UtcNow` for all seed records
- Assigns deterministic `Guid` IDs derived from the slug (e.g., `GuidV5` or `Guid.NewGuid()` on first seed) so re-runs remain idempotent
- Inserts with `db.BlogArticles.AddRangeAsync(...)` then `db.SaveChangesAsync(ct)`

### Changes to `Program.cs`

Add seeder call in the existing seeding block, alongside `SubscriptionPlanSeeder`:

```
await BlogArticleSeeder.SeedAsync(db, logger, ct);
```

---

## Phase 7 — ResetYourFuture.Client — Consumers

### New File: `Interfaces/IBlogConsumer.cs`

| Method | Return type |
|---|---|
| `GetSummariesAsync(int count, CancellationToken)` | `IReadOnlyList<BlogArticleSummaryDto>?` |
| `GetBySlugAsync(string slug, CancellationToken)` | `BlogArticleDto?` |

### New File: `Consumers/BlogConsumer.cs`

Implements `IBlogConsumer`. Typed `HttpClient` hitting `/api/blog/*`. Follows the same response-check pattern as `AdminCourseConsumer` (`response.IsSuccessStatusCode ? ReadFromJsonAsync : null`).

---

### New File: `Interfaces/IAdminBlogConsumer.cs`

| Method | Return type |
|---|---|
| `GetArticlesAsync(int page, int pageSize, string? search, CancellationToken)` | `PagedResult<AdminBlogArticleDto>?` |
| `GetArticleAsync(Guid id, CancellationToken)` | `AdminBlogArticleDto?` |
| `CreateArticleAsync(SaveBlogArticleRequest, CancellationToken)` | `AdminBlogArticleDto?` |
| `UpdateArticleAsync(Guid id, SaveBlogArticleRequest, CancellationToken)` | `AdminBlogArticleDto?` |
| `PublishArticleAsync(Guid id, CancellationToken)` | `bool` |
| `UnpublishArticleAsync(Guid id, CancellationToken)` | `bool` |
| `DeleteArticleAsync(Guid id, CancellationToken)` | `bool` |

### New File: `Consumers/AdminBlogConsumer.cs`

Implements `IAdminBlogConsumer`. Typed `HttpClient` hitting `/api/admin/blog/*`. Identical structural pattern to `AdminCourseConsumer`.

### Changes to `Client/Program.cs`

Register both consumers alongside existing consumer registrations:

```
builder.Services.AddHttpClient<IBlogConsumer, BlogConsumer>(...)
builder.Services.AddHttpClient<IAdminBlogConsumer, AdminBlogConsumer>(...)
```

---

## Phase 8 — ResetYourFuture.Client — Admin Pages

### New Files: `Pages/AdminBlog.razor` + `Pages/AdminBlog.razor.cs`

Route: `/admin/blog`

**Code-behind state:**
- `PagedResult<AdminBlogArticleDto>?` for the table
- `currentPage`, `pageSize`, `PageSizeOptions` (same pattern as `AdminUsers`)
- `searchTerm` with debounce via `CancellationTokenSource`
- `string message` for user feedback
- `Guid? _pendingDeleteId` for inline delete confirmation

**Behaviour:**
- `OnInitializedAsync` calls `LoadArticles()`
- Table columns: Cover (thumbnail), Title, Slug, Status badge (Published / Draft), Published At, Actions
- Status badge uses a green/grey chip — green for published, grey for draft
- Actions per row: **Edit** (navigates to `/admin/blog/{id}`), **Publish / Unpublish** toggle (calls respective consumer method then reloads), **Delete** (shows inline confirm, same pattern as `AdminUsers`)
- Top bar: search input + debounce CTS, page size selector, **"New Article"** button navigates to `/admin/blog/new`
- Pagination: Previous / Next using `HasNextPage` from `PagedResult`

---

### New Files: `Pages/AdminBlogEditor.razor` + `Pages/AdminBlogEditor.razor.cs`

Routes: `/admin/blog/new` (create) and `/admin/blog/{Id:guid}` (edit)

**Code-behind state:**
- `[Parameter] Guid? Id` — `null` means create mode
- `SaveBlogArticleRequest` as the bound form model
- `bool _isEditMode` derived from `Id`
- `bool _isBusy` to disable the submit button while in-flight
- `string? _error` for feedback

**Behaviour:**
- `OnInitializedAsync`: if `_isEditMode`, calls `GetArticleAsync(Id)` and maps result into the form model
- Auto-generate `Slug` from `Title` on input (lowercase, replace spaces with `-`, strip special chars) — editable after auto-generation
- Form fields: Title, Slug, Summary (textarea, max 500), Content (textarea, full body), Cover Image URL (text input), Author Name, Tags (comma-separated text input, split on save), IsPublished (checkbox)
- **Save** button: calls Create or Update based on mode — on success navigates back to `/admin/blog`, on `409` shows "Slug already in use" inline error
- **Cancel** button navigates back to `/admin/blog`

---

## Phase 9 — ResetYourFuture.Client — Landing Page

### Changes to `Home.razor`

Insert a new `<section class="section section-blog">` between the **Story section** and the **CTA section**, inside the `<NotAuthorized>` block.

**Behaviour:**
- `IBlogConsumer` injected in the code-behind
- `OnInitializedAsync` calls `GetSummariesAsync(count: 6)` and stores result in `IReadOnlyList<BlogArticleSummaryDto>? _blogSummaries`
- Section is only rendered when `_blogSummaries is { Count: > 0 }` — no empty state shown to public
- Loading state: skeleton placeholder cards matching the card height (3 placeholders on desktop)
- Card grid: 3 columns on desktop, 2 on tablet, 1 on mobile
- Each card renders:
  - Cover image (or a CSS gradient placeholder when `CoverImageUrl` is null)
  - Tags as small chips
  - Title
  - Summary (line-clamp to 3 lines)
  - Author name and formatted `PublishedAt` date
  - **"Read More"** button navigates to `/blog/{Slug}`
- Use `@key="article.Id"` on each card for stable diffing

---

### New Files: `Pages/BlogArticle.razor` + `Pages/BlogArticle.razor.cs`

Route: `/blog/{Slug}`

**Behaviour:**
- `[Parameter] string Slug` bound from route
- `OnParametersSetAsync` calls `IBlogConsumer.GetBySlugAsync(Slug)` and stores in `BlogArticleDto? _article`
- If `_article` is null: renders a styled "Article not found" state with a link back to `/`
- If loaded: renders full article layout — cover image, title, author + date row, tags row, full content rendered as `MarkupString` (assuming HTML stored in `Content`)
- Page title set via `<PageTitle>` using article title
- Loading state shows skeleton layout

---

## Phase 10 — Navigation

### Admin Sidebar / Nav

- Add **Blog Articles** nav item in the Admin section alongside Courses, Users, Analytics
- Route: `/admin/blog`
- Icon: document or pencil SVG (consistent with existing admin nav icons)

### Public Nav (if applicable)

- Optionally add a **Blog** anchor link in the landing page nav bar pointing to `/#blog` section
- The individual article pages are reachable via cards only (no dedicated `/blog` index page in this phase)

---

## New Files Summary

```
ResetYourFuture.Shared/
  DTOs/Blog/
    BlogArticleSummaryDto.cs
    BlogArticleDto.cs
    AdminBlogArticleDto.cs
    SaveBlogArticleRequest.cs
    BlogSeedData.cs

ResetYourFuture.Api/
  Domain/Entities/
    BlogArticle.cs
  Data/Configurations/
    BlogArticleConfiguration.cs
  Interfaces/
    IBlogArticleService.cs
  Services/
    BlogArticleService.cs
    BlogArticleSeeder.cs
  Controllers/
    BlogController.cs
  [Modified] Controllers/AdminController.cs
  [Modified] Data/ApplicationDbContext.cs
  [Modified] Program.cs

ResetYourFuture.Client/
  Interfaces/
    IBlogConsumer.cs
    IAdminBlogConsumer.cs
  Consumers/
    BlogConsumer.cs
    AdminBlogConsumer.cs
  Pages/
    AdminBlog.razor
    AdminBlog.razor.cs
    AdminBlogEditor.razor
    AdminBlogEditor.razor.cs
    BlogArticle.razor
    BlogArticle.razor.cs
  [Modified] Pages/Home.razor
  [Modified] Program.cs
```

---

## Implementation Order

```
1. Shared DTOs + BlogSeedData
        ↓
2. Domain Entity + EF Configuration + Migration
        ↓
3. IBlogArticleService + BlogArticleService
        ↓
4. BlogController (public) + AdminController (blog routes)
        ↓
5. BlogArticleSeeder + Program.cs DI wiring
        ↓
6. Client Consumers + Interfaces + Client Program.cs registration
        ↓
7. Admin Pages — AdminBlog (list) + AdminBlogEditor (create/edit)
        ↓
8. Landing page Blog section (Home.razor) + BlogArticle.razor page
        ↓
9. Navigation links (Admin sidebar + public nav anchor)
```

Each step is independently deployable. Steps 7, 8, and 9 can be worked on in parallel once step 6 is done.
