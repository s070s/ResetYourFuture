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
| High | 1 |
| Medium | 5 |
| Low | 5 |
| Info | 1 |

> **Fixed since audit:** PERF-2 (Medium — `ORDER BY`/range predicates ran against `nvarchar(48)` DateTimeOffset columns) — the columns are now native `datetimeoffset` on SQL Server (the `DateTimeOffsetToStringConverter` is scoped to the SQLite test provider and migration `ConvertDateTimeOffsetToNativeType` restored the type; owned by DB-2). Ordered/range date queries now use the native 10-byte type with no query rewrites, exactly as this finding anticipated.

The query layer is unusually disciplined for a project at this stage: list endpoints are paged, correlated subqueries have been deliberately replaced with batched `GROUP BY`/join queries (`ChatQueryService`, `AdminUserService`, `CourseService`), chat lists use `Virtualize` with `@key`, searches are debounced, and there are well-placed 30-second caches (subscription status, assistant status, sitemap). The dominant cost is architectural: because every page's data arrives via a real HTTP request to the app's own API, each interaction pays JWT minting, full middleware traversal, a per-request user lookup, and double JSON serialization — multiplied by global InteractiveServer, which also routes every keystroke of `oninput`-bound inputs and every binary payload (avatars, certificate PDFs) through the SignalR circuit.

## 3. Findings

### PERF-1: Every data fetch pays the full loopback HTTP pipeline — JWT mint, middleware, per-request DB auth lookup, and double JSON  [High] [Effort: L]
- **Evidence:** 19 typed consumers call the app's own REST API over HTTP (`Startup/ServiceRegistrationExtensions.cs:211-254`). Per call: a fresh JWT is minted — `ApiClientBase.EnsureAuthorizationAsync` (`Consumers/ApiClientBase.cs:34-39`) → `ApiTokenProvider` → `AuthService.GetTokenAsync` builds a new `SymmetricSecurityKey` + `SigningCredentials` and signs (`Infrastructure/Services/AuthService.cs:276-291`); SSR-side, `SsrApiHandler` does the same per request (`Services/SsrApiHandler.cs:35-54`). The receiving side re-validates the JWT and performs a `userManager.FindByIdAsync` DB query on **every** loopback request (`Startup/AuthenticationSetupExtensions.cs:159-179`; the DB-coupling aspect is REL-7). Payloads are JSON-serialized in the controller and deserialized again in the consumer. A single Courses page load issues 3 parallel loopback calls (`Pages/Courses.razor.cs:40-44`); `AvatarDropdown` adds 2 more per circuit (PERF-5); Chat opens issue 2-4 (PERF-8).
- **Impact:** Each logical "read some rows" costs an extra TLS-loopback HTTP round-trip, an HMAC sign + validate, one extra DB query, and two JSON passes — per consumer call, per page, per user. Latency stacks visibly on pages that fan out several calls, and CPU/GC load per interaction is a multiple of what in-process service calls would cost.
- **Recommendation:** ARCH-1 owns the redesign (call Application services in-process). Short-term mitigations that don't change the architecture: cache the minted JWT per circuit (the code itself documents "the token is identical for every call within a circuit", `ApiClientBase.cs:28-33`) instead of re-signing per call; cache the `SymmetricSecurityKey`/`SigningCredentials` instances (they are config-constant); and cache the (userId → IsEnabled/securityStamp) check per REL-7 to drop the per-request DB read.

### PERF-3: Blog summary queries load full article bodies to build summaries  [Medium] [Effort: S]
- **Evidence:** `Application/ApiServices/BlogArticleService.cs:34-41` — `GetPublishedSummariesAsync` does `ToListAsync()` on whole `BlogArticle` entities (including `ContentEn`/`ContentEl` rich-HTML bodies) and then maps to `BlogArticleSummaryDto`. Callers: the Home page (6 articles per render, `Pages/Home.razor.cs:99`) and the sitemap (up to 200 articles, `Startup/InfrastructureEndpointsExtensions.cs:228`).
- **Impact:** Full article HTML (unbounded columns per DB-8) is transferred from SQL, materialized, and immediately discarded — on the most-visited page of the site. The sitemap pass pulls up to 200 full bodies (mitigated by its 30-min cache).
- **Recommendation:** Project in SQL with `.Select(a => new { ... })` to only the summary fields, mirroring the projection pattern already used in `CourseService.GetPublishedCoursesAsync` (`CourseService.cs:70-85`).

### PERF-4: QuestPDF certificate generation runs synchronously inside the lesson-completion request  [Medium] [Effort: M]
- **Evidence:** `Application/ApiServices/CourseService.cs:344-360` — completing the final lesson calls `certificateService.GetOrGenerateAsync` inline; `Infrastructure/ApiServices/CertificateService.cs:158-170` renders the PDF on the request thread (`BuildDocument(...).GeneratePdf()`, `:183-273`) and writes it to disk before the completion response returns. The request itself is a loopback HTTP call from the student's circuit.
- **Impact:** The unlucky student who finishes a course pays CPU-bound PDF layout/rendering plus file I/O in their "mark lesson complete" click, holding a threadpool thread and a circuit interaction for the duration. Under concurrency (several completions at once), PDF rendering competes with all request processing.
- **Recommendation:** Defer generation: issue the `Certificate` row on completion and render the PDF lazily on first download (the idempotent `GetOrGenerateAsync` already supports get-or-create semantics), or queue generation to a background channel/hosted service — the repo already has the `BackgroundService` + scoped-service pattern (`CallRingMonitor`).

