Concern: Solution structure and dependency direction (Clean Architecture)

Correct way: Use 4 logical layers: `Domain` (entities + domain rules), `Application` (use-cases + ports), `Infrastructure` (EF + external integrations), `Api` (Minimal endpoints) + `Client` (Blazor WASM) + `Shared` (contracts). Only depend inward: `Api -> Application -> Domain`; `Infrastructure -> Application/Domain` via interfaces; `Client -> Shared` only.

False example: `Client` references `Infrastructure` to call EF directly; `Api` references `Client`; entities live in `Api` project.



Concern: “Shared” library purpose

Correct way: Shared holds \*contracts only\*: DTOs, request/response models, validation attributes (if used), enums, common result wrappers, pagination contracts. No EF, no services, no HttpClient logic.

False example: Shared contains `DbContext`, repositories, and business services because “both sides need it”.



Concern: Entities vs DTOs boundary

Correct way: Domain entities are persistence-ignorant and never leave the server boundary. API returns DTOs from `Shared`. Mapping happens in Application layer.

False example: Endpoints return EF entities directly (`return Results.Ok(entity)`), leaking navigation properties and internal fields.



Concern: EF Core in Clean Architecture

Correct way: EF lives only in Infrastructure; `DbContext` implements an `IAppDbContext` abstraction (Application-level) or repositories/unit-of-work ports as needed. Use EF configs via `IEntityTypeConfiguration<T>`.

False example: `DbContext` referenced by Application and Client; LINQ queries spread across endpoints.



Concern: Business logic placement

Correct way: All business rules and workflows live in Application use-cases (command/query handlers or services). Domain contains invariants and behavior on entities. Endpoints are thin: validate -> call use-case -> map result.

False example: Endpoints contain 200 lines of logic: authorization, EF queries, calculations, and mapping.



Concern: SOLID in services (dependency inversion)

Correct way: Application defines interfaces for external concerns (`IEmailSender`, `IPaymentGateway`, `IClock`, `IFileStorage`). Infrastructure implements them. Register via DI.

False example: Application directly calls `new StripeClient(...)` or uses `DateTime.Now` everywhere.



Concern: Avoiding “God services”

Correct way: Use small cohesive services/use-cases per feature (e.g., `EnrollInCourse`, `MarkLessonComplete`, `CreateAssessment`, `PublishCourse`).

False example: `CourseService` contains everything: CRUD, enrollment, progress, billing checks, notifications.



Concern: ViewModels in Blazor WASM (no logic in .razor)

Correct way: Razor components are mostly markup and binding. Put state + actions in ViewModels (classes) injected into components. Use partial class code-behind only for UI glue.

False example: `.razor` contains API calls, mapping, validation logic, and state transitions mixed with markup.



Concern: ViewModel lifecycle and state isolation

Correct way: ViewModels are scoped per page/component instance (not singleton). Use factory pattern or DI scoped services; reset state on navigation.

False example: Singleton ViewModel shared across pages causing data bleed and stale state.



Concern: Client-side API access

Correct way: Typed API clients per feature (`ICoursesApi`, `IAssessmentsApi`, `IBillingApi`). Centralize auth headers, retries, and base URL.

False example: Every ViewModel creates its own `HttpClient` and manually concatenates URLs.



Concern: Validation strategy (client + server)

Correct way: Single source of truth in Shared contracts via validators (FluentValidation in server + optional client adapter). Server enforces always. Client provides UX validation.

False example: Only client validates; server accepts anything, or validation rules differ between client and server.



Concern: Endpoint design in Minimal API

Correct way: Group endpoints by feature using `MapGroup("/api/courses")` etc. Use consistent route conventions, status codes, and ProblemDetails. Endpoints call Application use-cases.

False example: One `Endpoints.cs` with 100 routes; inconsistent routes (`/GetCourse`, `/course/delete`), random status codes.



Concern: DTOs naming and role clarity

Correct way: Use explicit request/response DTOs: `CreateCourseRequest`, `CourseSummaryDto`, `CourseDetailsDto`, `UpdateCourseRequest`. Keep them stable and versionable.

False example: Reusing one `CourseDto` for create/update/read with lots of nullable fields.



Concern: Avoiding redundancy in DTOs

Correct way: Compose DTOs via smaller shared records (e.g., `PagedRequest`, `PagedResponse<T>`, `SortSpec`) and reuse via inheritance/records.

False example: Copy-pasting paging fields into 20 request models.



Concern: Mapping strategy

Correct way: Centralize mapping in Application layer using Mapster/AutoMapper or explicit mapping methods. Keep mapping deterministic and tested.

False example: Mapping scattered across endpoints and ViewModels with duplicated property assignments.



Concern: Query performance + projection

Correct way: Use `Select` projection to DTOs at query time, `AsNoTracking()` for reads, and avoid lazy-loading. Use split queries where needed.

False example: Load full entity graph with `Include` everywhere, then map in memory.



Concern: Entity design (domain invariants)

Correct way: Entities enforce invariants through constructors/methods (e.g., cannot publish a course with zero lessons). Use value objects for strong types (Email, Money) when beneficial.

