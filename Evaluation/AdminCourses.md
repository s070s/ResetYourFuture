admin/courses

## Data load + table rendering

Trigger: Courses API returns an empty list
Correct Behavior: Show an explicit empty state (“No courses yet”) and keep “Create New Course” usable
False Behavior: Blank page, broken table, or console crash

Trigger: Courses API is slow
Correct Behavior: Show loading indicator/skeleton; disable row actions until data is ready
False Behavior: Frozen UI, actions clickable with undefined row data, or infinite spinner

Trigger: Courses API returns 500/timeout
Correct Behavior: Show a visible error + retry; keep navigation working
False Behavior: Silent failure, endless loading, or page crash

Trigger: Course item has missing/null fields (title/status/enrollments)
Correct Behavior: Render safe fallbacks (e.g., “—”, 0); log diagnostics; keep actions gated
False Behavior: Runtime exceptions or misaligned columns

Trigger: Very long course title
Correct Behavior: Truncate with ellipsis; stable row height; optionally tooltip/full view
False Behavior: Layout overflow, row height explosion, or actions pushed off-screen

Trigger: Unicode/special chars in title (Greek/accents)
Correct Behavior: Correct rendering and consistent sorting/search behavior (if present)
False Behavior: Garbled text or encoding artifacts

Trigger: Duplicate IDs / unstable row keys from backend
Correct Behavior: Stable row identity; no flicker; no action mis-targeting
False Behavior: React key warnings, row swapping, or clicking affects wrong course

---

## Create New Course

Trigger: Clicking “Create New Course” repeatedly / double submit
Correct Behavior: Single draft created; submit disabled while in-flight
False Behavior: Duplicate courses or multiple navigations with inconsistent drafts

Trigger: Create with missing required fields (title/slug/etc.)
Correct Behavior: Field-level validation errors; server enforces too
False Behavior: Invalid course created or generic error with no guidance

Trigger: Create succeeds but list doesn’t refresh
Correct Behavior: Invalidate cache/re-fetch; new course row appears immediately
False Behavior: User must hard refresh to see the new course

Trigger: Create returns unexpected response shape
Correct Behavior: UI uses server canonical data; handles extra/missing fields safely
False Behavior: Crash or partially rendered row

---

## Status + action availability (critical)

Trigger: Row status is Published but “Publish” button is still shown/enabled
Correct Behavior: “Publish” hidden/disabled (or replaced by “Unpublish/Archive” per policy)
False Behavior: User can re-publish a published course or sees contradictory UI

Trigger: Row status is Draft/Unpublished
Correct Behavior: Publish enabled; status badge reflects Draft/Unpublished accurately
False Behavior: Publish disabled incorrectly or status badge stuck on Published

Trigger: Backend returns unknown status (Archived/Disabled)
Correct Behavior: Generic badge; safest action gating; no crash
False Behavior: UI breaks or enables dangerous actions by default

Trigger: Status changes after publish/unpublish
Correct Behavior: Badge/buttons update to match backend after mutation + refresh
False Behavior: Stale status shown; buttons remain wrong

---

## Publish behavior

Trigger: Publish a course that’s structurally incomplete (no modules/lessons/content)
Correct Behavior: Publish blocked with explicit missing requirements (if that’s your rule)
False Behavior: Course becomes Published but is unusable for learners

Trigger: Publish clicked twice quickly
Correct Behavior: Button disabled in-flight; endpoint idempotent; single transition
False Behavior: Duplicate requests cause inconsistent UI or repeated toasts

Trigger: Another admin publishes same draft simultaneously
Correct Behavior: One succeeds; the other gets 409/conflict; UI refreshes to Published
False Behavior: UI stays Draft and allows endless publish attempts

Trigger: Publish fails (network/server error)
Correct Behavior: Roll back optimistic UI; show error; allow retry
False Behavior: Toast says success but status remains unchanged (or vice versa)

Trigger: Success toast “Course published” while row still shows Draft
Correct Behavior: Toast only on confirmed success; row updates immediately
False Behavior: Misleading toast or stale list state

