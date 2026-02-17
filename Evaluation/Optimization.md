Concern: Startup time (Blazor WASM cold start)

Correct way: Enable trimming + AOT selectively, lazy-load rarely used feature assemblies, keep initial route light, pre-render only critical UI (if applicable), compress assets (brotli), and minimize initial API calls.

False example: App loads all feature code (courses, admin, chat, billing) on first hit and fires 8 API calls immediately.



Concern: Bundle size and dependency bloat

Correct way: Remove unused NuGet/npm packages, avoid heavy UI libs for small needs, split feature modules, and enforce “no duplicate shared JS/CSS”.

False example: Multiple UI libraries + icon packs + redundant polyfills shipped to every user.



Concern: Deterministic UI state transitions

Correct way: Use explicit ViewModel state machines: `Idle -> Loading -> Loaded -> Error`, with a single “source of truth” state object and immutable updates.

False example: Multiple booleans (`isLoading`, `hasError`, `isRefreshing`) drifting out of sync and causing flicker.



Concern: Avoiding redundant renders in Blazor

Correct way: Use `ShouldRender()` where appropriate, avoid frequent `StateHasChanged`, debounce input events, and isolate components (split large pages).

False example: Calling `StateHasChanged()` on every keystroke across the whole page.



Concern: Deterministic data fetching (request dedupe)

Correct way: Centralize fetches in API clients with in-flight request coalescing (same key => same Task), caching with TTL/ETag, and cancellation tokens on navigation.

False example: Every component independently calls `/api/courses` on render, causing request storms and inconsistent ordering.



Concern: Server-side query determinism

Correct way: Always specify ordering in EF queries (`OrderBy`) before paging; use stable tie-breakers (e.g., `OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)`).

False example: Paging without `OrderBy` (non-deterministic results between requests).



Concern: Pagination performance

Correct way: Use cursor-based pagination for large datasets (transactions, chat messages) or indexed `(CreatedAt, Id)` offset pagination; project to DTOs.

False example: `Skip(n).Take(m)` on big tables without indexes and without deterministic ordering.



Concern: EF Core read performance

Correct way: Use `AsNoTracking()` for reads, `Select` projection to DTOs, avoid `Include` unless necessary, and use compiled queries for hot paths.

False example: Loading full entity graphs (`Include` chains) and mapping in memory for list pages.



Concern: EF indexes for hot endpoints

Correct way: Add indexes for common filters and sorts (e.g., `UserId`, `CourseId`, `Status`, `(ConversationId, CreatedAt)`, `(UserId, CreatedAt)`), and verify with query plans.

False example: Relying on table scans for chat history, billing history, enrollments.



Concern: Avoiding N+1 and over-fetching

Correct way: Query exactly what you need (DTO projection), batch related lookups, and avoid lazy loading.

False example: For each course, loading lessons count via separate query in a loop.



Concern: Transaction size and SaveChanges frequency

Correct way: One `SaveChangesAsync()` per command use-case, use bulk updates where appropriate, avoid per-row `SaveChanges` loops.

False example: Saving inside loops for each message/lesson completion.



Concern: API response size (network + CPU)

Correct way: Return minimal DTOs (summary vs details), compress responses, avoid huge nested objects, and use pagination everywhere.

False example: Returning course details with all lessons + full HTML content on courses list.



Concern: Caching strategy (server and client)

Correct way:



\* Server: ETag/If-None-Match for catalog data, short TTL for “mostly static” content

\* Client: Memory cache with TTL + invalidation on writes

&nbsp; False example: No caching; every navigation refetches everything.



Concern: Deterministic error handling and retries

Correct way: Retry only idempotent operations, use idempotency keys for payments and message send, and surface consistent ProblemDetails.

False example: Blind retries on POST create endpoints causing duplicate enrollments/messages.



Concern: Idempotency for user actions

Correct way: Use idempotency tokens for “Enroll”, “Mark Complete”, “Send Message”, “Start Checkout”. Server stores processed keys for a window.

False example: Double-click creates duplicates because server trusts client uniqueness.



Concern: Chat scalability (poor hardware + real-time)

