# Audit: API

| | |
|---|---|
| Finding prefix | API |
| Created | 2026-07-11 |
| Scope | REST conventions, status-code consistency, request validation plumbing, paging conventions, response envelopes (ProblemDetails), OpenAPI/Swagger accuracy, versioning, and endpoint authorization *as convention* — across the 24 controllers in `src/ResetYourFuture.Web/Controllers/`, the 4 minimal-API endpoints in `src/ResetYourFuture.Web/Startup/InfrastructureEndpointsExtensions.cs`, and the OpenAPI pipeline in `src/ResetYourFuture.Web/OpenApi/` |
| Delegated | Individual authorization vulnerabilities & rate-limit/brute-force posture → 25 (SEC). Business-rule correctness of endpoint behavior → 27 (BIZ). Query performance → 34 (PERF). Where an API-facing symptom is rooted in the schema (user-delete 500, missing Conflict from concurrency) → 30 (DB). Server-side sorting → implemented across the list endpoints (former plan 10), not re-reported. |

## 1. Methodology

Read in full: `AuthController`, `CoursesController`, `AdminController`, `ChatController`, `AssistantController`, `SubscriptionController`, `MediaController`, `CertificatesController`, `LessonAssetsController`, `AdminAnalyticsController`, `SiteSettingsController`, `BlogController`, `CategoriesController`, `TestimonialsController`, `ProfileController`, `AssessmentsController`, `AdminAssistantController`, and the seven admin CRUD controllers (Courses, Categories, Modules, Lessons, Blog, Testimonials, Assessments). Also: `Program.cs` (pipeline, exception handler, `UseStatusCodePages`), `ServiceRegistrationExtensions.cs` (`AddProblemDetails`, rate limiters, `AddControllers` — confirmed no `ApiBehaviorOptions` customization exists anywhere), `OpenApi/OpenApiExtensions.cs` (all four transformers), `Application/Common/ServiceResult.cs`, `Web/Extensions/ServiceResultExtensions.cs`, `Application/DTOs/PagedResult.cs`, request DTOs (`Auth/RegisterRequestDto.cs`, `AdminDtos.cs` Save* records — DataAnnotations confirmed present), the four minimal-API endpoints, and paging clamps in `BlogArticleService`, `TestimonialService`, `SubscriptionService`.

NOT examined: SignalR hub method contracts beyond their OpenAPI description (hubs are not REST surface; REL/SEC passes own their behavior); the generated `/openapi/v1.json` at runtime (no app was started — OpenAPI findings are derived from the transformer code plus ApiExplorer semantics); response localization content (owned by UX/COMP).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 5 |
| Info | 3 |

For ~90 endpoints built incrementally, the API surface is in good shape where it was designed deliberately: routes are consistently `api/{resource}` with admin surfaces under `api/admin/*`, every controller carries `[Tags]` grouping, list endpoints share a real `PagedResult<T>` envelope with one clamp semantic via `PagingParams.Normalize`, `AddProblemDetails` + `UseStatusCodePages` + a production exception handler give bare status codes a uniform RFC 7807 body with `traceId` (and the correct `application/problem+json` content type), and the OpenAPI pipeline is unusually mature (per-operation bearer requirements, curated parameter docs, request examples, SSE and SignalR documented in prose, minimal APIs included with summaries). All nine Medium findings (dead ModelState blocks, 400-vs-409 semantics, misleading `CreatedAtAction` targets, OpenAPI schema/content-type accuracy, false `PagedResult` sort defaults, duplicated paging clamps, resource-modeling drift, and inconsistent authorization declarations) are fixed; API-1's remaining sub-item — folding the auth failure envelope and outcome-embedded DTOs into ProblemDetails — is downgraded to Low (see API-1) because it would fight a different, already-documented convention (CQ-3) rather than fix an oversight.

## 3. Findings

