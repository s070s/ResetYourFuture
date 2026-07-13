# Plan: Five New High-Impact Features

| | |
|---|---|
| Status | Draft |
| Created | 2026-07-11 |
| Depends on | 11-plan-ollama-agent (only for the `recommend_courses` synergy in Feature 2) |
| Related audits | UX (33), Business Logic (27) |

## 1. Context & Goals

- Perfecting the app means adding capabilities users expect from a learning platform, **without** importing new infrastructure.
- Selection criterion: each feature **activates something the codebase already paid for** (SignalR, presence, embeddings, WebRTC, admin CRUD patterns) — maximum impact per line of code.
- "Done" per feature = student-facing UI + admin surface (where applicable) + EN/EL localization + tests, following the repo's existing entity → migration → service → controller → consumer → page chain.

## 2. Current State (what each feature builds on)

| Existing asset | File anchors |
|---|---|
| SignalR + presence | `src/ResetYourFuture.Web/Hubs/ChatHub.cs`, `Services/PresenceService.cs` |
| Admin CRUD page pattern | `Pages/AdminTestimonials.razor`, `Controllers/AdminTestimonialsController.cs`, `TestimonialService.cs` |
| Embeddings + chunk index | `AssistantContentChunks` table, `AssistantRetrievalService.cs`, `bge-m3` |
| WebRTC call stack (no scheduling surface) | `Hubs/CallHub.cs(+.Signaling.cs)`, `Services/CallService.cs`, `CallRegistry.cs`, `CallRingMonitor.cs` |
| Categories shared across content | `Category` entity, `CategoriesController.cs` |
| Enrollment/completion tracking | `Enrollment`, `LessonCompletion` entities |
| Loopback consumer convention | `src/ResetYourFuture.Web/Consumers/` + `SsrApiHandler` |

## 3. Design Decisions

| # | Decision | Alternatives rejected | Rationale |
|---|----------|-----------------------|-----------|
| 1 | Notification Center ships **first**; Features 2 and 5 emit into it | Independent toasts per feature | One persisted inbox + one SignalR push path beats N ad-hoc mechanisms |
| 2 | All five features follow the existing chain (entity → EF config → migration → Application service → controller → consumer → Razor page) with no new packages | New libraries (e.g. FullCalendar, search engines) | Repo conventions are strong; zero new dependencies keeps the fresh-clone story intact |
| 3 | Semantic search reuses the assistant's embedding pipeline and chunk table | Separate search index (Lucene, SQL full-text) | bge-m3 vectors for all published content already exist and are language-aware |
| 4 | Reviews are course-scoped, one per enrolled student, admin-moderated (pending → approved) | Anonymous/unmoderated reviews | Mirrors the proven testimonial moderation flow |
| 5 | Scheduled sessions reuse `CallSession` infrastructure — a schedule row *materializes* a call at start time | A parallel video stack | The call hub, registry and ring monitor already handle the hard parts |

## 4. Work Items (one per feature)

### ~~WI-1: In-App Notification Center~~ ✅ DONE
`Notification` entity (plain, Cascade FK to ApplicationUser) + migration `AddNotifications`; `NotificationService` (CRUD) + `INotificationDispatcher` (framework-agnostic interface in Application, implemented in Web via `NotificationDispatcher` using `IHubContext<NotificationHub>` — lets Infrastructure's `CertificateService` and Application's `SubscriptionService` raise notifications without a SignalR dependency); `NotificationHub` (connects globally like `CallHub`, `user_{userId}` groups) + `NotificationConnectionTracker` (per-user connection refcount, doubles as an online/offline signal). Emit points wired: chat message received while the recipient has no live connection at all (an active session already gets the existing live toast, so this avoids flooding the inbox), certificate issued, subscription activated via real checkout. Subscription-expiring emit deferred until BIZ-1's expiry sweep job exists (Phase 5) — wiring it now would mean building that job prematurely.
UI: `NotificationBell` (bell + unread badge, dropdown with mark-as-read, mounted in `MainLayout` next to `AvatarDropdown`) opens its own SignalR connection for live badge updates; full `/notifications` page uses `ScrollableTable` + `SortableColumnHeader` + `AdminPaginationToolbar` per the 10-plan pattern (createdat/isread sortable). New `NotificationRes.resx` (+ `.el`, hand-edited `Designer.cs`); TitleKey+BodyArgs stored (not pre-rendered text) so the same row renders correctly in whichever culture it's viewed in.
Tests: `NotificationSearchExtensionsTests`, `NotificationServiceTests`, `NotificationHubTests` (connect/disconnect/multi-tab), `NotificationsControllerTests` (authz + cross-user isolation), plus two `ChatHubTests` covering the online/offline dispatch decision. Verified live: bell renders, empty state, full page, EN/EL localization, no console errors, clean server boot.

