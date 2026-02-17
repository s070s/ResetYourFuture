## Profile load + initialization

Trigger: Open profile page with slow profile API
Correct Behavior: Loading state; disable Save/Change Password until data loaded
False Behavior: Empty fields with enabled actions; saving sends nulls; infinite spinner

Trigger: Profile API returns 401/403 (session expired / unauthorized)
Correct Behavior: Redirect to login; show “session expired” message
False Behavior: Page partially renders and actions fail silently

Trigger: Profile API returns partial/malformed data (missing email/name/DOB/avatar)
Correct Behavior: Safe defaults (“Not specified”); editable fields still work
False Behavior: Crash, “undefined”, or broken form bindings

Trigger: Open profile with cached stale data
Correct Behavior: Revalidate and update UI; prevent overwriting newer server state
False Behavior: User saves and unknowingly overwrites newer profile values

---

## Basic info edit (Display Name, etc.)

Trigger: Display Name empty / whitespace-only
Correct Behavior: Validation blocks or normalizes (trim) with clear message
False Behavior: Saves blank name, breaks UI elsewhere, or silently fails

Trigger: Display Name very long / contains emojis / Greek / special chars
Correct Behavior: Enforce max length; render correctly; round-trip safe
False Behavior: Truncation without warning, encoding issues, or layout overflow

Trigger: Rapidly clicking “Save Profile”
Correct Behavior: Disable button in-flight; single request; idempotent save
False Behavior: Multiple requests, race conditions, or inconsistent final state

Trigger: Save Profile fails (500/timeout)
Correct Behavior: Keep user input; show error; allow retry
False Behavior: Clears form, claims success, or leaves UI stuck disabled

Trigger: Save Profile succeeds but server normalizes value (trim/case rules)
Correct Behavior: UI updates to canonical server value after save
False Behavior: UI shows old value until refresh; user thinks it didn’t save

---

## Avatar upload (file input)

Trigger: Upload valid image (png/jpg/webp)
Correct Behavior: Shows preview (optional), upload progress, updates avatar everywhere after save
False Behavior: No feedback; avatar updates only after hard refresh; broken image URL

Trigger: Upload unsupported file type (pdf/exe/svg if disallowed)
Correct Behavior: Block with explicit error; do not upload
False Behavior: Upload attempts anyway or fails with generic server error

Trigger: Upload very large file
Correct Behavior: Client-side size check + server enforcement; clear limit message
False Behavior: Browser hangs, request times out, or server accepts and slows the app

Trigger: Upload image with extreme dimensions
Correct Behavior: Resize/crop strategy defined (center-crop/contain); consistent thumbnailing
False Behavior: Stretched avatar, layout shift, or massive payload stored

Trigger: Upload filename with special chars / long path / non-latin
Correct Behavior: Upload succeeds; server stores safely; URL encoding correct
False Behavior: Upload fails due to encoding/path issues

Trigger: Cancel file chooser or re-select same file
Correct Behavior: No unintended upload; re-select triggers change reliably
False Behavior: Stale file state prevents re-upload or triggers accidental save

Trigger: Upload then navigate away without saving (if save is separate)
Correct Behavior: Either auto-save avatar or warn about unsaved change; no orphan uploads
False Behavior: Orphaned files stored or avatar appears changed but isn’t persisted

Trigger: Avatar deletion/reset (if supported)
Correct Behavior: Reverts to default placeholder; cached images invalidated
False Behavior: Broken image link or old avatar persists due to caching

---

## Read-only fields (Email/Name/DOB display)

Trigger: Email field is displayed as read-only
Correct Behavior: Cannot be edited; clearly communicated; copied safely
False Behavior: Appears editable but changes are ignored or cause errors

Trigger: Date of Birth is “Not specified”
Correct Behavior: Clear placeholder; if editable, input validation and format localized
False Behavior: Invalid date accepted or locale format mismatch causes wrong DOB

Trigger: Timezone/locale impacts date display
Correct Behavior: DOB shown as date-only (no timezone drift)
False Behavior: Off-by-one-day due to timezone conversion

---

## Change Password flow

Trigger: Current password missing
Correct Behavior: Block submit; field-level error
False Behavior: Request sent; server error not mapped; confusing UX

Trigger: New password too weak (policy: length/complexity/breached list)
Correct Behavior: Clear requirements; block submit; server enforces identically
False Behavior: UI allows but server rejects with vague message (or worse, accepts weak)

Trigger: New password equals current password
Correct Behavior: Block with explicit message
False Behavior: Allows change but provides no security improvement

Trigger: New password and confirmation mismatch
Correct Behavior: Inline error; submit disabled until match
False Behavior: Submit allowed; server rejects; user confusion

Trigger: Change Password clicked twice quickly
Correct Behavior: Disable button in-flight; single request; clear result
False Behavior: Multiple requests; one succeeds and others fail unpredictably

Trigger: Password change succeeds
Correct Behavior: Show success; optionally require re-login; invalidate other sessions per policy
False Behavior: Claims success but old password still works or sessions not handled as intended

Trigger: Password change fails (401 wrong current password / 429 rate limit / 500)
Correct Behavior: Specific error for wrong current password; rate-limit message; retry possible
False Behavior: Generic failure; clears fields unnecessarily; locks UI

Trigger: Password fields autofill behavior
Correct Behavior: Correct `autocomplete` attributes (`current-password`, `new-password`)
False Behavior: Browser fills wrong fields or leaks password into display name inputs

---

## Logout

Trigger: Click Logout
Correct Behavior: Tokens/cookies cleared; redirect to public/login; back button doesn’t restore session
False Behavior: Appears logged out but API still works; back navigation shows authenticated pages

Trigger: Logout during in-flight save/password change
Correct Behavior: Either block logout until completion or cancel safely with clear messaging
False Behavior: Partial updates, ambiguous final state, or UI stuck

---

## Localization + messages (profile-specific)

Trigger: Toggle EN/EL while on profile page
Correct Behavior: Labels, placeholders, validation messages, toasts all switch; values unchanged
False Behavior: Mixed language labels or error messages remain in prior locale

Trigger: Server validation errors returned in English only
Correct Behavior: Client maps error codes to localized text
False Behavior: English-only errors in EL mode

---

## Security + privacy

Trigger: Avatar upload contains polyglot/embedded payload (e.g., malicious SVG)
Correct Behavior: Disallow or sanitize; serve with safe content-type; no script execution
False Behavior: XSS via avatar rendering

Trigger: Profile page caches sensitive responses
Correct Behavior: Appropriate cache headers; no sensitive data in logs
False Behavior: Sensitive info stored in client logs/localStorage unintentionally

Trigger: Unauthorized user tries to update another user’s profile by ID
Correct Behavior: Server blocks (403); client shows unauthorized
False Behavior: IDOR vulnerability

---

## Accessibility + UX

Trigger: Keyboard-only navigation through inputs/buttons
Correct Behavior: Logical tab order; visible focus; errors announced; modal dialogs (if any) trap focus
False Behavior: Focus jumps unpredictably or buttons not reachable

Trigger: Small viewport / mobile
Correct Behavior: Cards stack; buttons remain reachable; file input usable
False Behavior: Horizontal scrolling required; actions clipped
