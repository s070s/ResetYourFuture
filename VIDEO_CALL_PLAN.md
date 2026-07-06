# Video Calls (1:1 + Group) from Chat — Implementation Plan

## Context

Users should be able to start person-to-person and group video calls from the existing chat. The chat today is strictly 1:1 (`ChatConversation` = creator + one participant), real-time via SignalR `ChatHub` (`/hubs/chat`), gated by PrioritySupport subscription or Admin role.

**Confirmed decisions (user):**
- Self-hosted WebRTC, SignalR signaling, P2P mesh for groups (cap 6 participants).
- Features: mic mute, camera toggle, screen share, call events persisted in chat history.
- "Ring anywhere": incoming-call overlay pops up on any page.
- Group calls are **independent call sessions** (N participants, not tied to a conversation). 1:1 calls start from the conversation header; group calls from a multi-select picker; participants can be added mid-call.
- Access gate identical to chat: Admin OR PrioritySupport feature.

## Two verified blockers (must fix first)

1. **`Permissions-Policy` header blocks camera/mic site-wide.** [SecurityHeadersMiddlewareExtensions.cs:16](src/ResetYourFuture.Web/Startup/SecurityHeadersMiddlewareExtensions.cs) sends `camera=(), microphone=(), geolocation=()`. Change to `camera=(self), microphone=(self), display-capture=(self), geolocation=()`. (CSP is fine: `media-src 'self' blob:` exists; `srcObject` streams aren't governed by CSP.)
2. **JWT query-string auth is path-restricted.** `AuthenticationSetupExtensions.cs` `OnMessageReceived` (~line 146) only accepts `?access_token=` for `/hubs/chat` and `/api/lessons`. Add `path.StartsWithSegments("/hubs/call")`.

**Registration gotcha:** `ChatService` is registered via `AddHttpClient<IChatService, ChatService>` (ServiceRegistrationExtensions.cs:189) → **transient**, new instance per injection. CallService must instead be **`AddScoped`** (one per circuit) so `CallOverlayHost` (MainLayout) and chat components share the same instance/hub connection/state. CallService is hub-only (no REST), so it needs no HttpClient.

## Architecture

```
Browser A ◄─SignalR─► CallHub /hubs/call ◄─SignalR─► Browser B
CallService(scoped)     ├ CallRegistry (singleton, in-memory state)
webrtc-interop.js       ├ CallRingMonitor (BackgroundService, 5s poll)
CallOverlayHost         └ ICallEventService (persists CallSession/Participant/ChatMessage)
   └────────── WebRTC media P2P mesh (never touches server) ──────────┘
```

- Separate `CallHub`; `ChatHub` untouched (its `IHubContext<ChatHub>` is reused to push call-event chat messages live).
- Signaling identity = SignalR `ConnectionId` (peerKey). Registry maps connectionId→userId.
- Mesh join convention: **newcomer offers to each existing participant** (no glare on join). Perfect negotiation with deterministic polite/impolite roles (lower `string.CompareOrdinal` connectionId = impolite) for renegotiations.
- Screen share via `RTCRtpSender.replaceTrack` (no renegotiation, auto-revert on `track.onended`); renegotiation path exists as machinery for ICE restart.
- The global call-hub connection doubles as presence: offline callee = not in registry connection tracker.
- Server-side transient state in singleton `CallRegistry` (pure, lock-protected, zero deps — fully unit-testable). Ring timeout (45s) handled by `CallRingMonitor : BackgroundService` polling `TakeExpiredInvites` every 5s; also sweeps dangling DB sessions at startup (server restart mid-call).

## Work packages (one commit each)

### WP1 — Domain + EF + migration
New:
- `src\ResetYourFuture.Domain\Domain\Entities\CallSession.cs` — Id, InitiatorId (+nav), `Guid? ConversationId` (non-null ⇒ 1:1 from chat, `SetNull` on conversation delete), StartedAt (ring began), ConnectedAt (first accept), EndedAt, `CallEndReason? EndReason`, Participants.
- `src\ResetYourFuture.Domain\Domain\Entities\CallParticipant.cs` — CallSessionId, UserId, `CallParticipantStatus Status`, InvitedAt, JoinedAt?, LeftAt?.
- `src\ResetYourFuture.Domain\Domain\Enums\` — `CallParticipantStatus` {Invited, Joined, Declined, Missed, Left}, `CallEndReason` {Completed, Missed, Declined, Cancelled}, `CallEventKind` {Started, Missed, Ended}.
- `src\ResetYourFuture.Infrastructure\Data\Configurations\CallSessionConfiguration.cs` + `CallParticipantConfiguration.cs` — Initiator/User FK `Restrict`; Conversation FK `SetNull`; CallSession→Participants `Cascade`; unique index `(CallSessionId, UserId)`; indexes `(InitiatorId, StartedAt)`, `EndedAt`; enums as int.

Modified:
- `ChatMessage.cs` — add `Guid? CallSessionId` (+nav, `SetNull`) **and** `CallEventKind? CallEvent`. Two columns because one session yields two messages (Started, Ended) and localized text must be rendered at display time (bilingual site — never bake language into `Content`). `Content` gets a plain-English fallback ("Missed call", "Video call ended · 12:34") only for the `LastMessageContent` sidebar cache.
- `ChatMessageConfiguration.cs`, `ApplicationDbContext.cs` + `IApplicationDbContext.cs` (add `DbSet<CallSession>`, `DbSet<CallParticipant>`).

Migration: `dotnet ef migrations add AddCallSessions --project src/ResetYourFuture.Infrastructure --startup-project src/ResetYourFuture.Web` (all additive). ⚠ Memory note: plain `dotnet build/restore` can auto-pin an incompatible Microsoft.OpenApi version — if the build breaks after restore, `git checkout` the csproj/Directory.Packages.props.

### WP2 — Application layer
New:
- `src\ResetYourFuture.Application\DTOs\Call\CallDtos.cs` — `CallInviteDto(CallId, InitiatorId, InitiatorName, ConversationId?, IsGroup, ParticipantNames)`, `CallParticipantDto(UserId, DisplayName, ConnectionId, MicOn, CameraOn, ScreenSharing)`, `StartCallResultDto(CallId?, StartCallStatus, UnavailableUserIds)`, `StartCallStatus` {Ringing, AllUnavailable, CallerBusy, NoAccess}, `JoinCallResultDto(CallId, ExistingParticipants)`, `MediaStateDto(MicOn, CameraOn, ScreenSharing)`.
- `ApiInterfaces\ICallEventService.cs` + `ApiServices\CallEventService.cs` — `CreateSessionAsync(initiatorId, conversationId?, inviteeIds)`, `MarkParticipantAsync(callId, userId, status)` (sets JoinedAt/LeftAt; session ConnectedAt on first Joined), `EndSessionAsync(callId, reason)`, `RecordChatEventAsync(callId, kind)` → writes ChatMessage call-event row + updates conversation `LastMessageContent/LastMessageAt` cache, returns `ChatMessageDto` for broadcasting (null for group calls).
- `ApiInterfaces\ICallQueryService.cs` + `ApiServices\CallQueryService.cs` — `HasCallAccessAsync(userId, isAdmin)` (same rule as `ChatQueryService.HasChatAccessAsync`: Admin OR PrioritySupport via `ISubscriptionService`); `GetCallableUsersAsync(callerId, search)` — **do not reuse** `GET /api/chat/users`: `ChatQueryService.GetAvailableUsersAsync` deliberately excludes users you already have conversations with, which is exactly wrong for calling. Return all enabled users minus self.

Modified:
- `DTOs\Chat\ChatDtos.cs` — extend `ChatMessageDto` with trailing defaults: `CallEventKind? CallEvent = null, int? CallDurationSeconds = null` (keeps existing constructor calls compiling).
- `ApiServices\ChatQueryService.cs` — `GetMessagesAsync` projection: left-join CallSessions to compute `CallDurationSeconds` (ConnectedAt→EndedAt) and pass `CallEvent` through.

### WP3 — Server real-time
New (all in `src\ResetYourFuture.Web\`):
- `Hubs\CallHub.cs` (~330 lines; split `CallHub.Signaling.cs` partial if it grows) — `[Authorize]`, mirrors ChatHub: OnConnected enabled-check + `user_{userId}` group; per-call `call_{callId}` groups. Methods: `StartCall(List<string> calleeIds, Guid? conversationId)`, `AcceptCall(callId)`, `DeclineCall`, `CancelCall`, `LeaveCall`, `InviteToCall(callId, userId)`, `RejoinCall(callId)`, `SendOffer/SendAnswer/SendIceCandidate(callId, targetConnectionId, payload)` (server validates BOTH connections' call membership before relaying), `UpdateMediaState`, `GetCallableUsers(search)`. Client events: `IncomingCall`, `IncomingCallHandled` (dismiss other tabs), `CallAccepted`, `CallDeclined`, `CallCancelled`, `CallUnavailable`, `ParticipantJoined/Left/Reconnected`, `ParticipantMediaChanged`, `CallEnded`, `ReceiveOffer/Answer/IceCandidate`, `CallError`.
- `Services\CallRegistry.cs` — singleton pure state machine: `TryCreateCall`, `IsUserBusy`, `TryAddInvite`, `TryAccept`, `TryDecline`, `TryLeave`, `HandleDisconnect` (15s reconnect grace before treating as Left), `TryUpdateConnection` (rejoin re-key), `SetMediaState`, `GetJoinedParticipants`, `IsMemberConnection`, `TakeExpiredInvites`, `RemoveCall`.
- `Services\CallRingMonitor.cs` — BackgroundService: 5s poll → expired invites → mark Missed, notify (`CallUnavailable` to call group, `IncomingCallHandled` to callee), end call as Missed when <2 joined and no pending invites (via `IHubContext<CallHub>`/`IHubContext<ChatHub>`/`IServiceScopeFactory`); startup sweep for dangling sessions.
- `Services\WebRtcOptions.cs` — bound from config: `RingTimeoutSeconds` (45), `MaxParticipants` (6), `IceServers` (urls + optional username/credential so TURN is config-only later).

Modified:
- `Program.cs` — `app.MapHub<CallHub>("/hubs/call");`
- `Startup\AuthenticationSetupExtensions.cs` — blocker fix 2.
- `Startup\SecurityHeadersMiddlewareExtensions.cs` — blocker fix 1.
- `Startup\ServiceRegistrationExtensions.cs` — `AddSingleton<CallRegistry>()`, `AddHostedService<CallRingMonitor>()`, `AddScoped<ICallEventService, CallEventService>()`, `AddScoped<ICallQueryService, CallQueryService>()`, `AddScoped<ICallService, CallService>()` (WP5), `Configure<WebRtcOptions>(...)`.
- `appsettings.json` — `"WebRtc": { "RingTimeoutSeconds": 45, "MaxParticipants": 6, "IceServers": [{ "Urls": ["stun:stun.l.google.com:19302"] }] }`.

Key flows:
- **StartCall**: access gate → caller-busy check → filter callees to online (registry = presence); offline callees → `UnavailableUserIds`. 1:1 with sole callee offline: end immediately as Missed + missed-call chat event pushed via `IHubContext<ChatHub>` `ReceiveMessage` to both `user_` groups. Otherwise create session (DB) + registry entry, add caller to `call_` group, `IncomingCall` → each `user_{calleeId}` group (rings all tabs).
- **AcceptCall**: registry `TryAccept` → `IncomingCallHandled` to callee's user group (other tabs dismiss) → join `call_` group → persist Joined (+ConnectedAt; first accept of 1:1 also `RecordChatEventAsync(Started)` → broadcast over ChatHub) → return existing participants (newcomer offers to each) → `ParticipantJoined` to group.
- **End condition**: joined count <2 AND no ringing invites → `EndSessionAsync(reason)`, 1:1 chat event (Ended+duration / Missed), `CallEnded` to group, registry cleanup.

### WP4 — JS interop
New: `src\ResetYourFuture.Web\wwwroot\js\webrtc-interop.js` (~420 lines; if >500 split `webrtc-media.js`/`webrtc-peers.js` merged into one namespace via `Object.assign`) — `window.webrtcInterop`: `init(dotNetRef, iceServers)`, `startLocalMedia(audio, video)`, `createPeer(peerKey, polite)` (perfect-negotiation handlers), `initiateOffer(peerKey)`, `setRemoteDescription(peerKey, type, sdp)` (glare/rollback for polite peer), `addIceCandidate` (queue until remote description set), `closePeer/closeAll`, `setMicEnabled/setCameraEnabled` (`track.enabled`, no renegotiation), `startScreenShare` (getDisplayMedia → `replaceTrack` on every video sender; `onended` → auto-revert), `stopScreenShare`, `bindLocalVideo(elementId)`, `bindRemoteVideo(peerKey, elementId)`.
.NET callbacks (`[JSInvokable]` on CallService): `OnLocalDescription` (→ SendOffer/SendAnswer), `OnIceCandidate`, `OnPeerConnectionState` (drives tile spinner; "failed" → one `restartIce()` attempt), `OnRemoteStreamChanged`, `OnScreenShareEnded`, `OnMediaError`.

Modified: `App.razor` — `<script defer src="js/webrtc-interop.js"></script>` beside chat-interop.js.

### WP5 — Client CallService
New:
- `Interfaces\ICallService.cs` — `CallStage` {Idle, Outgoing, Incoming, Connecting, InCall}; state props (Stage, IncomingInvite, ActiveCallId, Participants w/ per-peer connection state, MicOn/CameraOn/ScreenSharing, IsConnected); `event StateChanged`, `event ErrorOccurred`; `EnsureConnectedAsync(circuitUser)` (idempotent), `StartCallAsync(userIds, conversationId?)`, `AcceptAsync/DeclineAsync/CancelAsync/HangUpAsync`, `InviteAsync`, `ToggleMicAsync/ToggleCameraAsync/ToggleScreenShareAsync`, `GetCallableUsersAsync`, `BindVideoAsync/BindLocalVideoAsync`, `RequestGroupCallPicker()`.
- `Services\CallService.cs` + `Services\CallService.Media.cs` (partial for JS interop + `[JSInvokable]` callbacks) — copies ChatService's hub patterns: hub URL from `IHttpContextAccessor` at ctor (HttpContext null in circuit); `AccessTokenProvider = () => _authService.GetTokenAsync(circuitUser)`; `WithAutomaticReconnect()`; `Reconnected += ` → `RejoinCall(ActiveCallId)`. Accept flow: `startLocalMedia` → hub `AcceptCall` → `createPeer`+`initiateOffer` per existing participant. All JS calls try/catch → `ErrorOccurred`; dispose/hang-up always `closeAll()`. **Registered `AddScoped`, never disposed by pages.**

### WP6 — UI + localization (feature usable after this)
New under `src\ResetYourFuture.Web\Shared\Components\Call\`:
- `CallOverlayHost.razor(+.cs,.css)` — the ONLY stateful mount, added to `MainLayout.razor` after `<main>`. AuthorizeView-gated; in `OnAfterRenderAsync(firstRender)` only (prerender: true!) check access (same as Chat.razor.cs: Admin role or `SubscriptionConsumer` PrioritySupport) then `CallService.EnsureConnectedAsync(user)`; subscribes StateChanged/ErrorOccurred; renders children by Stage.
- `IncomingCallToast.razor(+.css)` — fixed bottom-right card: caller name, Accept/Decline.
- `ActiveCallView.razor(+.cs,.css)` — full-screen overlay, CSS grid 1–6 tiles, local self-view (`<video id="video-local" muted autoplay playsinline>`), "Ringing…"+Cancel for Outgoing stage.
- `ParticipantTile.razor(+.css)` — `<video id="video-@ConnectionId" autoplay playsinline>`, name, mic/camera/sharing badges, connecting spinner; `OnAfterRenderAsync` → `BindVideoAsync`.
- `CallControls.razor(+.css)` — mic/camera/screen-share/add-participant/hang-up, aria-labels from CallRes.
- `CallUserPickerModal.razor(+.cs)` — multi-select variant modeled on existing `UserPickerModal` (search + debounce); dual mode: start-group-call | add-to-active-call; cap-6 enforcement in UI.

Modified:
- `Layout\MainLayout.razor` — `<CallOverlayHost />`.
- `_Imports.razor` — `@using ResetYourFuture.Web.Shared.Components.Call`.
- `Shared\Components\Chat\MessagePane.razor(+.cs,.css)` — (a) video-call button in header (disabled when `Stage != Idle`) → `StartCallAsync([OtherUserId], ConversationId)`; (b) render call-event messages (`msg.CallEvent is not null`) as a centered localized chip (`CallRes.CallStarted` / `MissedCall` / `CallEndedWithDuration` + formatted duration) instead of a bubble.
- `Shared\Components\Chat\ConversationSidebar.razor` — "new group call" button → `RequestGroupCallPicker()`.

Localization: `src\ResetYourFuture.Shared\Resources\CallRes.resx` + `CallRes.el.resx` + **hand-written** `CallRes.Designer.cs` (copy ChatRes.Designer.cs structure — no auto-regen tooling in this repo). ~25 keys: IncomingVideoCall, Accept, Decline, Ringing, Cancel, HangUp, Mute, Unmute, CameraOn/Off, ShareScreen, StopSharing, AddParticipant, StartGroupCall, CallStarted, MissedCall, CallEndedWithDuration, UserBusy, UserUnavailable, CallFailed, MediaPermissionDenied, ParticipantLimitReached, You…

### WP7 — Tests + manual verification
- `tests\ResetYourFuture.Web.Tests\CallHubTests.cs` (mirror ChatHubTests: NSubstitute hub context, in-memory db, mocked UserManager, Shouldly): disabled-user abort; no-subscription → NoAccess; admin bypass; offline 1:1 callee → Missed chat message persisted; caller busy; accept → group add + Joined + Started chat event; decline 1:1 → Declined end; **relay to non-member connection refused** (security); media-state broadcast; last-peer disconnect → duration persisted; 7th invite rejected.
- `tests\ResetYourFuture.Web.Tests\CallRegistryTests.cs` — pure state machine incl. concurrent double-accept, expired-invite boundary, connection re-keying.
- `tests\ResetYourFuture.Application.Tests\CallEventServiceTests.cs` — ConnectedAt set once; duration math; group call writes no chat message; each CallEventKind's Content fallback + conversation cache update.
- Update `ChatQueryServiceTests` for CallEvent/CallDurationSeconds projection.
- Integration: `/hubs/call/negotiate` 401 without token (mirror ChatIntegrationTests).

## Edge cases handled (design summary)
- Cancel-before-answer, decline, 45s timeout → all dismiss overlays via `user_{id}` groups + persist Missed/Declined; 1:1 writes missed-call chat event.
- Two tabs: both ring; first accept wins; `IncomingCallHandled` dismisses others.
- Busy: checked at StartCall/Invite/Accept; simultaneous cross-call → second gets CallerBusy.
- getUserMedia denied → toast, state unchanged, no auto-decline (retry possible).
- SignalR reconnect mid-call: media continues P2P; 15s grace; `RejoinCall` re-keys connection + re-adds group; ICE failure → `restartIce()` once.
- Circuit death/tab close → hub OnDisconnected is authoritative → participant Left → end-call path.
- Server restart → registry lost; clients tear down when RejoinCall returns null; startup sweep closes dangling DB rows.

## Verification (end-to-end)
1. `dotnet build` + `dotnet test` after each WP.
2. Apply migration; inspect schema.
3. Two-browser manual matrix (Chrome + Edge/incognito, two Pro/Admin accounts):
   1:1 call from conversation header rings the other browser on the home page → accept → two-way A/V; "call started" appears live in both chat panes. Mute/camera badges propagate. Screen share + browser-chrome stop → auto-revert. Hang up → "Call ended · m:ss" + `CallSessions` row complete. Decline/45s-timeout/cancel → correct chat events + overlay dismissals. Group A→B+C staggered accepts → 3-way mesh (verify 2 RTCPeerConnections per browser in `chrome://webrtc-internals`), add D mid-call. Refresh callee tab → call ends with duration. Dev-tools offline 5s → call survives reconnect. Second tab dismissal. Non-Pro user sees no buttons and hub rejects. Deny camera → friendly error, retry works. el-GR culture fully localized.

Note (production-only, out of scope): users behind symmetric NAT need a TURN server — config-only via `WebRtc:IceServers`. Irrelevant for localhost/LAN demo per project purpose.
