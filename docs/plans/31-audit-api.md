# Audit: API

| | |
|---|---|
| Finding prefix | API |
| Created | 2026-07-11 |
| Scope | REST conventions, status-code consistency, request validation plumbing, paging conventions, response envelopes (ProblemDetails), OpenAPI/Swagger accuracy, versioning, and endpoint authorization *as convention* — across the 24 controllers in `src/ResetYourFuture.Web/Controllers/`, the 4 minimal-API endpoints in `src/ResetYourFuture.Web/Startup/InfrastructureEndpointsExtensions.cs`, and the OpenAPI pipeline in `src/ResetYourFuture.Web/OpenApi/` |
| Delegated | Individual authorization vulnerabilities & rate-limit/brute-force posture → 25 (SEC). Business-rule correctness of endpoint behavior → 27 (BIZ). Query performance → 34 (PERF). Where an API-facing symptom is rooted in the schema (user-delete 500, missing Conflict from concurrency) → 30 (DB). Server-side sorting rollout → existing plan `10-plan-sorting-rollout.md`, not re-reported. |

## 1. Methodology

Read in full: `AuthController`, `CoursesController`, `AdminController`, `ChatController`, `AssistantController`, `SubscriptionController`, `MediaController`, `CertificatesController`, `LessonAssetsController`, `AdminAnalyticsController`, `SiteSettingsController`, `BlogController`, `CategoriesController`, `TestimonialsController`, `ProfileController`, `AssessmentsController`, `AdminAssistantController`, and the seven admin CRUD controllers (Courses, Categories, Modules, Lessons, Blog, Testimonials, Assessments). Also: `Program.cs` (pipeline, exception handler, `UseStatusCodePages`), `ServiceRegistrationExtensions.cs` (`AddProblemDetails`, rate limiters, `AddControllers` — confirmed no `ApiBehaviorOptions` customization exists anywhere), `OpenApi/OpenApiExtensions.cs` (all four transformers), `Application/Common/ServiceResult.cs`, `Web/Extensions/ServiceResultExtensions.cs`, `Application/DTOs/PagedResult.cs`, request DTOs (`Auth/RegisterRequestDto.cs`, `AdminDtos.cs` Save* records — DataAnnotations confirmed present), the four minimal-API endpoints, and paging clamps in `BlogArticleService`, `TestimonialService`, `SubscriptionService`.

NOT examined: SignalR hub method contracts beyond their OpenAPI description (hubs are not REST surface; REL/SEC passes own their behavior); the generated `/openapi/v1.json` at runtime (no app was started — OpenAPI findings are derived from the transformer code plus ApiExplorer semantics); response localization content (owned by UX/COMP).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 9 |
| Low | 4 |
| Info | 3 |

> **Fixed since audit (partial — downgraded High → Medium):** API-1 (error responses had at least five competing body shapes and two content types) — every generic error is now a single RFC 7807 ProblemDetails **body**: `ServiceResultExtensions.ToActionResult` routes all 28 `ServiceResult` failures through `ControllerBase.Problem` (with the configured `traceId` extension), and the scattered `text/plain` string bodies and anonymous `{ message }`/`{ error }` objects across ~11 controllers (certificates, lesson assets, media, site settings, admin blog/lessons/testimonials, subscription webhook, etc.) were rewritten to `Problem(...)`. What keeps it Medium rather than closed: on controllers carrying `[Produces("application/json")]` the ProblemDetails body is served with an `application/json` content-type header instead of `application/problem+json` (the body shape is identical — a client parses one envelope), and three deliberate business-outcome DTOs (enroll 403, checkout 400, cancel 400) plus the auth `AuthResponseDto` failure envelope are intentionally left as typed contracts (the auth ModelState half is dead code owned by API-2).

For ~90 endpoints built incrementally, the API surface is in good shape where it was designed deliberately: routes are consistently `api/{resource}` with admin surfaces under `api/admin/*`, every controller carries `[Tags]` grouping, list endpoints share a real `PagedResult<T>` envelope with clamped paging, `AddProblemDetails` + `UseStatusCodePages` + a production exception handler give bare status codes a uniform RFC 7807 body with `traceId`, and the OpenAPI pipeline is unusually mature (per-operation bearer requirements, curated parameter docs, request examples, SSE and SignalR documented in prose, minimal APIs included with summaries). The error half of the contract used to be the dominant weakness — five different error body shapes — but generic errors now share one ProblemDetails envelope (API-1, mostly fixed); what remains is per-controller conflict/status divergence, `[ApiController]` auto-validation silently superseding the hand-written ModelState envelopes, and a handful of documented response types that don't match what actions actually return.

