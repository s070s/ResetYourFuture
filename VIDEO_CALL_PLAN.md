# Video Calls (1:1 + Group) from Chat — Implementation Plan

## Status

Implementation is happening on branch `feature/video-calls`, one commit per work package.

- [x] **WP1 — Domain + EF + migration** — done (commit `a620fbf`). `CallSession`/`CallParticipant` entities,
  enums (`CallParticipantStatus`, `CallEndReason`, `CallEventKind`), EF configurations, `ChatMessage`
  `CallSessionId`/`CallEvent` columns, `IApplicationDbContext`/`ApplicationDbContext` DbSets, and the
  `AddCallSessions` migration. Full solution build verified green.
- [x] **WP2 — Application layer** — done (commit `945d0b9`). `CallDtos.cs` (`CallInviteDto`,
  `CallParticipantDto`, `StartCallResultDto`/`StartCallStatus`, `JoinCallResultDto`, `MediaStateDto`);
  `ICallEventService`/`CallEventService` (`CreateSessionAsync`, `MarkParticipantAsync`, `EndSessionAsync`,
  `RecordChatEventAsync` — 1:1 only, returns `null` for group calls); `ICallQueryService`/`CallQueryService`
  (`HasCallAccessAsync` mirrors chat's Admin-or-PrioritySupport rule; `GetCallableUsersAsync` returns all
  enabled users minus self, deliberately not excluding existing chat partners). `ChatMessageDto` extended
  with trailing `CallEvent`/`CallDurationSeconds`; `ChatQueryService.GetMessagesAsync` now includes
  `ChatMessage.CallSession` to project duration. Not registered in DI yet (WP3). Added
  `CallEventServiceTests`/`CallQueryServiceTests`; full solution build + all 355 tests green.
