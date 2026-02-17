## Data load + table rendering

Trigger: Users API returns an empty list
Correct Behavior: Show explicit empty state (“No users found”) + keep search usable
False Behavior: Blank table area, broken layout, or crash

Trigger: Users API is slow
Correct Behavior: Loading state; disable row actions until rows are loaded
False Behavior: Actions clickable with undefined data; infinite spinner

Trigger: Users API returns 500/timeout
Correct Behavior: Error banner + retry; no partial stale rows
False Behavior: Silent failure, endless loading, or frozen UI

Trigger: User record has missing/null fields (email/name/status/roles/confirmed/enabled)
Correct Behavior: Render safe defaults (“—”, false); do not crash; log diagnostics
False Behavior: Runtime errors or misaligned columns

Trigger: Very long email/name/roles list
Correct Behavior: Truncate/ellipsis; tooltip; row height stays stable
False Behavior: Layout overflow or action buttons pushed off-screen

Trigger: Unicode/special characters in names (Greek)
Correct Behavior: Correct rendering; stable search behavior
False Behavior: Garbled text or encoding artifacts

Trigger: Duplicate user IDs or unstable row keys
Correct Behavior: Stable identity; no flicker; actions apply to correct user
False Behavior: React key warnings, row swapping, or actions hitting wrong row

---

## Search behavior (email/name input)

Trigger: Search box is empty
Correct Behavior: Shows default list (or prompts to search) consistently
False Behavior: Clears list unexpectedly or shows stale filtered results

Trigger: Leading/trailing spaces in query
Correct Behavior: Trim; equivalent results to unspaced query
False Behavior: No results due to whitespace mismatch

Trigger: Case differences (GSOtIRO@ vs gsotiro@)
Correct Behavior: Case-insensitive matching for email and name
False Behavior: Missed matches due to casing

Trigger: Partial match / substring search
Correct Behavior: Predictable behavior (contains vs starts-with) and consistent across fields
False Behavior: Random matches or inconsistent filtering between email and name

Trigger: Special characters in query (`+`, `.`, `_`, `@`)
Correct Behavior: Treated as literal characters; no regex injection
False Behavior: Regex-like behavior causing wrong results or crash

Trigger: Rapid typing (10+ keystrokes quickly)
Correct Behavior: Debounced requests; last query wins; no request storm
False Behavior: Flood of requests, out-of-order responses overriding newer results

Trigger: Search returns “no matches”
Correct Behavior: Empty state message (“No users match your search”)
False Behavior: Old results remain visible or table collapses without explanation

---

## Enabled/Disabled toggle (Disable action)

Trigger: Click Disable on an enabled user
Correct Behavior: Confirmation (if required); disables account; row updates to enabled=false
False Behavior: UI changes but backend didn’t; or wrong user disabled

Trigger: Click Disable on an already disabled user
Correct Behavior: Button hidden/disabled or changes to “Enable”; no-op is clear
False Behavior: Still clickable and returns confusing errors

Trigger: Disable current logged-in admin user
Correct Behavior: Blocked (cannot self-disable) or forces logout with explicit warning
False Behavior: Admin locks themselves out silently or session becomes inconsistent

Trigger: Disable a user with active session/token
Correct Behavior: Define policy and enforce (token revoked or session ends soon); UI messaging consistent
False Behavior: Disabled user remains fully authenticated indefinitely

Trigger: Disable fails (409/500/timeout)
Correct Behavior: Roll back optimistic UI; show error; allow retry
False Behavior: UI shows disabled but backend is enabled (or vice versa)

---

## Reset Password (sensitive)

Trigger: Click Reset Password
Correct Behavior: Confirmation; triggers correct backend flow; success message indicates what happened (email sent or temp password shown)
False Behavior: Silent success with no actual reset; or wrong account reset

Trigger: Reset password for unconfirmed user
Correct Behavior: Allowed/blocked per policy; clear explanation
False Behavior: Reset appears to work but user cannot log in due to confirmation state

Trigger: Reset password for disabled user
Correct Behavior: Policy-consistent (allowed but still disabled, or blocked); explicit message
False Behavior: Reset implicitly re-enables user or creates conflicting state

Trigger: Reset clicked repeatedly / double-click
Correct Behavior: Idempotent behavior or throttling; button disabled in-flight
False Behavior: Multiple reset emails/temp passwords generated unexpectedly

