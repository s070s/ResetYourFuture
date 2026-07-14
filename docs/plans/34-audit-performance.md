# Audit: Performance

| | |
|---|---|
| Finding prefix | PERF |
| Created | 2026-07-11 |
| Scope | Runtime cost that is fixable by code optimization: query patterns (round-trips, over-fetching, tracking), allocation and payload churn, Blazor Server render/circuit traffic, the per-request cost of the loopback HTTP architecture, chat/presence/assistant hot paths, and PDF generation on request threads. |
| Delegated | The loopback-SSR design itself → ARCH (21, ARCH-1). Multi-instance / user-growth blockers → SCALE (35). Failure modes, uptime, boot behaviour → AVAIL (36). Schema design of the `nvarchar(48)` DateTimeOffset columns → DB (30, DB-2). Per-request security-stamp DB lookup as a reliability coupling → REL (26, REL-7). Logger design/rotation → LOG (37). |

## 1. Methodology

Traced the render-to-data path end to end: `Program.cs` (global InteractiveServer, `App.razor` prerender), the 19 loopback HttpClient consumers (`Startup/ServiceRegistrationExtensions.cs:211-254`, `Consumers/ApiClientBase.cs`, `Services/SsrApiHandler.cs`, `Services/ApiTokenProvider.cs`, `Infrastructure/Services/AuthService.cs:276-291`), and both auth validation hooks (`Startup/AuthenticationSetupExtensions.cs`). Read every Application service with query logic (`CourseService`, `ChatQueryService`, `AdminUserService`, `SubscriptionService`, `BlogArticleService`, `AssistantService`, `AssistantRetrievalService`, `AssistantIndexingService`), both SignalR hubs, `CallRegistry`, `PresenceService`, the chat UI stack (`Pages/Chat.razor(.cs)`, `Shared/Components/Chat/*`), `AssistantWidget`, `AvatarDropdown`, `MyCertificates`, `CertificateService` (QuestPDF), `LocalFileStorage`, `MediaController`, `LessonAssetsController`, and the custom `Web/Logging/FileLogger*` write path. Checked `Directory.Packages.props` and `Program.cs` for compression/caching middleware. Verified the `nvarchar(48)` claim against `ApplicationDbContextModelSnapshot.cs` (32 occurrences).

NOT examined: actual profiling/benchmarks (static analysis only, app not launched per audit constraints); SQL execution plans; browser-side JS performance (`webrtc-interop.js` internals) beyond render-loop interactions.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 5 |
| Info | 1 |

> **Accepted since audit (out of scope — will not implement):** PERF-1 (every data fetch pays the full loopback HTTP pipeline — JWT mint, middleware, per-request DB auth lookup, double JSON). This is the per-interaction cost of the loopback self-API architecture (ARCH-1), which is itself consciously accepted as a documented tradeoff — the only real fix is ARCH-1's in-process redesign, which the project does not need. The short-term mitigations this finding listed (cache the per-circuit JWT and signing credentials, drop the per-request auth DB lookup) are individually small but touch the auth hot path with no user-visible benefit at single-instance demo scale, so PERF-1 is accepted rather than fixed. See ARCH-1's matching note in [21-audit-architecture.md](21-audit-architecture.md).

The query layer is unusually disciplined for a project at this stage: list endpoints are paged, correlated subqueries have been deliberately replaced with batched `GROUP BY`/join queries (`ChatQueryService`, `AdminUserService`, `CourseService`), chat lists use `Virtualize` with `@key`, searches are debounced, and there are well-placed 30-second caches (subscription status, assistant status, sitemap). The dominant cost is architectural: because every page's data arrives via a real HTTP request to the app's own API, each interaction pays JWT minting, full middleware traversal, a per-request user lookup, and double JSON serialization (PERF-1, accepted above).

All five Medium findings have been resolved: blog summaries now project in SQL instead of loading full article bodies (PERF-3); certificate PDF rendering is deferred off the lesson-completion request to first download (PERF-4); avatars and certificate PDFs are served straight to the browser over same-origin cookie-authenticated HTTP rather than base64/bytes through the circuit (PERF-5); the chat and assistant inputs no longer two-way bind every keystroke through the circuit (PERF-6); and the chat send path builds the sender's name/role from claims instead of two identity queries per message (PERF-7). What remains is five Low items and one Info observation.

## 3. Findings

> The five Medium findings (PERF-3, PERF-4, PERF-5, PERF-6, PERF-7) are resolved and have been removed from this list; see §2 for the summary and the git history (`Fix PERF-3` … `Fix PERF-7`) for the changes. The remaining open items are five Low and one Info.

### PERF-8: Opening a conversation loads messages twice (page 1, then the real last page)  [Low] [Effort: S]
- **Evidence:** `Pages/Chat.razor.cs:51-72` — `SelectConversation` calls `LoadMessagesAsync()` (page 1), inspects `TotalPages`, then calls it again for the last page. `OnMessagePageSizeChanged` repeats the same double-load (`:107-119`). Each load is a loopback REST call that runs a `COUNT` plus a page query (`ChatQueryService.cs:107-118`).
- **Impact:** Every conversation open costs 2× (COUNT + page fetch + roles query) through the full loopback pipeline; the first page's rows are fetched and discarded.
- **Recommendation:** Order messages descending and fetch page 1 (reversing client-side), or expose a `page=last` server convention so one call returns the newest page.

