admin/assessments

## Data load + table rendering

Trigger: API returns an empty list
Correct Behavior: Show an explicit empty state (“No assessments yet”) and keep page usable
False Behavior: Blank page, broken table layout, or console crash

Trigger: API response is slow
Correct Behavior: Show loading indicator/skeleton; disable row actions until data is ready
False Behavior: Frozen UI, actions clickable while data is undefined, or infinite spinner after completion

Trigger: API returns 500/timeout
Correct Behavior: Show an error message with a retry path; keep navigation intact
False Behavior: Silent failure, endless loading, or page crash

Trigger: API returns malformed/partial objects (missing status/submissions/title/key)
Correct Behavior: Render safe fallbacks; log diagnostics; keep row actions gated
False Behavior: Runtime exception, broken row rendering, or missing columns

Trigger: Very long title/key strings
Correct Behavior: Truncate with ellipsis; preserve row height; tooltip/full view on hover
False Behavior: Layout overflow, table stretching, or actions pushed off-screen

Trigger: Unicode/special characters in title (Greek, accents, emoji)
Correct Behavior: Renders correctly; sorting/search (if present) behaves predictably
False Behavior: Garbled text, encoding artifacts, or incorrect truncation

Trigger: Duplicate IDs/React keys returned from backend
Correct Behavior: Stable row identity; no flicker; de-dup or handle collisions deterministically
False Behavior: React key warnings, row swapping, or action applied to wrong row

Trigger: Backend returns unstable ordering between refreshes
Correct Behavior: Stable sort (or explicit “latest first” rule) to avoid flicker
False Behavior: Rows reorder unpredictably after any mutation or refresh

---

## Keys, identifiers, and uniqueness

Trigger: Create/edit uses a key that already exists
Correct Behavior: Server returns 409/validation; UI shows clear “Key already in use” error
False Behavior: Duplicate assessments with same key or ambiguous routing to wrong assessment

Trigger: Key differs only by casing (career_v1 vs Career_v1)
Correct Behavior: Enforce a single rule (case-insensitive or case-sensitive) consistently in UI + API
False Behavior: One view treats them distinct while another treats them equal, causing conflicts later

Trigger: Key contains spaces/invalid characters
Correct Behavior: Validation blocks with allowed-pattern message; server enforces too
False Behavior: Key is accepted but breaks routing, querying, or future edits

Trigger: Key/title exceeds max length
Correct Behavior: Validation prevents submit; server rejects with clear message if bypassed
False Behavior: Truncation on server leading to collisions or unusable identifiers

---

## Status + action availability

Trigger: Draft assessment row
Correct Behavior: Publish enabled; Submissions allowed (if relevant); Edit/Delete follow policy
False Behavior: Publish missing/disabled incorrectly, or Submissions shown as active when it shouldn’t be

Trigger: Published assessment row
Correct Behavior: Publish disabled/hidden; Edit/Delete behavior matches policy (lock schema if required)
False Behavior: Publish still available, or edits allowed that silently mutate live submissions

Trigger: Unknown/new status from backend (Archived/Disabled)
Correct Behavior: Render a generic badge; restrict actions safely; prompt refresh/handling
False Behavior: UI crashes or enables dangerous actions by default

Trigger: Status changes after action (publish/delete)
Correct Behavior: Badge and buttons update immediately and match backend truth after refresh
False Behavior: Stale status displayed, buttons remain incorrect, or optimistic UI never reconciles

---

## Create New Assessment

Trigger: User double-clicks “Create New Assessment” / submits twice
Correct Behavior: Single creation; UI disables submit and shows in-progress state
False Behavior: Duplicate assessments created or multiple navigations to inconsistent drafts

Trigger: Creating without required fields (title/key/etc.)
Correct Behavior: Field-level validation errors with actionable guidance
False Behavior: Submit succeeds with invalid record or fails with vague generic error

Trigger: Create succeeds but list view remains stale
Correct Behavior: Invalidate cache/re-fetch; new row appears with correct status
False Behavior: User must hard refresh to see the new assessment

Trigger: Create returns unexpected response shape
Correct Behavior: UI uses server canonical data; handles additional/missing fields safely
False Behavior: Crash or incorrect row rendering due to strict assumptions

---

## Edit flow

Trigger: Click Edit on a specific row
Correct Behavior: Loads correct assessment by ID; fields populated reliably
False Behavior: Loads wrong assessment due to index-based navigation or stale selection

Trigger: Unsaved changes then navigate away
Correct Behavior: Warning/confirm; option to stay or discard changes
False Behavior: Silent loss of edits

Trigger: Save with server validation errors
Correct Behavior: Map errors to specific fields; keep user input intact
False Behavior: Clears form, shows generic toast only, or loses user changes

Trigger: Two tabs editing same assessment
Correct Behavior: Concurrency handling (ETag/rowversion) or clear “modified elsewhere” message
False Behavior: Last write silently overwrites without warning

