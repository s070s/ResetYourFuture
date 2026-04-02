# Code Audit — ResetYourFuture Solution

> Scanned: 2026-04-02 | Scope: Api, Web, Client, Shared projects

---

## Projects
- `ResetYourFuture.Api` — ASP.NET Core REST API
- `ResetYourFuture.Web` — Razor Pages / Blazor Server frontend
- `ResetYourFuture.Client` — Blazor WASM client
- `ResetYourFuture.Shared` — DTOs, shared models

---

## CRITICAL

### 1. Hardcoded Credentials & JWT Key in appsettings.json
- **Files:** `src/ResetYourFuture.Api/appsettings.json`, `src/ResetYourFuture.Web/appsettings.json`
- `AdminUser:Password = "Admin123!"` and `Jwt:Key = "CHANGE_THIS_IN_PRODUCTION..."` committed to source control
- `Program.cs` fallback defaults use the same weak values if config is missing
- **Fix:** Use User Secrets in dev + Azure Key Vault / environment variables in prod. Remove all defaults — fail startup if any required key is missing.

### 2. XSS via Unsanitized `MarkupString`
- **Files:** `Client/Pages/BlogArticle.razor:54`, `CourseDetail.razor:27,84`, `LessonViewer.razor:76`
- Rich HTML from the Quill editor is rendered with `@((MarkupString)content)` — no sanitization applied
- Also: `wwwroot/js/quill-interop.js` uses `quill.root.innerHTML = content` instead of the safer `quill.setContents()`
- **Fix:** Run content through `HtmlSanitizer` (NuGet) server-side before storing. Use `quill.setContents()` in JS.

### 3. No Rate Limiting on Auth Endpoints
- **Files:** `Api/Program.cs`, `Web/Program.cs`
- No rate limiting anywhere — `/api/auth/login` and `/api/auth/register` are open to brute-force attacks
- **Fix:** Add `AddRateLimiter()` with a per-IP fixed-window policy targeting auth endpoints.

### 4. No Global Exception Handling Middleware
- **Files:** `Api/Program.cs`, `Web/Program.cs`
- Unhandled exceptions may expose stack traces to clients in production if `DeveloperExceptionPage` is accidentally active
- **Fix:** Add `app.UseExceptionHandler(...)` for production; return a generic `ProblemDetails` response.

### 5. Email Confirmation & Password Reset Not Implemented (TODOs in prod)
- **Files:** `Api/Controllers/AuthController.cs:76,101,227` (duplicated in `Web/Controllers/AuthController.cs`)
- Confirmation URL is **logged to console** instead of emailed; password reset URL is never sent; parental consent is skipped with a comment
- **Fix:** Implement `IEmailService` properly. At minimum, stop leaking the confirmation URL in the API response/logs.

---

## HIGH

### 6. Silent Exception Swallowing in ChatService
- **Files:** `Client/Services/ChatService.cs:61-64,89,105,120,138,168,179`
  `Web/Services/ChatService.cs:69,97,113,128,146,176,187`
- 12+ bare `catch (HttpRequestException) { }` and `catch (Exception) { }` blocks — no logging, no user feedback
- SignalR connection failures silently dispose the hub; API call failures return empty lists
- **Fix:** Log at `LogError` level with context (userId, operation). Surface error state to the calling component.

### 7. `async void` Event Handlers
- **Files:** `Client/Layout/ImpersonationBanner.razor.cs:23`, `Client/Layout/AvatarDropdown.razor.cs:30`
  `Web/Layout/ImpersonationBanner.razor.cs:24`, `Web/Layout/AvatarDropdown.razor.cs:33`
- `async void` methods silently crash the process on unhandled exceptions
- **Fix:** Change return type to `async Task`. Wrap body in try/catch if needed.

### 8. `.Result` Blocking Calls — Deadlock Risk in Blazor Server
- **Files:** `Client/Pages/Courses.razor.cs:35`, `Client/Pages/CourseDetail.razor.cs:36`
  `Web/Pages/Courses.razor.cs:35`, `Web/Pages/CourseDetail.razor.cs:36`