### PERF-9: Published-courses listing executes the filtered query three times  [Low] [Effort: S]
- **Evidence:** `Application/ApiServices/CourseService.cs:48-85` — `CountAsync`, then a `Skip/Take` fetch of `pageIds`, then a second identical `Skip/Take` with the full projection; plus the two batch lookups (enrollments, lesson counts). Five round-trips per page render where four would do.
- **Impact:** One redundant paged query per Courses page view — small individually, but it sits on a page every student hits and runs through the loopback pipeline (PERF-1) on top.
- **Recommendation:** Run the projection query once and derive `pageIds` from its results before the two batch lookups.

### PERF-10: Assistant index pass materializes every chunk row (embeddings included) just to diff hashes  [Low] [Effort: S]
- **Evidence:** `Application/ApiServices/AssistantIndexingService.cs:25-27` — `db.AssistantContentChunks.ToListAsync()` loads all rows, embedding `varbinary` blobs included, into tracked entities every pass (every 6 h and on admin reindex, `Web/Services/AssistantIndexer.cs:18-19`), though unchanged sources only need `(SourceType, SourceId, Language, ContentHash)` plus the row IDs for deletes.
- **Impact:** Memory/allocation spike proportional to the full index size on every pass; harmless at hundreds of chunks, increasingly wasteful as content grows (SCALE-10 owns the growth ceiling).
- **Recommendation:** Project the diff query to keys + hash (+ Id), and fetch full rows only for the sources being replaced; use `ExecuteDelete` for removals.

### PERF-11: Read-only queries left on the change tracker  [Low] [Effort: S]
- **Evidence:** `Application/ApiServices/ChatQueryService.cs:107-118` — `GetMessagesAsync` fetches messages with two `Include`s and no `AsNoTracking` (a read-only history page). `Controllers/LessonAssetsController.cs:45-48` — tracked three-level `Include` chain for a pure read. `CourseService.EnrollAsync`'s course fetch (`CourseService.cs:156`) is also read-only but tracked.
- **Impact:** Needless snapshot/change-tracking allocations on hot read paths — the chat history query runs on every conversation open and page change.
- **Recommendation:** Add `AsNoTracking()`, matching the 50+ existing correct usages across the same services.

### PERF-12: No response compression and no cache headers on locally served static assets  [Low] [Effort: S]
- **Evidence:** `Program.cs:78` — bare `app.UseStaticFiles()` (no cache-control configuration); no `AddResponseCompression`/`UseResponseCompression` or output caching anywhere (verified by search). `App.razor:22-29` serves bootstrap.min.css, app.css, shared-components.css and the scoped-CSS bundle locally on every page load. `MediaController.cs:71` shows the intended caching pattern (`public, max-age=86400`) applied only to uploaded media.
- **Impact:** Full-page loads (first visit, post-logout, circuit-reload) re-negotiate every static asset with at best 304 revalidations and no gzip/brotli for the SSR HTML. Modest, but it is the entire anonymous-visitor experience (Home/blog/pricing).
- **Recommendation:** Add `UseResponseCompression` (or rely on a fronting proxy when one exists — CLOUD 41's call) and set `StaticFileOptions.OnPrepareResponse` cache headers with the fingerprinted-asset awareness .NET 10's `MapStaticAssets` provides out of the box.

### PERF-13: Assistant retrieval and chat-render hot paths are well built at current scale  [Info]
- **Evidence:** `AssistantChunkCache` swaps snapshots by single reference assignment (`Application/Common/AssistantChunkCache.cs:23-27`); ranking uses SIMD `TensorPrimitives.CosineSimilarity` over the in-memory snapshot (`AssistantRetrievalService.cs:27-34`); source resolution batches one query per source type (`:59-80`). `AssistantWidget` throttles streaming re-renders to 100 ms (`AssistantWidget.razor.cs:15,129-137`). Chat lists use `Virtualize` + `@key` (`MessagePane.razor:66-86`, `ConversationSidebar.razor:33-41`). The file logger's write path is non-blocking (bounded channel, `TryWrite`, single drain-then-flush reader — `Logging/FileLoggerProvider.cs:18-53`).
- **Impact:** None — recorded so future work doesn't "optimize" already-sound paths.
- **Recommendation:** Keep these patterns as the house reference for new features.

## 4. Prioritized Action List

All five Medium items (PERF-3 through PERF-7) are resolved. The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| PERF-8 | Low | S | Single-fetch the newest chat page on conversation open |
| PERF-9 | Low | S | Collapse the duplicate paged query in `GetPublishedCoursesAsync` |
| PERF-10 | Low | S | Diff assistant index on projected hashes, not full rows |
| PERF-11 | Low | S | Add `AsNoTracking` to the remaining read-only queries |
| PERF-12 | Low | S | Add response compression + static-asset cache headers |

## 5. Related Findings Elsewhere

- **ARCH (21):** ARCH-1 owns the loopback-HTTP-to-self design that PERF-1 costs out; ARCH-7 owns the global InteractiveServer decision behind PERF-6's per-keystroke traffic.
- **REL (26):** REL-7 owns the per-request `FindByIdAsync` in both auth hooks — the single biggest per-call item inside PERF-1's pipeline; REL-3 owns the consumer error-swallowing on those same calls.
- **DB (30):** DB-2 owned the nvarchar(48) schema fix underlying PERF-2 (both now fixed — columns are native `datetimeoffset`); PERF-3's over-fetch is on `ContentEn`/`ContentEl` (intentionally unbounded rich-text, not one of DB-8's capped columns — DB-8 itself is fixed).
- **SCALE (35):** Owns the multi-instance/user-growth consequences of the same hot paths (presence fan-out, circuit memory, chunk-cache growth).
- **AVAIL (36):** Owns timeout/resilience of the loopback HttpClients whose per-call cost PERF-1 describes.
- **UX (33):** Perceived-latency consequences (loading states during multi-call page loads) are UX territory.