False example: Public setters on everything and business rules implemented in UI.



Concern: EF navigation properties and serialization

Correct way: Keep navigation properties internal/private where possible; never serialize entities. Configure relationships explicitly.

False example: Public nav properties + returning entities leads to circular JSON and accidental overexposure.



Concern: Concurrency control

Correct way: Use concurrency tokens (rowversion) for updates on sensitive aggregates (courses, assessments). Surface 409 conflicts cleanly.

False example: Last write wins silently, overwriting other admin edits.



Concern: Soft delete vs hard delete

Correct way: Define policy per aggregate (users, courses, transactions). Use soft delete with query filters where audit/history matters.

False example: Hard deleting users/courses breaks foreign keys and analytics.



Concern: Authorization boundaries

Correct way: Authorize in endpoints (policies/roles) and re-check in Application where required for data scoping (tenant/user ownership).

False example: Only hiding admin buttons in UI; endpoints allow actions if called directly.



Concern: Multi-tenant / ownership scoping (if applicable)

Correct way: Always scope queries by current user/tenant in Application queries and repositories.

False example: `db.Courses.Find(id)` without ensuring user can access it.



Concern: Result handling and error model

Correct way: Use a consistent `Result<T>` pattern in Application and translate to HTTP: 400 validation, 401/403 auth, 404 not found, 409 conflict, 422 domain rule, 500 unexpected. Use ProblemDetails.

False example: Throw exceptions for expected flows (not found, validation) and return 200 with `{ success:false }`.



Concern: Transaction boundaries

Correct way: Use one transaction per use-case that changes state; keep it server-side. Use EF `SaveChangesAsync` once per command when possible.

False example: Multiple `SaveChanges` scattered across services or in loops with partial commits.



Concern: Domain events (optional but clean)

Correct way: Emit domain events (e.g., `SubscriptionActivated`, `CourseCompleted`) and handle in Application/Infrastructure (email, notifications, audit).

False example: Use-cases directly call email/notification code inline, tangling side-effects with core logic.



Concern: Integrations (Stripe, email, etc.)

Correct way: Wrap external APIs behind ports with idempotency keys and webhook verification in Infrastructure. Persist external IDs in dedicated tables.

False example: Client calls Stripe directly; server trusts querystring `session\_id` without verification.



Concern: Blazor state + caching redundancies

Correct way: Centralize caching policies (e.g., course catalog cached with ETag) inside API client; ViewModels remain simple.

False example: Each ViewModel implements its own caching and invalidation with duplicated code.



Concern: Clearing redundancies in UI components

Correct way: Create reusable components for repeated patterns (table, modal confirm, toast, pagination). Keep them parameterized and style-consistent.

False example: Copy/paste the same table markup into Users/Courses/Assessments with minor changes.



Concern: Shared enums/strings for status

Correct way: Use enums in Shared (e.g., `CourseStatus`, `AssessmentStatus`) and map from domain; localize display text in UI.

False example: Status is a raw string everywhere (“Published”, “draft”, “PUB”) causing mismatches.



Concern: API versioning readiness

Correct way: Keep DTOs stable; isolate endpoints per version group if needed later (`/api/v1/...`). Use additive changes; avoid breaking rename/removal.

False example: Frequent DTO breaking changes without versioning, forcing client and server to deploy in lockstep only.



Concern: Minimal API composition and testability

Correct way: Endpoint handlers call injected use-case services; keep handlers small and unit-test use-cases. Add integration tests for endpoints.

False example: Logic embedded in lambda endpoints, making testing hard and encouraging duplication.



Concern: Logging and observability

Correct way: Structured logs at boundaries (request start/end, key IDs); correlation IDs; avoid logging PII; audit logs for admin actions.

False example: `Console.WriteLine` everywhere; logs include passwords/emails; no audit for deletes/resets.



Concern: EF migrations and environment safety

Correct way: Migrations generated in Infrastructure; apply via CI/CD or startup with strict controls; never auto-migrate in production without guardrails.

False example: `context.Database.Migrate()` always runs in production startup.



Concern: Data seeding and dev/test parity

Correct way: Deterministic seed scripts for dev; factory methods; test fixtures.

False example: Random seed data in startup leading to flaky tests and inconsistent dev runs.



Concern: Naming and folder conventions (redundancy killer)

Correct way: Feature folders across Application/Api/Client: `Courses`, `Assessments`, `Users`, `Billing`. Keep matching names and clear boundaries.

False example: Mixed organization: some by layer, some by feature, resulting in duplicated “helpers” everywhere.



Concern: Anti-pattern: “Shared” used as dumping ground

Correct way: Periodically enforce rules: Shared contains only contracts; add analyzers or solution-level dependency constraints to prevent forbidden references.

False example: Shared gradually accumulates EF entities, services, and utility singletons because it’s “convenient”.



If you want, I can propose a concrete solution/project layout (folder tree + key interfaces + endpoint grouping) aligned to your current feature set (Courses, Assessments, Users, Billing, Chat) in the same style.