Correct way: Incremental loading (latest N), windowed rendering, message virtualization, and server push with reconnect + “fetch missed since last id”.

False example: Loading entire history and re-rendering the full list on every new message.



Concern: UI virtualization in lists

Correct way: Use virtualization for long lists (conversations, transactions, courses if big), keep DOM small.

False example: Rendering 1000 rows with complex markup and live bindings.



Concern: Minimizing allocations (server)

Correct way: Use `record` DTOs carefully, avoid excessive LINQ chains in hot paths, prefer `ValueTask` where appropriate, and reuse buffers for serialization if needed.

False example: Heavy per-request allocations from deep LINQ + mapping + serialization of large graphs.



Concern: Serialization performance

Correct way: Use System.Text.Json source generation for DTOs in hot endpoints; keep DTOs simple (no polymorphic graphs).

False example: Reflection-heavy serialization of complex polymorphic objects on every request.



Concern: Deterministic time handling

Correct way: Store times in UTC; use injected `IClock`; format in UI using locale; avoid `DateTime.Now`.

False example: Mixed local times causing inconsistent ordering and “renewal date” drift.



Concern: Background jobs and webhooks (billing)

Correct way: Webhook processing is idempotent, uses a durable store (outbox/inbox), and does minimal work inline (queue follow-up).

False example: Webhook handler performs heavy DB work + email sending synchronously and times out.



Concern: Image/media optimization (avatars, thumbnails)

Correct way: Resize server-side, serve modern formats, cache aggressively, and avoid downloading large originals in UI.

False example: Serving multi-megabyte images to the profile page and resizing in CSS.



Concern: WYSIWYG content rendering performance

Correct way: Sanitize once at save-time, store sanitized HTML, render as static markup; avoid re-sanitizing on each read.

False example: Sanitizing large HTML on every GET and on every client render.



Concern: API endpoint “hot path” design

Correct way: For frequently hit endpoints (courses list, lesson view, progress update), keep them single-purpose, minimize joins, and avoid unnecessary authorization DB checks by using claims + cached permissions.

False example: Every GET performs multiple permission queries and loads unrelated aggregates.



Concern: Deterministic routing/navigation (client)

Correct way: Cancel in-flight requests on navigation, ensure “last navigation wins”, and avoid racey state updates.

False example: Late API responses overwrite newer page state, causing UI to show wrong course/lesson.



Concern: Minimizing JS interop and heavy DOM work

Correct way: Keep JS interop rare and coarse; avoid per-message/per-row interop; prefer CSS and Blazor-native rendering.

False example: JS interop called for every chat bubble render.



Concern: Logging overhead (server)

Correct way: Structured logs with sampling for noisy endpoints, avoid logging large payloads, and disable debug logs in production.

False example: Logging full request/response bodies for chat and course content.



Concern: Deterministic builds and performance configuration

Correct way: Release builds with trimming settings verified, deterministic builds enabled, consistent `InvariantGlobalization` decision, and performance regression tests.

False example: Debug-like builds shipped; trimming breaks features; performance varies unpredictably per build.



Concern: Performance testing and regression prevention

Correct way: Define SLOs: max TTFB, max payload size per endpoint, max client render time, and run smoke perf tests (k6 + Playwright) on CI.

False example: No metrics; performance issues only discovered on low-end devices in production.



Concern: Database growth control

Correct way: Archive/partition chat messages and transaction logs, keep indexes maintained, and prune ephemeral records (idempotency keys, drafts) with TTL.

False example: Chat table grows unbounded with no indexes and queries get slower over time.



Concern: Memory pressure in Client (WASM)

Correct way: Avoid storing large HTML/media in long-lived state, dispose streams, and keep ViewModels lightweight; use paging/virtualization.

False example: Storing entire course HTML + video metadata + chat history in a singleton state store.



Concern: UI perceived performance

Correct way: Skeleton loaders, optimistic UI where safe (with rollback), and keep interactions under ~100ms by deferring non-critical work.

False example: Blocking UI on every network call with full-screen spinner and no caching.



If you want, I can translate this into a prioritized “Refactor + Perf Checklist” (Top 20 changes) for your specific features (Courses, Lessons, Chat, Billing) in the same Concern/Correct/False format.