Trigger: Edit policy for Published assessments (if restricted)
Correct Behavior: UI blocks or limits edits; server enforces; clear explanation
False Behavior: UI allows changes that break reporting or invalidate existing submissions

---

## Publish behavior

Trigger: Publish draft with missing required structure (no questions/scoring)
Correct Behavior: Publish blocked with explicit missing-items list
False Behavior: Published but unusable assessment or runtime errors for end users

Trigger: Publish clicked twice quickly
Correct Behavior: Idempotent call; button disabled during request; single state transition
False Behavior: Double requests cause inconsistent status or duplicate side effects

Trigger: Another admin publishes first (race)
Correct Behavior: Current admin receives 409/conflict; UI refreshes to Published state
False Behavior: UI stays Draft and allows further publish attempts indefinitely

Trigger: Publish fails (network/server)
Correct Behavior: Roll back optimistic UI; show error; allow retry
False Behavior: UI shows Published while server is Draft (or vice versa)

Trigger: Language completeness rule (EN/EL) unmet
Correct Behavior: Enforce defined rule (block or warn) consistently; server matches
False Behavior: Some users see missing strings or broken pages post-publish

---

## Submissions count + Submissions page

Trigger: Submissions count is 0
Correct Behavior: Submissions page shows empty state; no errors
False Behavior: Page errors or implies data exists

Trigger: Count > 0 but submissions endpoint returns empty
Correct Behavior: Detect inconsistency; show “Data mismatch” warning; prompt refresh/log
False Behavior: Silent empty list implying all submissions were deleted

Trigger: Very large submissions volume
Correct Behavior: Pagination/virtualization; page remains responsive
False Behavior: UI freezes or times out trying to render everything

Trigger: Rapidly clicking “Submissions”
Correct Behavior: Single navigation/request; debounced clicks
False Behavior: Multiple requests, duplicated navigation, or stuck loading state

Trigger: Non-admin attempts to access submissions route directly
Correct Behavior: 403/redirect to login or unauthorized page
False Behavior: Data leaks or partial access granted

---

## Delete behavior

Trigger: Click Delete
Correct Behavior: Confirmation modal; cancel does nothing
False Behavior: Immediate delete without confirmation or accidental deletion

Trigger: Delete draft with 0 submissions
Correct Behavior: Deleted (hard or soft per policy); row disappears after refresh
False Behavior: Row remains but is broken, or delete succeeds but list doesn’t update

Trigger: Delete assessment with submissions (policy-sensitive)
Correct Behavior: Enforce policy (block/soft-delete/archive); clear message explaining why
False Behavior: Deletes anyway and breaks reporting/audit trail

Trigger: Delete races with another action (edit/publish)
Correct Behavior: One outcome wins deterministically; the other receives 404/409 with refresh
False Behavior: Partial state (deleted but still “published” in UI) or ghost rows

Trigger: Delete request times out then user retries
Correct Behavior: Idempotent behavior; clear final state after refresh
False Behavior: Double-delete errors, stuck buttons, or inconsistent list state

---

## Authorization + routing hardening

Trigger: Non-admin visits /admin/assessments
Correct Behavior: Redirect/403 with no table data loaded
False Behavior: UI renders data or actions become available client-side

Trigger: Session expires mid-action
Correct Behavior: Shows auth error; routes to login; action is not applied client-side
False Behavior: UI claims success while server rejected, or infinite redirect loop

Trigger: Attempt IDOR (edit/delete another org’s assessment by ID)
Correct Behavior: Server returns 403/404; UI shows unauthorized
False Behavior: Cross-tenant access succeeds

---

## Network resilience

Trigger: Offline during publish/delete/edit
Correct Behavior: Clear error state; buttons re-enable; retry works
False Behavior: Permanent disabled UI, phantom success, or data corruption in client cache

Trigger: Server returns 429 rate limit
Correct Behavior: Inform user; backoff and prevent spamming; retry later
False Behavior: Continuous rapid retries or unclear failures

---

## Security + sanitization

Trigger: Title/key contains HTML/script payload
Correct Behavior: Render as plain text; no execution; stored safely
False Behavior: XSS in table row or toast/error rendering

Trigger: Server error message includes unsafe content
Correct Behavior: Escape output; show generic message; log raw server response safely
False Behavior: Injected markup executed in UI

---

## Accessibility + UX

Trigger: Keyboard-only navigation across actions
Correct Behavior: Focus reaches all buttons; visible focus; Enter/Space activates
False Behavior: Focus trapped, no focus ring, or unreachable actions

Trigger: Confirmation dialog opened
Correct Behavior: Focus trap inside modal; ESC closes; returns focus to trigger
False Behavior: Focus goes behind modal or modal cannot be dismissed reliably

Trigger: Small viewport width
Correct Behavior: Table scrolls horizontally or adapts; actions remain reachable
False Behavior: Actions overlap, disappear, or cause layout collapse
