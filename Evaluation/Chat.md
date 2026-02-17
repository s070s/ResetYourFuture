## Conversations list (left panel)

Trigger: No conversations exist
Correct Behavior: Empty state (“No conversations yet”) + New button works
False Behavior: Blank panel with no guidance or disabled New

Trigger: Conversations API is slow
Correct Behavior: Loading indicator; list placeholders; selecting disabled until loaded
False Behavior: Clicking selects “null” conversation or crashes

Trigger: Conversations API fails (500/timeout)
Correct Behavior: Error + retry; keep compose disabled to avoid orphan state
False Behavior: Silent failure or infinite spinner

Trigger: Very long participant names / last message preview
Correct Behavior: Truncate/ellipsis; stable row height; tooltip optional
False Behavior: Layout overflow or unreadable list

Trigger: Same user appears in multiple conversations (edge modeling)
Correct Behavior: Deterministic labeling (e.g., topic/ID); no duplicates unless intended
False Behavior: Duplicate threads with identical labels and user confusion

Trigger: Sorting by most recent activity
Correct Behavior: Updates when new messages arrive; stable order
False Behavior: Out-of-order threads or stale sorting after sending/receiving

Trigger: Unread counts (if supported)
Correct Behavior: Accurate per thread; clears on open; persists across refresh
False Behavior: Unread badge stuck or clears without opening

---

## New conversation creation

Trigger: Click “New”
Correct Behavior: Opens user picker/compose flow; prevents duplicates if policy says “one thread per pair”
False Behavior: Creates empty conversation with no recipient or duplicates on repeated clicks

Trigger: New clicked repeatedly / double click
Correct Behavior: Single create flow; button disabled in-flight
False Behavior: Multiple empty threads created

Trigger: Create conversation fails (403/500/timeout)
Correct Behavior: Show error; no orphan thread in list
False Behavior: Thread appears but can’t be opened/sent to

---

## Thread load + message history

Trigger: Open a conversation with many messages
Correct Behavior: Paginated/infinite scroll; loads recent first; smooth scrolling
False Behavior: Fetch-all freezes UI or crashes tab

Trigger: Open conversation while messages API is slow
Correct Behavior: Loading state in message pane; input disabled until thread ready (or safe to type draft)
False Behavior: Sending goes to wrong thread or disappears

Trigger: Messages API fails (500/timeout)
Correct Behavior: Error + retry; keep existing cached messages visible if safe
False Behavior: Blank pane with no retry or loses already loaded history

Trigger: Message ordering with identical timestamps
Correct Behavior: Stable order using secondary key (server sequence/ID)
False Behavior: Messages reorder on refresh

Trigger: Deleted/removed messages (if supported)
Correct Behavior: Placeholder (“Message removed”) and preserves timeline
False Behavior: Timeline collapses causing confusing jumps

---

## Sending messages

Trigger: Send a normal text message
Correct Behavior: Message appears once; delivered status eventually confirmed
False Behavior: Duplicate send, missing message, or message shown in wrong thread

Trigger: Send empty/whitespace-only message
Correct Behavior: Block send; no request fired
False Behavior: Empty bubbles appear or server stores blank messages

Trigger: Rapidly press Enter / click send multiple times
Correct Behavior: Debounce; idempotency key; one message per intent
False Behavior: Duplicate messages

Trigger: Slow network while sending
Correct Behavior: “Sending…” state; input remains usable; user can’t accidentally resend same payload
False Behavior: UI stuck; user spams send; duplicates created

Trigger: Send fails (timeout/500)
Correct Behavior: Message marked failed with retry; draft preserved
False Behavior: Message disappears or shown as sent when it failed

Trigger: User navigates to another thread mid-send
Correct Behavior: Send completes in original thread; UI does not mis-attach message
False Behavior: Message ends up in the newly opened conversation

Trigger: Server rejects message (403 blocked user / conversation closed)
Correct Behavior: Clear error; no optimistic “sent”; input disabled if needed
False Behavior: UI shows sent but recipient never gets it

---

## Input UX (composer)