- `.Result` used inside `OnInitializedAsync` — can deadlock in Blazor Server's synchronization context
- **Fix:** Replace with `await` after `Task.WhenAll()`.

### 9. Bare `catch` in AssessmentsController Schema Parsing
- **File:** `Api/Controllers/AssessmentsController.cs:107-110`
- `catch { return schemaJson; }` — parsing failure is completely invisible; stale data silently returned
- **Fix:** Log the exception before returning the fallback.

### 10. Certificate Auto-Generation Failure Not Surfaced
- **File:** `Api/Controllers/CoursesController.cs:365-375`
- Failed certificate generation is logged at `Warning` level only; client receives no indication
- **Fix:** Use `LogError`; consider returning a soft error indicator in the response.

---

## MEDIUM — Performance & Design

### 11. Direct `DbContext` in Controllers (Mixed Responsibility)
- **Files:** `Api/Controllers/CoursesController.cs:70-90`, `Api/Controllers/ChatController.cs:68-120`
  `Api/Controllers/AdminCoursesController.cs:85-107`
- Controllers inject `ApplicationDbContext` directly and compose complex LINQ queries
- Scatters business/query logic across the controller layer; makes unit testing very hard
- **Fix:** Move query logic into service classes.

### 12. N+1 / Multiple DB Queries Per Request
- **File:** `Api/Controllers/ChatController.cs:68-120`
  — 3 separate queries for one `GET /conversations`: conversations → unread counts → user roles
- **File:** `Api/Controllers/CoursesController.cs:102-118`
  — `LessonCompletions` fetched in a second query after loading the full course tree
- **Fix:** Combine using `Include`/`ThenInclude` or a single `Join`/`SelectMany` projection.

### 13. Duplicate `Program.cs` Service Registration
- **Files:** `Api/Program.cs:124-131` and `Web/Program.cs:170-177`
- Identity config, JWT setup, and authorization policy definitions are copy-pasted verbatim between both startup files
- **Fix:** Extract into `IServiceCollection` extension methods in a shared infrastructure project.

### 14. 15+ Near-Identical Consumer Classes
- **Files:** `Client/Consumers/CourseConsumer.cs`, `AssessmentConsumer.cs`, `BlogConsumer.cs` + 12 admin variants
- Every consumer re-implements the same `GetAsync`/`PostAsync` scaffolding
- **Fix:** Create a single generic `ApiClient<T>` base class.

### 15. `AuditableEntity` Missing `CreatedByUserId` and Auto-Assignment
- **File:** `Api/Domain/Entities/AuditableEntity.cs`
- `CreatedByUserId` is absent; `UpdatedAt` is not automatically set in `SaveChangesAsync`
- **Fix:** Override `SaveChangesAsync` in `ApplicationDbContext` to auto-set `CreatedAt`/`UpdatedAt` based on entity state.

### 16. No Soft Delete on Any Entity
- All deletes are permanent — no `IsDeleted` / `DeletedAt` fields on any entity
- No audit trail for deletions; problematic for GDPR compliance
- **Fix:** Add `IsDeleted`/`DeletedAt` to `AuditableEntity`. Add a global EF query filter.

### 17. `DataProtection` Keys Not Persisted
- **File:** `Web/Program.cs:238` — `AddDataProtection()` with no key storage configured
- Keys are lost on container/process restart; breaks encrypted cookies and sessions
- **Fix:** Add `.PersistKeysToDbContext<ApplicationDbContext>()` or file-based key storage.

### 18. SignalR — No Maximum Message Size
- **File:** `Api/Hubs/ChatHub.cs`
- No `MaximumReceiveMessageSize` limit configured — large messages can exhaust server memory
- **Fix:** `services.AddSignalR(o => o.MaximumReceiveMessageSize = 32_000);`