---

## Edit flow

Trigger: Click Edit on a row
Correct Behavior: Correct course loads by ID; fields populated reliably
False Behavior: Wrong course opens due to index-based selection or stale state

Trigger: Unsaved changes then navigate away
Correct Behavior: Confirm/guard; allow discard or stay
False Behavior: Silent loss of edits

Trigger: Save returns validation errors
Correct Behavior: Field-level errors mapped correctly; user input preserved
False Behavior: Generic error only or cleared form

Trigger: Course edited in two tabs/admins
Correct Behavior: Concurrency handling (ETag/rowversion) or “modified elsewhere” message
False Behavior: Silent overwrite (last write wins) with no warning

---

## Enrollments count

Trigger: Enrollments count is null/undefined
Correct Behavior: Show 0 or “—” consistently; no NaN
False Behavior: “NaN”, blank cell, or crash

Trigger: Enrollments count is large (10k+)
Correct Behavior: Renders instantly; no expensive recalculation per render
False Behavior: UI lag, jank, or slow scrolling

Trigger: Enrollment count changes after enroll/unenroll events
Correct Behavior: Count updates on refresh/polling/websocket (as designed)
False Behavior: Count stays stale indefinitely

Trigger: Published course with enrollments > 0 is edited
Correct Behavior: Enforce your policy (allowed edits vs locked fields); server enforces too
False Behavior: Edits break enrolled users’ experience or invalidate progress

---

## Delete behavior (high risk)

Trigger: Click Delete
Correct Behavior: Confirmation modal; cancel does nothing
False Behavior: Immediate deletion without confirmation

Trigger: Delete a course with enrollments > 0
Correct Behavior: Enforce policy (block/soft-delete/archive); explain impact clearly
False Behavior: Hard delete succeeds and breaks learners’ access/progress

Trigger: Delete a draft with 0 enrollments
Correct Behavior: Deletes cleanly; row disappears; list refreshes
False Behavior: Ghost row remains or requires manual refresh

Trigger: Delete races with publish/edit in another tab
Correct Behavior: Deterministic outcome; other action gets 404/409 + refresh
False Behavior: Partial state (deleted but still appears Published)

Trigger: Delete request times out then user retries
Correct Behavior: Idempotent behavior; final state resolves after refresh
False Behavior: Duplicate delete errors or stuck disabled UI

---

## Authorization + routing

Trigger: Non-admin hits /admin/courses directly
Correct Behavior: 403/redirect; no data rendered
False Behavior: Data leaks or actions available client-side

Trigger: Session expires mid-action
Correct Behavior: Auth error shown; route to login; no fake “success” toast
False Behavior: UI claims success while server rejected

Trigger: IDOR attempt (edit/delete course not owned by org/tenant)
Correct Behavior: Server returns 403/404; UI shows unauthorized
False Behavior: Cross-tenant access succeeds

---

## i18n (EN/EL toggle)

Trigger: Toggle EN/EL
Correct Behavior: All visible strings change (headers/buttons/toasts/empty states) consistently
False Behavior: Mixed languages or missing labels

Trigger: Missing translation keys
Correct Behavior: Predictable fallback (e.g., EN) and no blank UI text
False Behavior: Empty buttons/headers or broken layout

Trigger: Language preference persistence
Correct Behavior: Selection persists across refresh (if intended)
False Behavior: Resets unexpectedly on reload/navigation

---

## Resilience + security + UX

Trigger: Offline during publish/delete/edit
Correct Behavior: Clear error; actions re-enabled; retry works
False Behavior: Permanent disabled buttons or phantom success

Trigger: Title contains HTML/script payload
Correct Behavior: Render as plain text; no execution (XSS-safe)
False Behavior: Script executes in table/toast/modal

Trigger: Keyboard-only navigation
Correct Behavior: Focus reaches Edit/Publish/Delete; visible focus; modal traps focus; ESC closes
False Behavior: Unreachable actions, invisible focus, or focus escapes behind modal