### PERF-5: Megabyte-scale binaries are shuttled through the SignalR circuit (avatars as base64 data URLs, PDFs via JS interop)  [Medium] [Effort: M]
- **Evidence:** `Layout/AvatarDropdown.razor.cs:77-98` — every circuit fetches the profile *and* the raw avatar bytes over loopback HTTP, then builds `data:{type};base64,{...}` (avatars may be up to 5 MB, `LocalFileStorage.cs:16`) which lives in circuit memory and is re-sent in render batches. `Pages/MyCertificates.razor.cs:47-51` — certificate download pulls the whole PDF into a `byte[]` via loopback (`ApiClientBase.GetBytesAsync`) and pushes it through `JSRuntime.InvokeVoidAsync("downloadFile", ...)` over the circuit (PDF cap is 20 MB, `LocalFileStorage.cs:17`).
- **Impact:** Base64 inflates payloads ~33%; the bytes traverse loopback HTTP → server memory → SignalR WebSocket → browser instead of a plain HTTP download. Large avatars/PDFs stall the circuit (all UI interactivity shares that connection) and bloat per-circuit memory.
- **Recommendation:** Serve both via direct HTTP endpoints and plain `<img src>`/anchor downloads — the repo already has exactly this pattern with auth (`Controllers/LessonAssetsController.cs`, JWT-in-query support at `AuthenticationSetupExtensions.cs:146-158`) and caching (`Controllers/MediaController.cs:71`).

### PERF-6: `@bind:event="oninput"` sends every keystroke through the circuit  [Medium] [Effort: S]
- **Evidence:** `Shared/Components/Chat/MessagePane.razor:105` (chat message textarea), `Shared/Components/Assistant/AssistantWidget.razor:51` (assistant input), `Shared/Components/Chat/UserPickerModal.razor:9` and `Shared/Components/Call/CallUserPickerModal.razor:7` (search boxes). Global InteractiveServer (`Program.cs:95-96`) means each input event is a SignalR round-trip plus a server-side render/diff.
- **Impact:** Typing a chat message generates one server round-trip and render per keystroke, per user — the single chattiest interaction in the app, multiplied across all typing users. (The search boxes at least debounce their *API* calls, but still round-trip every keystroke for binding.)
- **Recommendation:** For the chat textarea and assistant input, bind on `onchange` and read the value on send, or move send-button enablement client-side (small JS helper). The only server-side need per keystroke — the send button's disabled state — can be tolerated at `onchange` granularity or handled in JS.

### PERF-7: ChatHub.SendMessage does two avoidable identity queries per message  [Medium] [Effort: S]
- **Evidence:** `Web/Hubs/ChatHub.cs:91-99` — per message: `userManager.FindByIdAsync(userId)` and `userManager.GetRolesAsync(sender)` (two DB queries) purely to build the sender's display name and role for the DTO. The principal already carries `firstName`, `lastName`, and role claims minted at sign-in (`Startup/InfrastructureEndpointsExtensions.cs:125-135`) and available as `Context.User` in the hub.
- **Impact:** The chat send hot path costs 4 DB operations (conversation fetch, two identity lookups, save) where 2 suffice. At sustained chat volume this doubles identity-table read traffic for zero information gain.
- **Recommendation:** Read name/role from `Context.User` claims (keep the `IsEnabled` re-check if desired — or fold it into the connection-level check that `OnConnectedAsync` already performs, `ChatHub.cs:34-52`).

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

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| PERF-1 | High | L | Cache per-circuit JWT + signing credentials now; move to in-process service calls with ARCH-1 |
| PERF-3 | Medium | S | Project blog summaries in SQL instead of loading full bodies |
| PERF-6 | Medium | S | Drop per-keystroke `oninput` binding on chat/assistant inputs |
| PERF-7 | Medium | S | Build chat sender name/role from claims, not two DB queries per message |
| PERF-4 | Medium | M | Move QuestPDF rendering off the lesson-completion request path |
| PERF-5 | Medium | M | Serve avatars/certificates via direct HTTP endpoints, not the circuit |
| PERF-8 | Low | S | Single-fetch the newest chat page on conversation open |
| PERF-9 | Low | S | Collapse the duplicate paged query in `GetPublishedCoursesAsync` |
| PERF-10 | Low | S | Diff assistant index on projected hashes, not full rows |
| PERF-11 | Low | S | Add `AsNoTracking` to the remaining read-only queries |
| PERF-12 | Low | S | Add response compression + static-asset cache headers |

## 5. Related Findings Elsewhere

- **ARCH (21):** ARCH-1 owns the loopback-HTTP-to-self design that PERF-1 costs out; ARCH-7 owns the global InteractiveServer decision behind PERF-6's per-keystroke traffic.
- **REL (26):** REL-7 owns the per-request `FindByIdAsync` in both auth hooks — the single biggest per-call item inside PERF-1's pipeline; REL-3 owns the consumer error-swallowing on those same calls.
- **DB (30):** DB-2 owned the nvarchar(48) schema fix underlying PERF-2 (both now fixed — columns are native `datetimeoffset`); DB-8 (unbounded blobs) amplifies PERF-3's over-fetch.
- **SCALE (35):** Owns the multi-instance/user-growth consequences of the same hot paths (presence fan-out, circuit memory, chunk-cache growth).
- **AVAIL (36):** Owns timeout/resilience of the loopback HttpClients whose per-call cost PERF-1 describes.
- **UX (33):** Perceived-latency consequences (loading states during multi-call page loads) are UX territory.