### API-1: Auth failure envelope and outcome-embedded DTOs stay outside ProblemDetails  [Low] [Effort: S]
- **Fixed:** `ServiceResultExtensions.ToActionResult(this)` emits an RFC 7807 ProblemDetails (via `ControllerBase.Problem`, carrying the configured `traceId`) for every `ServiceResult` failure. It's now served as `application/problem+json` too — every controller's `[Produces("application/json")]` was widened to `[Produces("application/json", "application/problem+json")]`, since the attribute's result filter was previously forcing all error bodies to the wrong content type regardless of their actual shape.
- **Deliberately not folded in:** `AuthResponseDto { Success=false, Message/Errors }` (the auth failure envelope) and the outcome-embedded DTOs (`EnrollmentResultDto`, `CheckoutSessionDto`, `CancelSubscriptionResultDto`) still bypass ProblemDetails via `ToEmbeddedActionResult`. Its own doc comment (`Web/Extensions/ServiceResultExtensions.cs:18-22`) states this is intentional (CQ-3): callers read the typed DTO's `Success`/`Message` fields even on a 4xx response, so every Razor page consuming auth or these three outcomes depends on the embedded shape surviving failure. Folding them into ProblemDetails would mean rewriting that convention everywhere it's used, not fixing an oversight — out of proportion to a Low finding.
- **Recommendation:** None further; re-open only if the codebase later moves away from the `ToEmbeddedActionResult` convention wholesale (a CQ-3-level decision, not an API one).

### API-10: Success bodies for admin mutations are an inconsistent mix of primitives, strings, and empty 200s  [Low] [Effort: S]
- **Evidence:** `AdminController`: role assign/remove/create return 200 + bare string messages (`ToActionResult` over `ServiceResult<string>`), `DisableUser`/`EnableUser`/`ForcePasswordReset` return bare `true`, `SetPassword` returns an *empty* 200 on success but `ToActionResult` shapes on failure (`AdminController.cs:179-183`). `DeleteUser` and the self-service `ProfileController.DeleteAccount` were fixed under API-8 to return `204 No Content`, and the `toggle-enable` endpoint (previously the one action here returning a proper DTO) was removed under the same fix — narrowing what's left to the role/disable/enable/set-password primitives. Elsewhere mutations return either the updated DTO (testimonials, blog) or 204 (publish/unpublish).
- **Impact:** Bare `true`/string JSON bodies are un-evolvable (adding a field later is a breaking change from primitive to object) and force clients into per-endpoint handling.
- **Recommendation:** Converge on: mutation returns the updated resource DTO, or 204 when there's nothing to say. Cheap to do while touching AdminController for API-8.

### API-11: CancellationToken plumbing is inconsistent across endpoints  [Low] [Effort: S]
- **Evidence:** Most endpoints accept and forward `CancellationToken`, but whole controllers don't: `AdminModulesController`, `AdminAnalyticsController`, `ProfileController`, and `CertificatesController` (`GetMyCertificates`/`Issue`/`Download` — `CertificatesController.cs:50-135`, note the un-parameterized `ToListAsync()` at line 68), plus scattered actions (`AdminAssessmentsController` GETs, `ChatController.StartConversation`/`GetAvailableUsers`/`GetUnreadCount`, `AdminLessonsController`).
- **Impact:** Abandoned requests (user navigates away mid-list) keep executing their queries; minor at this scale but free to fix and the inconsistency spreads by copy-paste.
- **Recommendation:** Add `CancellationToken cancellationToken = default` to every async action and thread it to EF/service calls; consider an analyzer (CA2016 is already relevant) in the build.

### API-12: Public endpoints reuse admin DTO types  [Low] [Effort: S]
- **Evidence:** Anonymous `GET /api/testimonials` returns `AdminTestimonialDto` (`TestimonialsController.cs:26-31`), whose shape (`DTOs/Testimonials/AdminTestimonialDto.cs:6-17`) includes `IsActive`, `CreatedAt`, `UpdatedAt` — admin bookkeeping fields meaningless (and always `true`) on the public feed.
- **Impact:** Public contract is coupled to the admin grid: any admin-side field addition leaks to the anonymous surface automatically; schema name "AdminTestimonialDto" in the public OpenAPI section is also just confusing.
- **Recommendation:** Add a slim `TestimonialDto` (name, role, quote, avatar, order) for the public endpoint — mirroring the existing student-vs-admin DTO split used for assessments and courses.

### API-13: State-changing GET for email confirmation, aimed at a JSON API route  [Low] [Effort: S]
- **Evidence:** `GET /api/auth/confirm-email` mutates state (confirms the address) and returns `AuthResponseDto` JSON (`AuthController.cs:46-52`); `RegisterAsync` builds the emailed link directly to this API action via `Url.Action("ConfirmEmail", "Auth", ...)` (`AuthController.cs:38-39`).
- **Impact:** Safe-method semantics are violated (link prefetchers/scanners can consume the confirmation token), and a real user clicking the emailed link lands on raw JSON rather than a page. Dev flows mask this via the DEBUG-only self-confirm endpoints.
- **Recommendation:** Point the emailed link at a Blazor confirmation page that POSTs the token to the API; keep GET only as the page's landing. (User-facing flow polish is UX-33's domain; the method semantics are the API-side fix.)

