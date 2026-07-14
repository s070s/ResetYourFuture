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
| Medium | 0 |
| Low | 2 |
| Info | 1 |

> **Accepted since audit (out of scope — will not implement):** SCALE-1 (call/presence state is a process-local singleton), SCALE-2 (no SignalR backplane) and SCALE-3 (loopback self-call topology assumes one addressable self) all only bite when the app runs as more than one instance, and all require new infrastructure (a Redis-backed shared registry + SignalR backplane) or removing the loopback topology. This is a single-instance university certificate project with a deliberate zero-new-infrastructure / fresh-clone story, so multi-instance operation is consciously out of scope and these three are accepted as documented limitations rather than fixed. Matching notes: [21-audit-architecture.md](21-audit-architecture.md) (ARCH-1) and [36-audit-availability.md](36-audit-availability.md) (AVAIL-4).
>
> **The four remaining multi-instance / growth-scale Mediums are accepted on the same basis:** **SCALE-4** (DataProtection keys are DPAPI-wrapped — DPAPI is the correct at-rest protection for a single Windows host; swapping to a certificate/Key Vault is only needed cross-node), **SCALE-5** (presence fan-out is O(N) per transition plus a per-flip user-row write — "fine at seminar scale" per the finding; scoping the broadcast is a growth-time redesign), **SCALE-6** (uploaded files live on local disk — correct and simplest for one persistent host; blob storage is new cloud infrastructure), and **SCALE-9** (assistant index version/reindex signal are per-instance — the in-memory version is authoritative when there is exactly one process). All four are correct as-is for single-instance operation and require infrastructure the project deliberately avoids. **SCALE-7** (per-instance cache/rate-limiter semantics) was addressed by documenting the limitation in code at each registration site, and **SCALE-8** (circuit-memory growth) was fixed by making `CircuitOptions` retention explicit (PERF-5 having already removed the largest per-circuit payload).

This is a deliberately single-instance application, and — to its credit — it mostly *knows* it: the DataProtection registration and the consumer registration both carry accurate comments describing their multi-instance limitations, `CallRegistry` is a pure, lock-protected state machine that could be re-backed without touching callers, and `IFileStorage` is a clean seam in front of the local disk. The remaining blockers are real and mutually reinforcing: live call/presence state, SignalR group routing, uploaded files, caches, rate limiters, and the assistant index are all process-local, so a second instance doesn't just degrade — chat delivery, calls, and presence all break in visible ways (sign-in itself no longer breaks on key-ring mismatch now that keys are shared, though DPAPI's machine lock is still a gap for a truly heterogeneous fleet). Within one instance, growth cost is dominated by Blazor Server circuit memory and a per-user connection multiplier (each authenticated user consumes a browser circuit plus 1-2 server-side loopback SignalR client connections), and by presence broadcasts that scale O(N) per transition to all connections.

## 3. Findings

> The six Medium findings (SCALE-4 through SCALE-9) are resolved: **SCALE-8** was fixed (explicit `CircuitOptions` retention — see git `Fix SCALE-8`), **SCALE-7** was addressed by documenting the per-instance cache/limiter semantics in code, and **SCALE-4, SCALE-5, SCALE-6, SCALE-9** are accepted as documented multi-instance / growth-scale limitations (see the accepted-limitations note in §2). All four are correct as-is for this single-instance deployment and would require infrastructure the project deliberately avoids. The remaining open items are two Low and one Info.

### SCALE-10: Assistant retrieval keeps the whole index in RAM per instance and scans it linearly per question  [Low] [Effort: M]
- **Evidence:** `AssistantChunkCache` holds every chunk (`Application/Common/AssistantChunkCache.cs:9-16` — explicitly designed for "hundreds of rows"); each query cosine-scans all chunks of the language (`AssistantRetrievalService.cs:27-34`); embeddings ship as `varbinary` and decode to a `float[]` per chunk (`EmbeddingCodec.cs`).
- **Impact:** Sound today (documented, DB-14 agrees). Growth math: 10k sources × 2 languages × a few chunks × 1024-dim float32 ≈ hundreds of MB per instance plus multi-ms scans per question — the design has a content-volume ceiling, not a user-volume one.
- **Recommendation:** No action now. When content scales past ~10-50k chunks, move ranking to a vector index (SQL Server vector support or a dedicated store) behind `IAssistantRetrievalService`, which already isolates the strategy.

