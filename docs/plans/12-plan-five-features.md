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

### WI-1: In-App Notification Center
**Why:** every interactive feature (chat, grading, subscriptions, sessions) currently ends in silence unless the user is on the right page; SignalR + presence make delivery nearly free. Highest cross-feature payoff, so it ships first.
- **Entities + migration:** `Notification` (Id, UserId, Type enum, TitleKey/BodyArgs for localized rendering, LinkUrl, IsRead, CreatedAt; index on `(UserId, IsRead, CreatedAt)`), migration `AddNotifications`.
- **Backend:** `NotificationService` (create/mark-read/list paged, prune old), `INotificationDispatcher` that persists **then** pushes via a new lightweight `NotificationHub` (or piggybacks on `ChatHub` connection) to online users; emit points: chat message received while offline from that conversation, certificate issued, subscription activated/expiring, (later) review approved + session reminders.
- **API + consumer:** `NotificationsController` (`GET /api/notifications` paged, `POST /{id}/read`, `POST /read-all`), `NotificationConsumer` per loopback convention.
- **UI:** bell + unread badge in `Layout/MainLayout.razor` (next to `AvatarDropdown`), dropdown panel with mark-as-read, full `/notifications` page using `ScrollableTable` + sorting pattern from 10-plan. New `NotificationRes.resx` (+ `.el`).
- **Effort:** L. **Depends on:** nothing.
- **Acceptance:** an event raised while the user is online shows the badge live (no refresh); offline events appear on next login; localized in both cultures.

### WI-2: Course Reviews & Ratings
**Why:** social proof drives enrollment; the testimonials pattern (entity, moderation queue, admin page) is a near copy-paste; ratings feed the agent's `recommend_courses` tool (11-plan WI-B2).
- **Entities + migration:** `CourseReview` (Id, CourseId, UserId, Rating 1–5, Body, Status Pending/Approved/Rejected, CreatedAt; unique index `(CourseId, UserId)`), migration `AddCourseReviews`.
- **Backend:** `CourseReviewService` — create (only enrolled students; one per course, editable), moderate, list approved per course, average-rating projection onto course DTOs.
- **API + consumer:** student endpoints under `CoursesController` (`GET/POST/PUT api/courses/{id}/reviews`), admin moderation `AdminCourseReviewsController` (list pending, approve/reject) — clone of `AdminTestimonialsController`; consumers likewise.
- **UI:** rating stars + review list on `CourseDetail.razor`; "write a review" gated on enrollment; `AdminCourseReviews.razor` moderation queue (testimonials page clone, with sorting per 10-plan); average stars on course cards in `Courses.razor`. `ReviewRes.resx` (+ `.el`).
- **Effort:** M. **Depends on:** WI-1 (emits "review approved" notification — optional coupling).
- **Acceptance:** enrolled student can post/edit; unapproved reviews invisible to others; average updates on approval; admin queue sortable.

### WI-3: Semantic Site Search
**Why:** the app has zero global search; the `bge-m3` embeddings of every published course/lesson/assessment/blog article **already exist** in `AssistantContentChunks` — this feature is mostly a thin query surface over paid-for infrastructure.
- **Entities + migration:** none (reuses `AssistantContentChunks`).
- **Backend:** `SiteSearchService` in Application: embed the query via the existing `IEmbeddingGenerator`, rank via the existing cosine/`AssistantChunkCache` path (`AssistantRetrievalService` refactored to share its ranking core), group hits by source (course/assessment/article) and return top sources with best-chunk snippet + URL. Graceful fallback to SQL `LIKE` over titles when the assistant/Ollama is unavailable (state from 11-plan WI-A2).
- **API + consumer:** `GET /api/search?q=…&limit=…` (`SearchController`, anonymous-allowed for published content), `SearchConsumer`.
- **UI:** search box in `NavMenu.razor` (or `MainLayout` header) with a results flyout + full `/search` page; highlight matched snippet; per-type icons. `SearchRes.resx` (+ `.el`).
- **Effort:** M. **Depends on:** 11-plan WI-A2 state (for the fallback signal) — otherwise standalone.
- **Acceptance:** querying a concept (not an exact title word) in either language surfaces the right course/article; Ollama down ⇒ title search still works with a notice.

### WI-4: Learning Paths
**Why:** courses are currently a flat catalog; ordered paths ("Career Change Starter → CV Lab → Interview Mastery") add curriculum value and a reason to subscribe; admin CRUD mirrors existing admin pages exactly.
- **Entities + migration:** `LearningPath` (Id, TitleEn/El, DescriptionEn/El, CategoryId?, Status, DisplayOrder), `LearningPathStep` (PathId, CourseId, StepOrder; unique `(PathId, StepOrder)`), migration `AddLearningPaths`.
- **Backend:** `LearningPathService` — CRUD, publish, per-user progress projection (step complete = existing `LessonCompletion` says course complete); no new progress tables.
- **API + consumer:** public `GET /api/paths`, `GET /api/paths/{id}` (with per-user progress when authenticated); admin CRUD `AdminLearningPathsController`; consumers per convention.
- **UI:** `/paths` list + `/paths/{id}` detail with step progression UI (locked/next/done states); `AdminLearningPaths.razor` (list + editor with step reordering — reuse the manual ↑/↓ pattern from `AdminTestimonials`); nav link. `PathRes.resx` (+ `.el`).
- **Effort:** M–L. **Depends on:** nothing.
- **Acceptance:** admin composes a path of 3 courses; student sees progress advance as courses complete; bilingual.

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