### API-14: No API versioning strategy beyond the "v1" document name  [Info] [Effort: S]
- **Evidence:** Routes are unversioned (`api/{resource}`); the only "v1" is the OpenAPI document name (`OpenApiExtensions.cs:19`, `Program.cs:33`). No `Asp.Versioning` package referenced.
- **Impact:** Acceptable — the only consumers are the app's own SSR consumers and Swagger; per 00-INDEX calibration this cannot rate higher. Worth a one-line ADR so the choice reads as deliberate.
- **Recommendation:** Document "single-version API, breaking changes allowed" in the README/architecture notes; revisit only if third-party consumers appear.

### API-15: OpenAPI/Swagger served in Development only  [Info] [Effort: S]
- **Evidence:** `Program.cs:22-39` — `MapOpenApi()` and `UseSwaggerUI` sit inside `if (app.Environment.IsDevelopment())`; the `#if DEBUG` dev auth endpoints (`AuthController.cs:134-160`) therefore appear only in documents no production build serves.
- **Impact:** Deliberate and sensible (smaller prod surface); noted so nobody files "docs missing in prod" as a bug. If the API were ever offered to external consumers, publish the generated `v1.json` as a build artifact instead of exposing the UI.
- **Recommendation:** None required.

### API-16: Certificate verification returns 200 with Valid=false for unknown IDs  [Info] [Effort: S]
- **Evidence:** `CertificatesController.cs:161-165` — unknown `verificationId` returns `200 OK` + `CertificateVerificationDto(false, ..., "Certificate not found.")` rather than 404; revoked certificates likewise 200 with `Valid=false` (lines 167-179).
- **Impact:** Defensible design (verification is a query about validity, not resource retrieval, and it avoids status-code-based enumeration signals), but it departs from the 404 convention used everywhere else, so it should be an explicit, documented choice.
- **Recommendation:** Keep the behavior; state it in the endpoint's XML summary so the OpenAPI description says a 200 always comes back with a boolean verdict.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| API-1 | Low | S | None further — auth failure envelope and outcome DTOs deliberately stay outside ProblemDetails (CQ-3) |
| API-10 | Low | S | Converge mutation success bodies on DTO-or-204 |
| API-11 | Low | S | Thread CancellationToken through all async actions |
| API-12 | Low | S | Public `TestimonialDto` instead of AdminTestimonialDto |
| API-13 | Low | S | Confirmation link → page + POST; stop mutating on GET |
| API-14 | Info | S | One-line ADR: unversioned single-consumer API |
| API-15 | Info | S | (No action) Dev-only Swagger is deliberate |
| API-16 | Info | S | Document the 200-with-verdict verification contract |

## 5. Related Findings Elsewhere

- **30 (DB):** DB-7 — concurrency tokens now exist on every admin-edited aggregate, with `AdminLessonsController` demonstrating the `DbUpdateConcurrencyException` → 409 mapping now used consistently across uniqueness/conflict handling (the other six controllers still need the same three-line wiring); DB-8 — AnswersJson/SchemaJson now have matching DTO/column caps, closing the unbounded-payload gap.
- **25 (SEC):** endpoint-level authorization *vulnerabilities* (anonymous Stripe webhook signature-skip when no secret is configured, JWT accepted via query string for `/api/lessons` and hubs, media/asset access rules, impersonation endpoint hardening) and the globally shared fixed-window "auth" rate limiter (one bucket for all clients).
- **26 (REL):** behavior of the self-calling SSR HttpClient consumers when API responses are non-success (`ApiClientBase` swallowing failures), and rate-limiter availability effects.
- **27 (BIZ):** correctness of enrollment/checkout/certificate business outcomes those endpoints return (e.g. mock-payment plan assignment).
- **28 (DQ):** validation of request *content* beyond shape — JSON payload well-formedness, business-rule field validation.
- **33 (UX):** the user-facing consequence of API-13 (email link landing on JSON) and error-message localization in responses.
- **44 (DOC):** README endpoint tables staying in sync with the actual ~90-endpoint surface.