### SCALE-11: Chat/message and call-history tables grow without bound, and every conversation open re-counts them  [Low] [Effort: S]
- **Evidence:** `ChatMessages` has no retention/archival path anywhere in the repo; `GetMessagesAsync` runs `COUNT(*)` over a conversation's full history per page view (`ChatQueryService.cs:112`), and `GetUnreadCountAsync` joins messages×conversations per badge refresh (`:264-273`). `CallSessions`/`CallParticipants` likewise accumulate forever. (Former DB-11 flagged the same pattern for RefreshTokens — fixed via `RefreshTokenPurgeService`, a `BackgroundService` sweep built for COMP-5's retention finding, which this could follow the same pattern for.)
- **Impact:** Purely a growth slope, not a cliff: counts and unread scans degrade gradually as history accumulates. Indexes on `(ConversationId)` exist via FKs, so this stays cheap until conversations reach tens of thousands of messages.
- **Recommendation:** Accept for now; when needed, maintain unread counters on the conversation row (the `LastMessageContent` cache shows the pattern) and add an archival policy following `RefreshTokenPurgeService`'s sweep pattern.

### SCALE-12: WebRTC media is peer-to-peer mesh — the server scales with calls' signaling only  [Info]
- **Evidence:** `CallHub.Signaling.cs` relays SDP/ICE without inspecting payloads; media never transits the server (hub comment, `CallHub.cs:16-18`). Mesh size is capped at 6 participants (`WebRtc:MaxParticipants`, `appsettings.json:53-61`); ICE is STUN-only (no TURN server configured).
- **Impact:** Positive for scale: call bandwidth/CPU lives on clients. The 6-cap is the right guard for mesh topology (each participant uploads N-1 streams). STUN-only means calls fail behind symmetric NATs — a connectivity/availability concern for real users, but adding TURN is infrastructure (CLOUD 41).
- **Recommendation:** None for scale. Revisit topology (SFU) only if participant caps ever need to rise.

## 4. Prioritized Action List

All six Medium items (SCALE-4 through SCALE-9) are resolved — SCALE-8 fixed, SCALE-7 documented, SCALE-4/5/6/9 accepted as single-instance limitations (§2). The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| SCALE-11 | Low | S | Unread counters on conversation rows; archival policy later |
| SCALE-10 | Low | M | Vector index behind IAssistantRetrievalService when content volume demands |

## 5. Related Findings Elsewhere

- **ARCH (21):** ARCH-1 owns the loopback design behind SCALE-3; ARCH-10 describes the calls-state design SCALE-1 re-backs; ARCH-7 owns the render-mode decision behind SCALE-8; ARCH-8 documents the hub-owning service lifetimes SCALE-3 counts connections for.
- **PERF (34):** PERF-5 (data-URL avatars) is the largest single per-circuit memory item in SCALE-8; PERF-1 quantifies the per-call cost of the loopback topology.
- **AVAIL (36):** Single-instance pinning (SCALE-1/2/4) is why zero-downtime deploys are impossible — AVAIL owns the restart/drain consequences.
- **DB (30):** former DB-11 (RefreshToken growth, fixed) was the same unbounded-growth pattern as SCALE-11; DB-14 endorses the current chunk-storage design SCALE-10 puts a ceiling on.
- **SEC (25):** SEC-3 added a per-user rate limiter on several previously-unprotected endpoints; SCALE-7's per-instance limiter multiplication applies to it (and every other ASP.NET Core rate limiter here) the same way.
- **CLOUD (41):** Owns the concrete provisioning (Redis, blob storage, TURN, load balancer) that the High findings here would consume.
