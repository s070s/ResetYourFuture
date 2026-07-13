# Audit: Scalability

| | |
|---|---|
| Finding prefix | SCALE |
| Created | 2026-07-11 |
| Scope | Blockers to running more than one instance and to user/content growth: in-memory singleton state (CallRegistry, AssistantChunkCache, IMemoryCache), SignalR backplane absence, DataProtection key locality, local-disk file storage, loopback self-calls under load balancing, per-user circuit and connection cost, and per-instance rate limiting. |
| Delegated | Per-request/per-render cost fixable by code optimization → PERF (34). Failure modes, boot behaviour, uptime → AVAIL (36). The loopback architecture's design critique → ARCH (21, ARCH-1); the calls-feature state design description → ARCH-10. Schema/index concerns → DB (30). Deployment topology / infrastructure choices (Redis, blob storage, TURN) as concrete provisioning → CLOUD (41). |

## 1. Methodology

Enumerated every stateful singleton and process-local resource: `CallRegistry` (`Web/Services/CallRegistry.cs`), `AssistantChunkCache` / `AssistantIndexVersion` / `AssistantIndexSignal` (`Application/Common/*`, `Web/Services/AssistantIndexSignal.cs`), `IMemoryCache` users (`SubscriptionService`, `AssistantService`, sitemap endpoint), rate limiters (`ServiceRegistrationExtensions.cs:145-167`), DataProtection (`:178-186`), `LocalFileStorage`, and the `FileLoggerProvider` log directory. Traced SignalR topology — both hubs (`Program.cs:93-94`), group usage (`ChatHub`, `CallHub`), `Clients.All` broadcasts, and the server-side hub *clients* each circuit opens (`ChatService.StartAsync`, `CallService.EnsureConnectedAsync`, mounted globally by `Shared/Components/Call/CallOverlayHost.razor` in MainLayout). Reviewed `SelfBaseUrl` handling (`ServiceRegistrationExtensions.cs:211-216`, `appsettings.json:10`), circuit configuration (none found — searched `CircuitOptions`/`HubOptions`), presence persistence (`CallHub.PersistLastSeenAsync`), and assistant retrieval/index growth characteristics.

NOT examined: load testing or capacity measurement (static analysis only); the WebRTC media plane beyond confirming it is peer-to-peer (media never transits the server); concrete cloud service selection → CLOUD (41).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 6 |
| Low | 2 |
| Info | 1 |

> **Accepted since audit (out of scope — will not implement):** SCALE-1 (call/presence state is a process-local singleton), SCALE-2 (no SignalR backplane) and SCALE-3 (loopback self-call topology assumes one addressable self) all only bite when the app runs as more than one instance, and all require new infrastructure (a Redis-backed shared registry + SignalR backplane) or removing the loopback topology. This is a single-instance university certificate project with a deliberate zero-new-infrastructure / fresh-clone story, so multi-instance operation is consciously out of scope and these three are accepted as documented limitations rather than fixed. The remaining SCALE findings (SCALE-4…12) stay open as within-a-single-instance concerns. Matching notes: [21-audit-architecture.md](21-audit-architecture.md) (ARCH-1) and [36-audit-availability.md](36-audit-availability.md) (AVAIL-4).

This is a deliberately single-instance application, and — to its credit — it mostly *knows* it: the DataProtection registration and the consumer registration both carry accurate comments describing their multi-instance limitations, `CallRegistry` is a pure, lock-protected state machine that could be re-backed without touching callers, and `IFileStorage` is a clean seam in front of the local disk. The remaining blockers are real and mutually reinforcing: live call/presence state, SignalR group routing, uploaded files, caches, rate limiters, and the assistant index are all process-local, so a second instance doesn't just degrade — chat delivery, calls, and presence all break in visible ways (sign-in itself no longer breaks on key-ring mismatch now that keys are shared, though DPAPI's machine lock is still a gap for a truly heterogeneous fleet). Within one instance, growth cost is dominated by Blazor Server circuit memory and a per-user connection multiplier (each authenticated user consumes a browser circuit plus 1-2 server-side loopback SignalR client connections), and by presence broadcasts that scale O(N) per transition to all connections.