### ~~WI-2: Course Reviews & Ratings~~ ✅ DONE
`CourseReview` entity (Cascade FKs to both Course and User, matching Enrollment's proven two-cascade-parent shape; unique index on `(CourseId, UserId)`) + migration `AddCourseReviews`. `CourseReviewService`: `SaveMyReviewAsync` upserts (enrollment required, editing resets to Pending for re-moderation), `GetApprovedForCourseAsync`/`GetRatingSummar(y/ies)Async` for public display (author-name and rating aggregation computed inline in the LINQ projection, not via a helper method, so it stays SQL-translatable), admin `GetPagedAsync`/`ApproveAsync`/`RejectAsync` with a new `CourseReviewSearchExtensions` (rating/status/createdat sort per the 10-plan pattern). Approving fires the WI-1 "review approved" notification, best-effort (a dispatch failure doesn't fail the moderation action).
`CourseListItemDto` gained `AverageRating`/`ReviewCount` (batch-computed in `CourseService`); `CourseDetailDto` was deliberately left unchanged — the detail page sources rating data from the same `GET .../reviews` call it already needs for the review list, avoiding two parallel rating computations.
API: `CoursesController` gained `GET/POST {id}/reviews` (combined response: approved list + summary + the caller's own review at any status in one round trip); `AdminCourseReviewsController` (list/approve/reject) — a `AdminTestimonialsController`-shaped clone.
UI: new shared `StarRating` display component (course cards, review lists, admin queue); review form + list + pending/rejected notices on `CourseDetail.razor`; average stars on `Courses.razor` cards; `AdminCourseReviews.razor` moderation queue (ScrollableTable + SortableColumnHeader + AdminPaginationToolbar, status filter). New `ReviewRes.resx` (+ `.el`, hand-edited `Designer.cs`); new `NavMenuRes.CourseReviews` key.
Tests: `CourseReviewSearchExtensionsTests`, `CourseReviewServiceTests` (enrollment gate, upsert/re-moderation, approved-only visibility, rating aggregation, notification dispatch incl. best-effort-on-failure), `CoursesControllerReviewsTests` + `AdminCourseReviewsControllerTests` (authz, cross-user pending-review isolation, status filtering). Verified live end-to-end with two real browser sessions: enroll → write review → pending → admin approves → visible on course page → average stars on course card → EL localization.

### ~~WI-3: Semantic Site Search~~ ✅ DONE
`AssistantRetrievalService` refactored to share its ranking core (`RankChunksAsync` embed+cosine+threshold step, reused by both methods): existing `SearchAsync` (per-chunk, for the assistant's grounding) is unchanged in contract; new `SearchGroupedAsync` deduplicates to one result per source (its best-scoring chunk becomes the snippet) — the shape search needs. `IAssistantRetrievalService` is now always resolvable regardless of `Assistant:Enabled` (new `DisabledAssistantRetrievalService`, mirroring `DisabledAssistantService`), so `SiteSearchService` can depend on it unconditionally.
`SiteSearchService`: semantic search only attempted once `AssistantRuntimeState.Status == Ready` (skips a pointless empty-cache call while bootstrapping/disabled); falls back to a SQL title search (plain `.Contains`, not `EF.Functions.Like` — the latter isn't supported by the EF Core InMemory provider the Web.Tests factory uses) across published courses/assessments/blog articles on any non-Ready state, an exception, or zero semantic hits. `SiteSearchResultDto.SemanticSearchUsed` lets the UI show a notice when running on the fallback.
UI: `SiteSearchBox` (debounced flyout, mounted in `MainLayout` header) + full `/search` page, both anonymous — `SearchController` has no `[Authorize]`, matching `BlogController`/`TestimonialsController`'s existing public-content precedent. New `SearchRes.resx` (+ `.el`).
Tests: `AssistantRetrievalServiceTests` extended for `SearchGroupedAsync` (dedup, ordering, topK, threshold); `SiteSearchServiceTests` (semantic path, fallback on not-Ready/exception/empty, published-only filtering); `SearchControllerTests` (anonymous 200, published-only, empty-query, limit clamping). Verified live: anonymous flyout + full page + fallback notice + EL localization, all against real seeded content.

### ~~WI-4: Learning Paths~~ ✅ DONE
`LearningPath` extends `AuditableEntity` (reuses `IsPublished`/`PublishedAt` instead of a bespoke Status enum, matching Course/BlogArticle) with optional `CategoryId` (`SetNull` on delete) and `DisplayOrder`; `LearningPathStep` (Cascade FK to `LearningPath`, Restrict FK to `Course` — Courses are only ever soft-deleted so this never actually fires) with a unique `(LearningPathId, StepOrder)` index, plus a matching soft-delete query filter (`!s.Course.IsDeleted`) alongside Enrollment/LessonCompletion's existing ones; migration `AddLearningPaths`.
`LearningPathService`: public `GetPublishedAsync`/`GetByIdAsync` project per-user step state from the existing `Enrollment.Status == Completed` signal (no new progress table) — step 1 is never locked, a step is locked iff the previous step isn't completed, and the first non-completed/non-locked step is flagged `IsNext`; anonymous callers get a browsing-only view (nothing locked, nothing flagged next). Admin CRUD (`Create/Update/Publish/Unpublish/DeleteAsync`, the last a soft delete via `IsDeleted`) plus step management (`AddStepAsync` appends at `max(StepOrder)+1` and rejects a duplicate course with 409; `RemoveStepAsync` re-sequences remaining steps ascending so no two rows transiently collide on the unique index; `MoveStepUp/DownAsync` swap `StepOrder` via a `-1` sentinel in an intermediate save, for the same reason) — new `LearningPathSearchExtensions` (titleen/ispublished/createdat/displayorder, default displayorder asc) for the admin list.
API: public `PathsController` (`GET /api/paths`, `GET /api/paths/{id}`, anonymous — reads `ClaimTypes.NameIdentifier` when present, like `SearchController`/`BlogController`'s public-content precedent); admin `AdminLearningPathsController` (CRUD + step add/remove/move, clone of `AdminTestimonialsController`'s shape).
UI: `/paths` catalog + `/paths/{id}` detail (step list with locked/next/completed badges, "Go to course" link per unlocked step, sign-in prompt for anonymous visitors); `AdminLearningPaths.razor` (ScrollableTable/SortableColumnHeader/AdminPaginationToolbar list) + `AdminLearningPathEditor.razor` (title/description EN+EL, category dropdown, display order, and — once the path exists — a step list with the `AdminTestimonials` manual ↑/↓ reorder pattern plus an add-course dropdown); nav links for both Student and Admin roles. New `PathRes.resx` (+ `.el`, hand-edited `Designer.cs`); new `NavMenuRes.Paths` key.
Tests: `LearningPathSearchExtensionsTests`, `LearningPathServiceTests` (progress projection incl. anonymous-vs-authenticated, step add/remove/reorder, admin CRUD), `PathsControllerTests` + `AdminLearningPathsControllerTests` (anonymous/authz, published-only visibility, step management). Verified live end-to-end with two real browser sessions: admin composes a 2-course path → publishes → student sees it in the catalog → step 1 unlocked/step 2 locked → student completes every lesson in course 1 → step 1 flips to completed and step 2 unlocks as "next" → EL localization confirmed on the catalog heading.

### WI-5: Scheduled Live Sessions
**Why:** the WebRTC stack (hub, registry, ring monitor, group calls ≤ 6) is fully built but only reachable ad-hoc; scheduling turns dormant infrastructure into a headline feature (office hours, group coaching).
- **Entities + migration:** `ScheduledSession` (Id, HostUserId, TitleEn/El, CourseId?, StartsAtUtc, DurationMinutes, MaxParticipants ≤ 6, Status Scheduled/Live/Ended/Cancelled, CallSessionId?), `SessionRegistration` (SessionId, UserId; unique pair), migration `AddScheduledSessions`.
- **Backend:** `ScheduledSessionService` (CRUD + register/unregister with capacity check); extend the existing `CallRingMonitor`/new `SessionStartMonitor` hosted service: at `StartsAtUtc` materialize a `CallSession` via `CallService`, flip status Live, notify registrants (WI-1); "Join" resolves to the existing call UI (`CallOverlayHost`).
- **API + consumer:** `GET /api/sessions` (upcoming, mine), `POST /api/sessions/{id}/register`; admin/host CRUD `AdminSessionsController`; consumers per convention.
- **UI:** `/sessions` upcoming list (register/joins states, countdown), admin/host `AdminSessions.razor` (create/edit/cancel, sortable per 10-plan); join button appears when Live; reminder notification 15 min before. `SessionRes.resx` (+ `.el`).
- **Effort:** L. **Depends on:** WI-1 (reminders/notifications), existing call stack.
- **Acceptance:** registered student gets a reminder, clicks Join at start time, lands in the existing group-call UI with the host; capacity enforced at 6.

## 5. Implementation Order & Dependencies

1. **WI-1 Notification Center** — everything else emits into it.
2. **WI-3 Semantic Search** — smallest, zero schema, immediately visible.
3. **WI-2 Reviews** — testimonials clone + notification emit.
4. **WI-4 Learning Paths** — independent; can run parallel with WI-2.
5. **WI-5 Scheduled Sessions** — last; leans on notifications + call stack.

## 6. Verification

- Per feature: `dotnet build` / `dotnet test` green; new services unit-tested (in-memory DbContext, per repo pattern); controller happy-path + authz matrix tests in `Web.Tests` (clone existing controller test shape).
- Manual script per feature in **EN and EL** (culture selector), covering the acceptance line of each WI.
- Cross-feature drill: student enrolls → completes a path step → posts a review → admin approves (notification) → registers for a session → gets reminder → joins live call.
- Regression: existing chat/call/assessment flows untouched (`Web.Tests` suite green).

## 7. Out of Scope

- Payments for individual sessions (subscription tiers already gate content).
- Email delivery of notifications (in-app only in v1; SMTP exists if wanted later).
- Search over user-generated content (chat, reviews) — published content only.
- Recurring session schedules (single occurrences in v1).