### 19. Missing Indexes on High-Frequency Foreign Key Columns
- `Enrollment.(UserId, CourseId)`, `LessonCompletion.UserId`, `ChatMessage.ConversationId`, `Module.CourseId`, `Lesson.ModuleId`
- No explicit `HasIndex()` calls found for these columns in entity configurations
- **Fix:** Add indexes in the relevant `IEntityTypeConfiguration` classes.

### 20. Hardcoded Sitemap Base URL
- **File:** `Web/Program.cs:535` — `const string baseUrl = "https://reset-your-future.com";`
- Staging / preview deployments will always generate production URLs in the sitemap
- **Fix:** `config["Sitemap:BaseUrl"] ?? throw new InvalidOperationException(...)`

---

## LOW — Code Quality & Maintainability

### 21. Zero Test Projects
- No unit, integration, or E2E tests exist in the solution
- Critical paths (auth, enrollment, assessments, billing) are entirely untested
- **Fix:** Add `ResetYourFuture.Api.Tests` project; start with service-layer and controller unit tests.

### 22. `Console.WriteLine` Instead of `ILogger` (37 occurrences)
- **Files:** `Client/Pages/*.razor.cs`, `Web/Pages/*.razor.cs`
- `catch (Exception ex) { Console.WriteLine(ex.Message); }` — console output is invisible in production log sinks
- **Fix:** Inject `ILogger<T>` and replace with `_logger.LogError(ex, "message")`.

### 23. Only 11 of 32 DTOs Have Validation Attributes
- **Files:** `Shared/DTOs/Blog/SaveBlogArticleRequest.cs`, `SaveCourseRequest.cs`, `SaveModuleRequest.cs`, + ~18 others
- Free-text fields have no `[MaxLength]` — a single request could submit gigabytes of data
- **Fix:** Add `[Required]`, `[MinLength]`, `[MaxLength]` to all request DTOs.

### 24. Magic Numbers for Lesson Content Type
- **File:** `Api/Controllers/CoursesController.cs:47-50`
- Returns `1`, `2`, `3` for lesson type with only inline comments to explain them
- **Fix:** Create a `LessonContentType` enum (`Text = 1, Video = 2, Pdf = 3`).

### 25. Admin Impersonation Not Logged
- **File:** `Web/Program.cs:395-508`
- No structured log entry when an admin impersonates a user — hides potential unauthorized access
- **Fix:** `_logger.LogWarning("Admin {AdminId} impersonated user {UserId}", adminId, userId);`

### 26. `DeserializeFeatures` Silently Returns `null` on Corrupted JSON
- **File:** `Api/Services/SubscriptionService.cs:322-337`
- `catch { return null; }` — corrupted subscription feature data is silently swallowed
- **Fix:** `_logger.LogError(ex, "Failed to deserialize features for plan {PlanId}", planId);`

### 27. CORS Overly Permissive Headers and Methods
- **File:** `Api/Program.cs:154-159`
- Origin is correctly restricted, but `AllowAnyHeader()` + `AllowAnyMethod()` are broader than necessary
- **Fix:** Replace with explicit `.WithHeaders(...)` and `.WithMethods(...)`.

---

## Quick Wins (each under 1 hour)

| # | Task |
|---|------|
| 1 | Replace 37× `Console.WriteLine` with `ILogger` calls |
| 2 | Add `HtmlSanitizer` NuGet; sanitize content before storing |
| 3 | Add `AddRateLimiter()` for `/auth/login` and `/auth/register` |
| 4 | Add `app.UseExceptionHandler(...)` for production in both `Program.cs` files |
| 5 | Log exceptions in all empty `catch` blocks in `ChatService` |
| 6 | Fix 4× `async void` → `async Task` |
| 7 | Fix 4× `.Result` → `await` (after `Task.WhenAll`) |
| 8 | Create `LessonContentType` enum to replace magic numbers `1, 2, 3` |
| 9 | Add impersonation log entry in `Web/Program.cs` |
| 10 | Log deserialization failure in `SubscriptionService.DeserializeFeatures` |