## 3. Findings

### API-1: Residual error-shape inconsistencies after the ProblemDetails unification  [Medium] [Effort: S]
- **Fixed:** `ServiceResultExtensions.ToActionResult(this)` now emits an RFC 7807 ProblemDetails (via `ControllerBase.Problem`, carrying the configured `traceId`) for every `ServiceResult` failure — one change covering all 28 call sites. The `text/plain` string bodies and anonymous `{ message }`/`{ error }` objects (certificates, lesson assets, media, site settings, admin blog/lessons/testimonials, the Stripe webhook, the checkout 503) were rewritten to `Problem(...)`. A generic client can now parse one error envelope for all of these.
- **Residual evidence:** (a) On controllers carrying `[Produces("application/json")]` (most of them), the `ProblemDetails` error body is served as `application/json` rather than `application/problem+json` — the `[Produces]` result filter overrides the media type. The body shape is identical, so this is a content-type header nicety, not a shape divergence. (b) `AuthResponseDto { Success=false, Message/Errors }` remains the auth failure envelope (`AuthController.cs:36,62,78,104`, `AuthApiService.cs`); the ModelState half is dead code (see API-2). (c) Three deliberate outcome-embedded DTOs at error status: `CoursesController.cs` enroll 403 with `EnrollmentResultDto`, `SubscriptionController.cs` checkout 400 with `CheckoutSessionDto`, cancel 400 with `CancelSubscriptionResultDto` — structured business outcomes the UI reads, not generic errors.
- **Recommendation:** Optional polish: drop `[Produces("application/json")]` (or add `application/problem+json`) so error content-types match their bodies; fold the auth failure envelope into ProblemDetails when API-2's dead ModelState blocks are removed; decide per-endpoint whether the three outcome DTOs should move to 200-with-verdict (like API-16's verification contract) or ProblemDetails.