Trigger: Enter key behavior
Correct Behavior: Enter sends (or Shift+Enter newline) consistently; documented via placeholder/help
False Behavior: Unpredictable send/newline behavior or accidental sends

Trigger: Pasting large text
Correct Behavior: Handles large payload within limits; shows error if over max length
False Behavior: UI freezes or silently truncates

Trigger: Emoji / Greek / RTL characters
Correct Behavior: Correct rendering; cursor behaves correctly; preserved on send
False Behavior: Garbled characters or cursor jumps

Trigger: Draft persistence per conversation
Correct Behavior: Draft saved per thread (optional) and restored when switching
False Behavior: Draft lost on thread switch or overwritten by other thread draft

Trigger: Autoscroll when new message arrives
Correct Behavior: If user is at bottom, autoscroll; if user is reading older messages, do not yank scroll
False Behavior: Scroll jumps unexpectedly while reading history

---

## Receiving messages (real-time)

Trigger: New incoming message in currently open conversation
Correct Behavior: Message appears in real-time; unread count not incremented for active thread
False Behavior: Message doesn’t appear until refresh or appears twice

Trigger: New incoming message in a different conversation
Correct Behavior: Conversation moves to top; unread badge increments; notification policy applied
False Behavior: No badge; thread order doesn’t update; message lost until refresh

Trigger: Connection drop (websocket/sse)
Correct Behavior: Reconnect with backoff; fetch missed messages; show “Reconnecting…”
False Behavior: Silent disconnect; missing messages permanently

Trigger: Out-of-order delivery (network jitter)
Correct Behavior: Client reorders using server sequence; no duplicates
False Behavior: Messages appear in wrong order

---

## Read receipts / delivery status (if supported)

Trigger: Message delivered but not read
Correct Behavior: “Delivered” state shown (or nothing if not supported)
False Behavior: Shown as read immediately

Trigger: Read receipt update arrives
Correct Behavior: Updates state for correct message/thread only
False Behavior: Marks wrong message as read due to ID mismatch

---

## Attachments (if supported now or later)

Trigger: Upload attachment
Correct Behavior: Progress shown; size/type limits enforced; safe preview/download
False Behavior: Upload stuck, broken links, or unsafe file types allowed

Trigger: Attachment upload fails
Correct Behavior: Clear error + retry; message not sent with missing file silently
False Behavior: Message sent referencing missing attachment

---

## Authorization + privacy

Trigger: User tries to open a conversation they don’t belong to (ID in URL)
Correct Behavior: 403/404; no message leakage
False Behavior: Messages visible (IDOR)

Trigger: Admin vs student role boundaries (if applicable)
Correct Behavior: Only permitted cross-role conversations; clear labeling
False Behavior: Unauthorized messaging possible

Trigger: PII in conversation list preview
Correct Behavior: Only intended preview shown; respects privacy policy
False Behavior: Sensitive content leaked in previews/notifications unexpectedly

---

## Localization + formatting

Trigger: Toggle EN/EL on chat page
Correct Behavior: UI labels/placeholders/system messages localized; timestamps formatted per locale
False Behavior: Mixed language UI or timestamps remain in old locale

Trigger: Timestamp formatting and timezone
Correct Behavior: Uses user locale/timezone; consistent across list + message bubbles
False Behavior: Different timezone per panel or inconsistent formats

---

## Performance + stability

Trigger: Very large conversation list (1000 threads)
Correct Behavior: Pagination/virtualization; fast search/filter if present
False Behavior: Page becomes slow to render/scroll

Trigger: Very long message thread (10k messages)
Correct Behavior: Windowed rendering; stable memory; quick jump-to-latest
False Behavior: Browser memory blow-up or input lag

---

## Accessibility

Trigger: Keyboard navigation
Correct Behavior: Tab order: conversations -> messages -> composer -> send; Enter activates buttons; focus visible
False Behavior: Focus trapped, invisible focus, or send button unreachable

Trigger: Screen reader
Correct Behavior: New messages announced (polite); message list has proper roles/labels
False Behavior: No announcement or confusing reading order