## 3. Findings

### SCALE-4: DataProtection keys are still DPAPI-protected — a second (or non-Windows) node can't decrypt them  [Medium] [Effort: S]
- **Evidence:** `ServiceRegistrationExtensions.cs` — keys now persist to the shared database (`PersistKeysToDbContext<ApplicationDbContext>`, fixed), but `ProtectKeysWithDpapi()` still wraps them at rest on Windows.
- **Impact:** Narrowed from the original finding: the key *ring* is now shared, but DPAPI still machine-locks each key's encryption, so a second instance (or a container rebuilt on different hardware) can read the row but not decrypt it — sign-in would still fail cross-node. No longer a data-loss risk (keys survive a redeploy on the same machine); downgraded from High since the more severe half is fixed.
- **Recommendation:** Before any real multi-instance/cross-platform target: swap `ProtectKeysWithDpapi()` for `ProtectKeysWithCertificate()` (a cert every instance can load) or Key Vault, per the original recommendation.

### SCALE-5: Presence fan-out is O(all connections) per transition, plus a user-row write per online/offline flip  [Medium] [Effort: M]
- **Evidence:** `CallHub.OnConnectedAsync`/`OnDisconnectedAsync` broadcast `Clients.All.SendAsync("PresenceChanged", ...)` on every first-connect/last-disconnect (`CallHub.cs:63-67, 94-101`) — to every connection of every user, since `CallOverlayHost` connects everyone. Each transition also writes `LastSeenAt` via `userManager.UpdateAsync` (full Identity row update, `:112-123`). Every circuit seeds presence by pulling the complete online-user list (`GetOnlineUsers` → `CallRegistry.GetOnlineUserIds`, `PresenceService.SeedAsync`), and each `PresenceChanged` re-renders every mounted `PresenceIndicator` in every circuit (`PresenceIndicator.razor:55`).
- **Impact:** With N connected users, login/logout churn generates O(N) messages per event — O(N²) system-wide message volume as usage grows — plus render work in every circuit and a DB write per flip. Fine at seminar scale; the first quadratic curve the platform will hit as users grow.
- **Recommendation:** Scope presence to interested parties (e.g., a `presence_{userId}` group subscribed only by users who share a conversation, or per-conversation groups), debounce `LastSeenAt` writes (e.g., at most once per few minutes via `ExecuteUpdate` on the single column), and keep the full-list seed but page it.

### SCALE-6: Uploaded files (avatars, lesson videos, certificate PDFs) live on the instance's local disk  [Medium] [Effort: M]
- **Evidence:** `Infrastructure/ApiServices/LocalFileStorage.cs:36-46` — base path `ContentRootPath/App_Data/Uploads`; lesson videos up to 500 MB (`:17`), certificates written by `CertificateService.GeneratePdfAsync`. Registered as the only `IFileStorage` (`ServiceRegistrationExtensions.cs:35`).
- **Impact:** A second instance can't see files uploaded through the first (missing avatars, 404 lesson assets, failed certificate downloads depending on which node serves the request), and any container/ephemeral redeploy loses all uploads. Also couples disk capacity of the web node to content volume (500 MB per video).
- **Recommendation:** The `IFileStorage` interface is already the right seam — add a blob-storage implementation and select by configuration when deploying beyond one persistent host. Until then, document the single-host requirement next to the DataProtection comment.