Trigger: Reset fails (429/500/timeout)
Correct Behavior: Error shown; no misleading “success”; retry possible
False Behavior: Success toast despite failure or stuck loading state

Trigger: Reset response includes sensitive secrets (temp password)
Correct Behavior: Render once; avoid logging; mask by default; copy button optional
False Behavior: Password displayed in logs/console or persisted in UI after navigation

---

## Delete user (high risk)

Trigger: Click Delete
Correct Behavior: Strong confirmation (type email / confirm); warns about irreversibility and data impact
False Behavior: One-click delete or weak confirmation leading to accidental deletion

Trigger: Delete user with existing enrollments/submissions/activity
Correct Behavior: Enforce policy (soft delete/anonymize/archive); preserve referential integrity; explain outcome
False Behavior: Hard delete breaks foreign keys, analytics, submissions history, or course progress

Trigger: Delete an admin account
Correct Behavior: Block or require elevated confirmation; ensure at least one admin remains
False Behavior: Last admin can be deleted; system becomes unmanaged

Trigger: Delete currently logged-in admin
Correct Behavior: Block or force immediate logout after confirmed delete
False Behavior: Session continues in a deleted account state

Trigger: Delete races with Disable/Reset Password in another tab
Correct Behavior: Deterministic outcome; other action gets 404/409; UI refreshes
False Behavior: Partial state (deleted but still visible/operable)

Trigger: Delete fails (409 due to dependencies / 500/timeout)
Correct Behavior: Clear error; row remains; no UI desync; retry possible
False Behavior: Row disappears but user still exists; or stuck UI

---

## Roles + authorization correctness

Trigger: Roles field is empty/null or has unknown role
Correct Behavior: Render “—” or “Unknown”; safest action gating
False Behavior: Crash or incorrect assumption of privileges

Trigger: User has multiple roles (Student + Admin, etc.)
Correct Behavior: Display consistently; actions respect highest privilege and policy
False Behavior: UI displays one role but backend enforces another

Trigger: Admin-only actions visible to non-admin viewers
Correct Behavior: Hidden/disabled + server rejects if attempted
False Behavior: Privilege escalation via UI exposure

Trigger: Attempt to disable/delete higher-privilege account (e.g., superadmin)
Correct Behavior: Block or require elevated flow; audit log entry
False Behavior: Any admin can remove superadmin without trace

---

## Confirmed state handling

Trigger: Unconfirmed user row
Correct Behavior: Confirmed=false displayed clearly; Reset/Disable/Delete follow policy
False Behavior: Confirmed indicator wrong or actions behave inconsistently

Trigger: Confirmation state changes externally (user confirms email)
Correct Behavior: Refresh updates confirmed flag accurately
False Behavior: Stale confirmed status persists indefinitely

---

## Concurrency + paging (if applicable)

Trigger: Two admins act on the same user simultaneously
Correct Behavior: Conflict handled (409) or last-write messaging; UI refreshes to truth
False Behavior: UI shows success but final state differs from server

Trigger: Large user base (1000+)
Correct Behavior: Pagination/virtualization; search uses server-side querying; responsive UI
False Behavior: Fetch-all slows page, search becomes unusable, browser jank

---

## Security + privacy + audit

Trigger: Email/name contains HTML/script payload
Correct Behavior: Render as text; no execution (XSS-safe)
False Behavior: Stored XSS in table or toasts

Trigger: Unauthorized actor tries endpoints directly (disable/reset/delete)
Correct Behavior: Server returns 403; UI shows unauthorized; nothing changes
False Behavior: Actions succeed without proper authorization

Trigger: Sensitive actions performed (reset/delete/disable)
Correct Behavior: Audit log created (who/when/target/action) and visible to admins (if you have logs)
False Behavior: No audit trail for high-impact actions

---

## UX/accessibility

Trigger: Keyboard-only navigation across search + action buttons
Correct Behavior: Focus order sane; visible focus; Enter/Space works; confirmation modal traps focus
False Behavior: Actions unreachable, focus lost, or modal not keyboard-dismissable

Trigger: Small viewport / responsive
Correct Behavior: Table scrolls or stacks; action buttons remain reachable
False Behavior: Buttons overlap, disappear, or require horizontal scrolling for basic actions