### API-2: Hand-written ModelState checks are dead code — the real 400 contract is auto ValidationProblemDetails  [Medium] [Effort: S]
- **Evidence:** All 24 controllers are `[ApiController]` and no `ApiBehaviorOptions`/`SuppressModelStateInvalidFilter` configuration exists anywhere in `src/` (verified by search). Request DTOs carry DataAnnotations (`Auth/RegisterRequestDto.cs:9-42`, `AdminDtos.cs:60-66,87-90,116-120`), so invalid bodies are rejected by the framework *before* the action runs with a `ValidationProblemDetails` body. The manual blocks in `AuthController.cs:35-36, 61-62, 77-78, 103-104` that build `AuthResponseDto { Errors = ModelState... }` therefore never execute.
- **Impact:** Misleading source of truth: the controller code (and the OpenAPI request examples suggesting AuthResponseDto errors) implies one 400 shape while clients actually receive another. Anyone "fixing" error text in those dead blocks will see no effect.
- **Recommendation:** Delete the manual ModelState blocks and embrace the automatic `ValidationProblemDetails` (which conveniently matches API-1's target envelope). Document the 400 schema via `[ProducesResponseType(typeof(ValidationProblemDetails), 400)]` or the ParameterAndResponseDocsTransformer.

### API-3: Conflict/uniqueness and rejection semantics differ per controller for the same situation  [Medium] [Effort: S]
- **Evidence:** Duplicate blog slug → **409** (`AdminBlogController.cs:66,716` region — `Conflict(...)`); duplicate assessment key → **400** (`AdminAssessmentsController.cs:121,187`); duplicate category name → **400** (`AdminCategoryService.cs:51,75`). Root cause: `ServiceResult` (`Application/Common/ServiceResult.cs:11-17`) has factories for 200/201/204/400/401/403/404 but none for 409, so services shoehorn conflicts into BadRequest. Similarly divergent rejection styles: admin-enrollment block returns `StatusCode(403, dto)` (`CoursesController.cs:63-64`) while other forbidden paths use bare `Forbid()`-equivalents or `StatusCode(403, string)` (`CertificatesController.cs:86-87`, `LessonAssetsController.cs`), and checkout-unavailable is a 503 with an ad-hoc body (`SubscriptionController.cs:82-86`).
- **Impact:** Clients can't distinguish "fix your input" (400) from "retry with a different name" (409); UI code ends up string-matching error messages. The OpenAPI response docs advertise 409 as "already exists" (`OpenApiExtensions.cs:227`) — true for exactly one controller.
- **Recommendation:** Add `ServiceResult<T>.Conflict(...)` and use 409 for every uniqueness violation (assessment key, category name, slug); reserve 400 for validation. Fold the status mapping into the API-1 ProblemDetails work so shape and code land together.

### API-4: CreatedAtAction targets list endpoints, producing misleading Location headers  [Medium] [Effort: S]
- **Evidence:** `AdminAssessmentsController.cs:165` — `CreatedAtAction(nameof(GetAssessments), new { id = ... })`: `GetAssessments` is the paged *list*, so the Location resolves to `/api/admin/assessments?id={guid}` instead of `/api/admin/assessments/{guid}` even though `GetAssessmentById` exists. `AdminModulesController.cs:114` points at `GetModulesByCourse` although `GetModuleById` exists. `AdminLessonsController.cs:112` points at `GetLessonsByModule` — and here no single-lesson admin GET exists at all (the edit UI must reload the whole module list). Correct usage exists in the same folder to copy from: `AdminCoursesController.cs:58`, `AdminBlogController.cs:68`, `AdminTestimonialsController.cs:61`.
- **Impact:** 201 Location headers — the one REST affordance clients are entitled to follow — point at collections with a bogus query string; the missing `GET /api/admin/lessons/{id}` is a resource-model gap.
- **Recommendation:** Point assessments/modules at their by-id GETs; add `GET /api/admin/lessons/{id:guid}` and reference it. Trivial diffs, copy the AdminCourses pattern.

### API-5: OpenAPI document inaccuracies — declared types diverge from actual payloads  [Medium] [Effort: M]
- **Evidence:** (a) `AdminAssessmentsController` Create/Update are declared `ActionResult<AssessmentDefinitionDto>` (the *student* DTO with `Title`/`Description`) but return `AdminAssessmentDefinitionDto` (bilingual fields, SchemaJson) — the documented 200 schema for Update is simply wrong; Create's 201 attribute is right but the action signature still advertises the wrong T. (b) `POST /api/assistant/chat` (`AssistantController.cs:29-37`) returns `IResult`/`TypedResults.ServerSentEvents` with no `Produces` metadata — documented as a bare 200 with no `text/event-stream` content type or `AssistantStreamEvent` schema; only the document-info prose (`OpenApiExtensions.cs:83-89`) explains it, and that prose describes the chat hub but never mentions `/hubs/call`. (c) File-serving endpoints (`MediaController`, `LessonAssetsController`, certificate download, avatar GET, site background) declare `ProducesResponseType(200)` with no content type while the controller-level default elsewhere claims `application/json`. (d) `ProfileController.GetAvatar` XML doc says "current or specified user" but the action takes no user parameter. (e) Every list endpoint's response schema documents `sortBy`/`sortDir` defaults of `"email"`/`"asc"` regardless of resource (see API-6).
- **Impact:** The Swagger UI is a first-class deliverable of this project (rich transformers, examples, persistent auth); consumers generating clients from `/openapi/v1.json` get compile-time-wrong types for admin assessments and no usable contract for the SSE stream.
- **Recommendation:** Fix the two `ActionResult<T>` generic types; add `[ProducesResponseType(typeof(AssistantStreamEvent), 200, "text/event-stream")]` (or `.Produces` metadata) to the SSE action plus a sentence about `/hubs/call` in the doc prose; give file endpoints explicit content types (`application/pdf`, `image/*`, `application/octet-stream`); correct the GetAvatar comment.

### API-6: PagedResult envelope leaks admin-user sorting defaults into every list response  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Application/DTOs/PagedResult.cs:11-12` — the generic envelope defaults `SortBy = "email"`, `SortDir = "asc"`. Only `GET /api/admin/users` actually accepts/uses sort parameters (`AdminController.cs:31-32`); all other constructors omit them (e.g. `AdminAssessmentsController.cs:74,352`, `ChatQueryService`, `AdminCategoryService.cs:35`), so course, chat, blog, testimonial, assessment and billing lists all serialize `"sortBy": "email"` — a field none of them sorts by or even contains.
- **Impact:** Actively false response metadata; a client that echoes the envelope's sort state back (the intended pattern for the users grid) would send nonsense to other endpoints. Will get worse as `10-plan-sorting-rollout.md` adds real sorting per resource.
- **Recommendation:** Make `SortBy`/`SortDir` nullable with no defaults (omit when the endpoint doesn't sort), or move them to a derived `SortedPagedResult<T>` used only by endpoints that implement sorting — align with the sorting rollout plan rather than duplicating its work.

### API-7: Paging clamps are duplicated with divergent semantics and inconsistent placement  [Medium] [Effort: S]
- **Evidence:** Style A clamps oversize to the max: `Math.Clamp(pageSize, 1, 100)` (`AdminController.cs:35-36`, `ChatController.cs:34-35,51-52`, `AdminCoursesController.cs:43-44`, `AdminCategoriesController.cs:30-31`). Style B *resets* oversize to 10: `if (pageSize < 1 || pageSize > 100) pageSize = 10` (`CoursesController.cs:39-40`, `AssessmentsController.cs:37`, `AdminAssessmentsController.cs:51,321`, `SubscriptionService.cs:352-353`). Placement also varies: usually the controller, but blog/testimonials clamp in the service instead (`BlogArticleService.cs:59-60`, `TestimonialService.cs:38-39`) leaving their controllers pass-through (`AdminBlogController.cs:38-43`, `AdminTestimonialsController.cs:37-41`), and `SubscriptionController.GetBillingOverview` (215-219) relies solely on the service.
- **Impact:** `pageSize=150` returns 100 items from some endpoints and 10 from others — surprising for any shared client paging component; every new list endpoint re-decides both the rule and the layer, and one will eventually forget (nothing enforces the clamp for a controller whose service doesn't duplicate it).
- **Recommendation:** Introduce one `PagingParams` (record with `Normalize()` or a model-binder) used by every list action; pick one semantic (clamp-to-max is friendlier) and document it in the OpenAPI parameter text (`OpenApiExtensions.cs:208` already promises "1–100").

### API-8: Resource-modeling drift — RPC verbs, split resources, and one DELETE that isn't like the others  [Medium] [Effort: M]
- **Evidence:** (a) `AdminController` exposes `POST users/{id}/toggle-enable` *and* `POST users/{id}/disable` *and* `POST users/{id}/enable` (`AdminController.cs:96-161`) — three endpoints mutating the same flag, the first non-idempotent and racy for two concurrent admins. (b) Lessons live under two roots: detail at `GET /api/courses/lessons/{lessonId}` (`CoursesController.cs:73`) but assets at `GET /api/lessons/{lessonId}/asset` (`LessonAssetsController.cs:15,41`). (c) The admin-only site upload sits at `POST /api/site/admin/background-image` (`SiteSettingsController.cs:16,65`) instead of under the `api/admin/*` prefix every other admin surface uses. (d) `DELETE /api/admin/users/{userId}` returns 200 + a string ("User deleted.") via `ToActionResult` (`AdminController.cs:106-111`) while every other DELETE in the codebase returns 204. (e) Assorted RPC verbs (`move-up`/`move-down`, `set-password`, `force-password-reset`, `impersonate`) are pragmatic and fine — listed only to note the *pair* `toggle-X` + `X`/`un-X` is the pattern worth pruning.
- **Impact:** Consumers can't predict URLs or DELETE semantics; the `api/site/admin/*` outlier also weakens the "everything admin lives under /api/admin" review heuristic that SEC audits rely on.
- **Recommendation:** Drop `toggle-enable` (keep the idempotent enable/disable pair); move background upload to `api/admin/site/background-image`; return 204 from user delete; long-term, expose lesson detail and assets under one root.

### API-9: Authorization declaration conventions are inconsistent (default-allow controllers)  [Medium] [Effort: S]
- **Evidence:** Convention elsewhere is class-level `[Authorize]`/`[Authorize(Policy = "AdminOnly")]` with explicit `[AllowAnonymous]` opt-outs (Certificates, Courses, Chat, all Admin*). But `SubscriptionController` (`SubscriptionController.cs:16-21`) and `SiteSettingsController` (`SiteSettingsController.cs:16-21`) have no class-level attribute and secure individual actions — any newly added action is anonymous by default. Cosmetic variants of the same theme: `CategoriesController.cs:9-16` XML-docs itself "Public category discovery" while carrying `[Authorize]`; `AuthController.Refresh` carries a redundant `[AllowAnonymous]` (line 73) on an anonymous controller.
- **Impact:** Convention-level only (concrete authorization vulnerabilities, including the deliberately anonymous webhook and JWT-in-query-string for `/api/lessons` assets, are delegated to SEC-25) — but default-allow controllers are exactly where the next missed `[Authorize]` comes from, and the misleading doc comments erode review trust.
- **Recommendation:** Put `[Authorize]` at class level on Subscription and SiteSettings with `[AllowAnonymous]` on `GetPlans`/webhook/`GetBackgroundImage`; fix the Categories doc comment; drop the redundant AllowAnonymous.

### API-10: Success bodies for admin mutations are an inconsistent mix of primitives, strings, and empty 200s  [Low] [Effort: S]
- **Evidence:** `AdminController`: role assign/remove/create return 200 + bare string messages (`ToActionResult` over `ServiceResult<string>`), `DisableUser`/`EnableUser`/`ForcePasswordReset` return bare `true`, `SetPassword` returns an *empty* 200 on success but `ToActionResult` shapes on failure (`AdminController.cs:179-183`), `ToggleEnable` returns a proper DTO (`UserEnabledStateDto`). Elsewhere mutations return either the updated DTO (testimonials, blog) or 204 (publish/unpublish).
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
| API-2 | Medium | S | Delete dead ModelState blocks; document ValidationProblemDetails as the 400 contract |
| API-1 | Medium | S | Residual polish after the ProblemDetails unification: align error content-types, fold auth failures in, decide the three outcome DTOs |
| API-3 | Medium | S | Add `ServiceResult.Conflict`; use 409 for all uniqueness violations; unify 403 style |
| API-4 | Medium | S | Fix CreatedAtAction targets; add `GET /api/admin/lessons/{id}` |
| API-6 | Medium | S | Remove `sortBy=email` defaults from `PagedResult<T>` (align with plan 10) |
| API-7 | Medium | S | Single `PagingParams` normalization with one clamp semantic |
| API-9 | Medium | S | Class-level `[Authorize]` on Subscription/SiteSettings; fix Categories doc comment |
| API-5 | Medium | M | Correct OpenAPI schemas (admin assessments), declare SSE and file content types, mention call hub |
| API-8 | Medium | M | Prune toggle-enable, relocate `api/site/admin/*`, make user DELETE return 204, unify lesson routes |
| API-10 | Low | S | Converge mutation success bodies on DTO-or-204 |
| API-11 | Low | S | Thread CancellationToken through all async actions |
| API-12 | Low | S | Public `TestimonialDto` instead of AdminTestimonialDto |
| API-13 | Low | S | Confirmation link → page + POST; stop mutating on GET |
| API-14 | Info | S | One-line ADR: unversioned single-consumer API |
| API-15 | Info | S | (No action) Dev-only Swagger is deliberate |
| API-16 | Info | S | Document the 200-with-verdict verification contract |

## 5. Related Findings Elsewhere

- **30 (DB):** DB-1 — the user-delete 500 whose API symptom API-1 would turn into a 409; DB-7 — concurrency tokens whose `DbUpdateConcurrencyException` needs the 409 mapping from API-3; DB-8 — unbounded request payloads (AnswersJson/SchemaJson) reaching the API without size validation.
- **25 (SEC):** endpoint-level authorization *vulnerabilities* (anonymous Stripe webhook signature-skip when no secret is configured, JWT accepted via query string for `/api/lessons` and hubs, media/asset access rules, impersonation endpoint hardening) and the globally shared fixed-window "auth" rate limiter (one bucket for all clients).
- **26 (REL):** behavior of the self-calling SSR HttpClient consumers when API responses are non-success (`ApiClientBase` swallowing failures), and rate-limiter availability effects.
- **27 (BIZ):** correctness of enrollment/checkout/certificate business outcomes those endpoints return (e.g. mock-payment plan assignment).
- **28 (DQ):** validation of request *content* beyond shape — JSON payload well-formedness, business-rule field validation.
- **33 (UX):** the user-facing consequence of API-13 (email link landing on JSON) and error-message localization in responses.
- **Plan 10 (`10-plan-sorting-rollout.md`):** server-side sorting rollout for the remaining list endpoints — deliberately not re-reported here; API-6 only covers the envelope's false metadata.
- **44 (DOC):** README endpoint tables staying in sync with the actual ~90-endpoint surface.