### SCALE-7: In-memory caches and rate limiters are per-instance — invalidation and limits don't hold across nodes  [Medium] [Effort: S]
- **Evidence:** `AddMemoryCache` (`ServiceRegistrationExtensions.cs:130`) backs the 30 s subscription-status cache with *explicit invalidation on plan change* (`SubscriptionService.cs:71-107` and its cache-removal on subscribe/cancel), the assistant status cache, and the sitemap cache. Rate limiters are the built-in in-process ones (`:145-167`).
- **Impact:** On node B, a user who just upgraded on node A keeps their old tier for up to 30 s (benign), but the *pattern* — explicit invalidation assumed to be authoritative — will mislead future longer-TTL caches. Rate limits multiply by node count (the global 10/min "auth" window becomes 10×N/min), weakening the brute-force protection SEC relies on.
- **Recommendation:** Keep TTLs short (they are), note the per-instance semantics where invalidation is used, and move rate limiting to a shared store (or the fronting proxy) when scaling out.

### SCALE-8: Blazor Server circuit memory grows linearly with concurrent users, with default circuit retention and no tuning  [Medium] [Effort: M]
- **Evidence:** Global InteractiveServer (`Program.cs:95-96`); no `CircuitOptions`/`HubOptions` configuration anywhere (verified by search). Per-circuit state includes paged lists, chat history pages, assistant transcripts (`AssistantWidget.razor.cs:23`), and avatar base64 data URLs (`AvatarDropdown.razor.cs:87` — up to ~6.7 MB per circuit for a 5 MB avatar; PERF-5). Defaults retain up to 100 disconnected circuits for 3 minutes after tab close.
- **Impact:** Every concurrent user is server RAM: UI state + renderer tree + retained disconnected circuits, on top of SCALE-3's connection multiplier. Capacity is bounded by memory per node well before CPU; growth requires either bigger nodes or the multi-instance work blocked by SCALE-1/2/4.
- **Recommendation:** Establish a per-circuit memory budget (fix PERF-5's data-URL avatars first — the single biggest per-circuit item), tune `CircuitOptions.DisconnectedCircuitMaxRetained/RetentionPeriod` to the real audience, and load-test one node to learn the ceiling before it's discovered in production.

### SCALE-9: Assistant index versioning and the reindex signal are per-instance  [Medium] [Effort: S]
- **Evidence:** `AssistantIndexVersion` and `AssistantChunkCache` are singletons (`ServiceRegistrationExtensions.cs:103-104`); `AssistantIndexer` bumps the version only in the process where it ran (`Web/Services/AssistantIndexer.cs:29-32`); the admin "reindex now" endpoint fires `AssistantIndexSignal` in the handling process only (`:95`). Retrieval refreshes its snapshot when *its* process's version advances (`AssistantRetrievalService.cs:41-52`).
- **Impact:** Multi-instance: an admin reindex refreshes one node; others serve stale chunks for up to 6 h. Every node also runs its own full 6-hourly embedding pass against the shared DB and the single Ollama sidecar — duplicate embedding compute per node.
- **Recommendation:** Make the DB the version authority (e.g., a `MAX(UpdatedAt)`/rowversion check on `AssistantContentChunks` instead of the in-memory counter) so every instance detects changes; designate one indexer (config flag) when running multiple nodes.

### SCALE-10: Assistant retrieval keeps the whole index in RAM per instance and scans it linearly per question  [Low] [Effort: M]
- **Evidence:** `AssistantChunkCache` holds every chunk (`Application/Common/AssistantChunkCache.cs:9-16` — explicitly designed for "hundreds of rows"); each query cosine-scans all chunks of the language (`AssistantRetrievalService.cs:27-34`); embeddings ship as `varbinary` and decode to a `float[]` per chunk (`EmbeddingCodec.cs`).
- **Impact:** Sound today (documented, DB-14 agrees). Growth math: 10k sources × 2 languages × a few chunks × 1024-dim float32 ≈ hundreds of MB per instance plus multi-ms scans per question — the design has a content-volume ceiling, not a user-volume one.
- **Recommendation:** No action now. When content scales past ~10-50k chunks, move ranking to a vector index (SQL Server vector support or a dedicated store) behind `IAssistantRetrievalService`, which already isolates the strategy.

### SCALE-11: Chat/message and call-history tables grow without bound, and every conversation open re-counts them  [Low] [Effort: S]
- **Evidence:** `ChatMessages` has no retention/archival path anywhere in the repo; `GetMessagesAsync` runs `COUNT(*)` over a conversation's full history per page view (`ChatQueryService.cs:112`), and `GetUnreadCountAsync` joins messages×conversations per badge refresh (`:264-273`). `CallSessions`/`CallParticipants` likewise accumulate forever. (DB-11 flags the same pattern for RefreshTokens.)
- **Impact:** Purely a growth slope, not a cliff: counts and unread scans degrade gradually as history accumulates. Indexes on `(ConversationId)` exist via FKs, so this stays cheap until conversations reach tens of thousands of messages.
- **Recommendation:** Accept for now; when needed, maintain unread counters on the conversation row (the `LastMessageContent` cache shows the pattern) and add an archival policy alongside DB-11's purge job.

### SCALE-12: WebRTC media is peer-to-peer mesh — the server scales with calls' signaling only  [Info]
- **Evidence:** `CallHub.Signaling.cs` relays SDP/ICE without inspecting payloads; media never transits the server (hub comment, `CallHub.cs:16-18`). Mesh size is capped at 6 participants (`WebRtc:MaxParticipants`, `appsettings.json:53-61`); ICE is STUN-only (no TURN server configured).
- **Impact:** Positive for scale: call bandwidth/CPU lives on clients. The 6-cap is the right guard for mesh topology (each participant uploads N-1 streams). STUN-only means calls fail behind symmetric NATs — a connectivity/availability concern for real users, but adding TURN is infrastructure (CLOUD 41).
- **Recommendation:** None for scale. Revisit topology (SFU) only if participant caps ever need to rise.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| SCALE-7 | Medium | S | Document per-instance cache/limiter semantics; shared rate limiting at scale-out |
| SCALE-9 | Medium | S | DB-authoritative assistant index version; single designated indexer |
| SCALE-4 | Medium | S | Swap ProtectKeysWithDpapi() for a certificate or Key Vault before any multi-instance attempt |
| SCALE-5 | Medium | M | Scope presence broadcasts; debounce LastSeenAt writes |
| SCALE-6 | Medium | M | Blob-backed IFileStorage implementation for multi-node/ephemeral hosts |
| SCALE-8 | Medium | M | Budget + tune circuit memory; load-test single-node ceiling |
| SCALE-11 | Low | S | Unread counters on conversation rows; archival policy later |
| SCALE-10 | Low | M | Vector index behind IAssistantRetrievalService when content volume demands |

## 5. Related Findings Elsewhere

- **ARCH (21):** ARCH-1 owns the loopback design behind SCALE-3; ARCH-10 describes the calls-state design SCALE-1 re-backs; ARCH-7 owns the render-mode decision behind SCALE-8; ARCH-8 documents the hub-owning service lifetimes SCALE-3 counts connections for.
- **PERF (34):** PERF-5 (data-URL avatars) is the largest single per-circuit memory item in SCALE-8; PERF-1 quantifies the per-call cost of the loopback topology.
- **AVAIL (36):** Single-instance pinning (SCALE-1/2/4) is why zero-downtime deploys are impossible — AVAIL owns the restart/drain consequences.
- **DB (30):** DB-11 (RefreshToken growth) is the same unbounded-growth pattern as SCALE-11; DB-14 endorses the current chunk-storage design SCALE-10 puts a ceiling on.
- **SEC (25):** SEC-3's rate-limiting gaps worsen under SCALE-7's per-instance limiter multiplication.
- **CLOUD (41):** Owns the concrete provisioning (Redis, blob storage, TURN, load balancer) that the High findings here would consume.