- [x] **WP3 — Server real-time** — done (commit `e96b1f9`). `CallHub` (`/hubs/call`, mirrors
  ChatHub's connect/disable-check/group pattern) with `StartCall`/`AcceptCall`/`DeclineCall`/
  `CancelCall`/`LeaveCall`/`InviteToCall`/`RejoinCall`, WebRTC `SendOffer`/`SendAnswer`/
  `SendIceCandidate` relay (split into `CallHub.Signaling.cs`, validates both connections' call
  membership before relaying), `UpdateMediaState`, `GetCallableUsers`; `CallRegistry` (singleton,
  pure/lock-protected/zero-deps state machine: presence via connection ref-counting, invite/accept/
  decline/leave, expired-invite and disconnect-grace sweeps, a sticky `HasConnected` flag so
  end-of-call logic can tell "connected then everyone left" apart from "never connected" after
  participants are removed from the map, and a pure `ShouldEndCall` check); `CallRingMonitor`
  (`BackgroundService`, 5s `Task.Delay` poll loop matching `BulkStudentSeedingService`'s existing
  convention, `IServiceScopeFactory`-scoped DB/service access, startup sweep of dangling
  `CallSession` rows from a previous process). `WebRtcOptions` bound from `WebRtc` config
  (`RingTimeoutSeconds`, `MaxParticipants`, `IceServers`). Registered in DI
  (`ICallEventService`/`ICallQueryService`/`CallRegistry`/`CallRingMonitor`/`WebRtcOptions` —
  `ICallService`/`CallService` deliberately NOT registered, that's WP5). Fixed both verified
  blockers: `Permissions-Policy` now allows `camera=(self) microphone=(self) display-capture=(self)`,
  and JWT query-string auth now accepts `/hubs/call`. `CallRegistryTests` (45 facts) covers the
  state machine in isolation — busy checks, invite/accept/decline/leave transitions, expired-invite
  and disconnect-grace boundaries, rejoin re-keying, `HasConnected` stickiness, and the pure
  end-of-call decision. Full solution build + all 400 tests green (355 + 45 new). Reviewed
  (spec-compliance + code-quality passes): one spec gap found and fixed (`CallAccepted` event
  wasn't emitted on accept, commit `45ac04b`); quality review returned **Approved with minor
  notes** — one Important item deferred as a known follow-up rather than reworked now:
  `CallHub.ForceEndCall` and `CallRingMonitor.EndCallIfNeededAsync` duplicate the same
  end-of-call sequence (compute reason from `HasConnected` → `EndSessionAsync` → conditional
  1:1 chat event → `CallEnded` broadcast → `RemoveCall`); both copies are currently correct and
  consistent, but a shared helper should be extracted before end-of-call semantics change again.
- [x] **WP4 — JS interop** — done (commit `28e8266`). `wwwroot\js\webrtc-interop.js` —
  `window.webrtcInterop`: `init`, `startLocalMedia`, `createPeer` (perfect-negotiation
  handlers: `onnegotiationneeded`/`onicecandidate`/`ontrack`/`onconnectionstatechange`),
  `initiateOffer`, `setRemoteDescription` (glare/rollback per MDN's canonical
  polite/impolite algorithm, tracks `isSettingRemoteAnswerPending`), `addIceCandidate`
  (queues until remote description set), `restartIce` (one-shot guard, exposed as a
  primitive — the "retry once" policy decision belongs to CallService in WP5, which owns
  connection-failure state), `closePeer`/`closeAll`, `setMicEnabled`/`setCameraEnabled`,
  `startScreenShare`/`stopScreenShare` (`replaceTrack` across all peers, `track.onended`
  auto-revert), `bindLocalVideo`/`bindRemoteVideo` (remote stream cached on `ontrack` so
  binding works even if the `<video>` element renders after the track arrives). Script tag
  added to `App.razor` beside `chat-interop.js`. Not wired to any .NET consumer yet — no
  `DotNetObjectReference` exists until `CallService` (WP5) creates one; the `[JSInvokable]`
  callback names (`OnLocalDescription`, `OnIceCandidate`, `OnPeerConnectionState`,
  `OnRemoteStreamChanged`, `OnScreenShareEnded`, `OnMediaError`) are just the JS→.NET
  contract this module calls into once WP5 lands. Full solution build verified green
  (no C# changes in this WP).
- [x] **WP5 — Client CallService** — done (commit `224923c`). `Interfaces\ICallService.cs`
  (`CallStage` {Idle, Outgoing, Incoming, Connecting, InCall}, `CallParticipantView` client-side
  mutable view model incl. `PeerConnectionState` for the connecting spinner); `Services\CallService.cs`
  + `CallService.Media.cs` (partial: JS interop + `[JSInvokable]` callbacks). Mirrors ChatService's
  hub-bootstrap pattern (hub URL from `IHttpContextAccessor` at ctor, fresh token per (re)connect via
  `IAuthService.GetTokenAsync(circuitUser)`, `WithAutomaticReconnect()`). Mesh wiring: `CallAccepted`
  is a pure stage-transition signal (Outgoing→Connecting) with no peer side effects; `ParticipantJoined`
  is what actually creates a peer (skipped for the self-echo the group broadcast includes) — the
  newcomer (`AcceptAsync`/`RejoinAsync`, `initiateOffer: true`) offers to each existing participant,
  everyone else just creates a peer and waits for that offer (`initiateOffer: false`), per the
  no-glare-on-join convention. Polite/impolite role = `string.CompareOrdinal` on the two connectionIds.
  Reconnect handling deliberately does **not** tear down existing RTCPeerConnections (media keeps
  flowing P2P through a signaling blip) — `RejoinCall`'s reply is diffed against already-known
  participants and only genuinely-new ones get wired up. Offer/answer payloads cross the hub as a
  `Dictionary<string,string>{"type","sdp"}`/plain JSON string (ICE), read back via `JsonElement` on
  the client so wire-format naming policy never has to be reasoned about. Registered `AddScoped`
  (`ICallService, CallService`) in `ServiceRegistrationExtensions.cs` — hub-only, no `AddHttpClient`.
  Not wired into any UI yet (WP6). Found and fixed one build issue along the way: `CallService.Media.cs`
  was missing `using Microsoft.AspNetCore.SignalR.Client;` — extension methods are file-scoped even
  within the same partial class, so without it `HubConnection.On`/`.InvokeAsync` calls silently fell
  back to unrelated overloads with confusing compiler errors. Full solution build + all 400 tests green.
- [x] **WP6 — UI + localization** — done (commit `9c3948a`). Feature is usable end-to-end.
  New `Shared\Components\Call\`: `CallOverlayHost` (the one stateful mount, added to
  `MainLayout`; gates on Admin-or-PrioritySupport exactly like `Chat.razor.cs`, then
  `EnsureConnectedAsync` + renders by `CallStage` — `IncomingCallToast` while Incoming,
  `ActiveCallView` while Outgoing/Connecting/InCall, plus a top error toast mapping known
  keys — `StartCallStatus` names, `CallDeclined`/`CallUnavailable`/`MediaPermissionDenied` —
  to localized `CallRes` text and passing anything else through as-is), `IncomingCallToast`
  (bottom-right card), `ActiveCallView` (1–6 tile CSS grid, local self-view, Ringing/
  Connecting status banner, hosts its own add-participant `CallUserPickerModal`),
  `ParticipantTile` (per-peer `<video>` bound in `OnAfterRenderAsync`, connecting spinner,
  mic/camera/sharing badges), `CallControls` (mic/camera/screen-share/add-participant/
  hang-up), `CallUserPickerModal` (multi-select, modeled on `UserPickerModal`; dual-purpose —
  CallOverlayHost hosts one in "start group call" mode opened via
  `CallService.GroupCallPickerRequested`, ActiveCallView hosts a separate one in "add to
  active call" mode — cap enforced via `ICallService.MaxParticipants`, new prop this WP).
  Modified: `MainLayout.razor` (`<CallOverlayHost />`), `_Imports.razor`, `MessagePane`
  (video-call button disabled outside `CallStage.Idle`; call-event messages render as a
  centered chip via `msg.CallEvent`/`CallDurationSeconds` instead of a bubble),
  `ConversationSidebar` ("new group call" button → `CallService.RequestGroupCallPicker`).
  `CallRes.resx`/`CallRes.el.resx` + hand-written `CallRes.Designer.cs` (~29 keys) registered
  in `ResetYourFuture.Shared.csproj` alongside `ChatRes`'s existing block. Two small WP5
  follow-ups needed by the UI: `ICallService.MaxParticipants` passthrough, and
  `CallService.Media.cs`'s `OnMediaError` now maps any `startLocalMedia` JS failure to the
  clean `MediaPermissionDenied` key instead of the raw (English-only) browser exception text.
  **Verified live** (`dotnet run`, logged in as the seeded admin): Chat page shows the new
  group-call icon next to "New"; clicking it opens the picker showing real callable users
  from `GetCallableUsersAsync` with a live "N of 5 selected" counter (5 = `MaxParticipants`−1,
  correct) and cap enforcement; selecting a user and clicking "Start call" drives
  `StartCallAsync` → `webrtcInterop.startLocalMedia` → (this headless environment has no
  camera, so) `getUserMedia` denial → `OnMediaError` → `ErrorOccurred("MediaPermissionDenied")`
  → `CallOverlayHost` → the correctly localized toast "Camera/microphone access was denied.
  Check your browser permissions and try again." — confirming the full JS→CallService→
  CallOverlayHost→CallRes pipeline end-to-end. Fixed one issue found live: the error toast's
  `top: 1rem` overlapped the sticky header; changed to `6.5rem` and reconfirmed via the
  compiled `ResetYourFuture.Web.styles.css`. Did not verify `ActiveCallView`/`ParticipantTile`/
  `CallControls` rendering live (needs an actual accepted call, blocked by no camera in this
  environment) or two-browser real-media flows — those need real hardware and are WP7's
  manual-verification matrix, not something to automate here. Full solution build + all 400
  tests green throughout (no test changes in this WP — it's pure UI).
- [ ] **WP7 — Tests + manual verification** — automated half done (commit `9c410d9`); manual
  half still open. `tests\ResetYourFuture.Web.Tests\CallHubTests.cs` (new): mirrors ChatHubTests
  — real `CallEventService`/`CallQueryService` over an InMemory db (not mocked) + a real
  `CallRegistry`, substituted SignalR plumbing and `UserManager`. Covers disabled-user connect
  abort, no-subscription → `NoAccess`, admin bypass, offline 1:1 callee → persisted Missed chat
  message broadcast to both `user_` groups, caller-busy rejection, accept → group join + Joined
  status + Started chat event, decline-before-connecting → `CallDeclined` broadcast + session
  ends as **Missed** (not "Declined" — a 1:1 decline never sets `HasConnected`, so it takes the
  same missed-call path as cancel/timeout per the edge-case notes above), signaling relay
  refusing a non-member target connection, media-state broadcast, 1:1 leave ending the session
  with persisted duration, and the 7th participant rejected at capacity. `ChatQueryServiceTests`
  gained the `CallEvent`/`CallDurationSeconds` projection coverage that was missing since WP2.
  `CallIntegrationTests.cs` (new): `/hubs/call/negotiate` → 401 without a token.
  `CallEventServiceTests`/`CallQueryServiceTests`/`CallRegistryTests` already covered the rest of
  this WP's list from WP2/WP3 — no changes needed there. Full solution build + all 414 tests
  green (400 + 14 new). **Still open**: the two-browser real-camera manual matrix from the
  Verification section below — needs actual camera/mic hardware across two real browser
  profiles, which isn't something this session's environment or tooling can exercise (confirmed
  during WP6: the local dev browser has no camera, so `getUserMedia` reliably fails, which is
  exactly what let WP6 verify the *error* path but not the live two-way media path).

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
