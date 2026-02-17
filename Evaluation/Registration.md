## Page load + initialization

Trigger: Registration page loads with slow config/API (captcha, password policy, i18n strings)
Correct Behavior: Form renders deterministically; submit disabled until required config is ready
False Behavior: Missing labels/policy text, submit enabled with wrong rules, or infinite loader

Trigger: User is already authenticated and visits /register
Correct Behavior: Redirect to home/profile (or show “already logged in”)
False Behavior: Allows creating another account while logged in or breaks session

Trigger: i18n toggle EN/EL on the register page
Correct Behavior: All labels/help text/errors switch; user-entered values stay intact
False Behavior: Mixed language labels or input fields reset/clear

---

## Field validation (client + server alignment)

Trigger: First/Last name empty
Correct Behavior: Inline required error; prevents submit
False Behavior: Submit allowed; server rejects with generic error or creates incomplete account

Trigger: Names contain leading/trailing spaces
Correct Behavior: Trim on submit; stored normalized value; UI consistent after success
False Behavior: Stored with extra spaces leading to duplicate-like users or display issues

Trigger: Names contain Unicode (Greek), hyphen, apostrophe
Correct Behavior: Accepted within defined charset rules; renders correctly
False Behavior: Rejected unexpectedly or saved with corrupted characters

Trigger: Extremely long names
Correct Behavior: Enforce max length; clear error message
False Behavior: Layout breaks or server truncates silently

Trigger: Email is invalid format
Correct Behavior: Inline validation before submit; consistent with server rules
False Behavior: Client accepts but server rejects, or client rejects valid addresses

Trigger: Email has uppercase letters / whitespace
Correct Behavior: Trim; normalize case (usually lower) for uniqueness; consistent behavior
False Behavior: `Test@X.com` and `test@x.com` treated as different accounts

Trigger: Email includes plus addressing ([user+tag@gmail.com](mailto:user+tag@gmail.com))
Correct Behavior: Accepted (unless explicitly disallowed) and treated consistently in uniqueness checks
False Behavior: Accepted but later login/confirmation fails due to normalization mismatch

Trigger: Duplicate email already registered
Correct Behavior: Server returns conflict; UI shows “Email already in use” without revealing extra info beyond policy
False Behavior: Creates duplicate accounts or leaks user existence in an inconsistent way

Trigger: Date of birth missing
Correct Behavior: If required, block submit with clear message; if optional, allow submit
False Behavior: Inconsistent requirement (sometimes required, sometimes not)

Trigger: Date of birth entered via manual typing vs picker
Correct Behavior: Both paths validated; stored as date-only; no timezone drift
False Behavior: Off-by-one day stored, invalid format accepted, or typing bypasses validation

Trigger: Underage DOB (if age rules exist)
Correct Behavior: Block with policy message; server enforces too
False Behavior: Client blocks but server allows (or vice versa)

---

## Password + confirm password

Trigger: Password shorter than policy (e.g., < 8 chars)
Correct Behavior: Inline error; submit disabled; server matches policy
False Behavior: Client allows but server rejects, or policy text is wrong

Trigger: Password meets length but fails complexity (if required)
Correct Behavior: Clear requirements; field-level error
False Behavior: Vague “invalid password” or inconsistent rules between client/server

Trigger: Password and Confirm Password mismatch
Correct Behavior: Immediate mismatch error; prevents submit
False Behavior: Submit allowed and server rejects; or error appears on wrong field

Trigger: Password contains leading/trailing spaces
Correct Behavior: Define rule (either allow exactly or trim consistently) and enforce identically on login
False Behavior: Account created but user can’t log in due to hidden spaces

Trigger: Show/Hide password toggle
Correct Behavior: Only toggles visibility; does not change value; keyboard accessible
False Behavior: Clears field, moves caret unexpectedly, or leaks password in logs

Trigger: Password is common/breached (if you check)
Correct Behavior: Block with clear message; server enforces
False Behavior: Allows weak passwords or inconsistent acceptance

Trigger: Submit clicked multiple times
Correct Behavior: Single request; button disabled in-flight; idempotent UI
False Behavior: Multiple accounts created / multiple confirmation emails / race conditions

---

## Consent checkbox (GDPR/terms)

Trigger: Consent checkbox is unchecked and is required
Correct Behavior: Prevent submit; clear error indicating consent required
False Behavior: Submit succeeds without consent or error is unclear

Trigger: Consent checkbox toggled then language toggled
Correct Behavior: Consent state remains; text updates to selected language
False Behavior: Checkbox resets or consent state lost

Trigger: Consent copy includes link(s) (privacy policy/terms)
Correct Behavior: Links open correctly; do not navigate away without warning if form dirty
False Behavior: Broken links or form data lost unexpectedly

Trigger: Consent recorded server-side
Correct Behavior: Timestamp/version recorded; auditable; consistent across retries
False Behavior: Account created but consent not stored (compliance gap)

---

## Submission outcomes + flows

Trigger: Registration succeeds
Correct Behavior: User redirected to login or logged in automatically (per design); success message localized
False Behavior: No feedback; user stuck on form; double-submits

Trigger: Email confirmation required
Correct Behavior: UI explains next step; resend mechanism works; user cannot access protected areas until confirmed
False Behavior: User appears active but can’t log in; or can log in without confirmation unexpectedly

Trigger: Registration fails due to server validation
Correct Behavior: Field-level errors mapped properly; user input preserved
False Behavior: Generic error; form clears; user forced to retype everything

Trigger: Registration fails due to network/timeout
Correct Behavior: Error shown; retry possible; no duplicate account created on retry
False Behavior: Unclear state (“did it register?”), or retry creates duplicates

Trigger: Server returns 429 rate limit / anti-abuse triggered
Correct Behavior: Clear message and cooldown guidance; UI prevents spamming
False Behavior: Infinite retries or silent failures

---

## Security + abuse resistance

Trigger: Attempt XSS in name fields (`<script>`)
Correct Behavior: Stored/returned safely escaped; never executed in UI or emails
False Behavior: Stored XSS shows up in profile/admin tables

Trigger: SQLi-like payload in inputs
Correct Behavior: Treated as plain text; server parameterized; no errors leaked
False Behavior: Server errors expose internals or behavior changes

Trigger: Automated signups (bot)
Correct Behavior: Rate limiting/captcha/behavioral checks (as implemented) work; audit events recorded
False Behavior: Unlimited account creation possible

Trigger: Enumeration via “email already exists” messaging
Correct Behavior: Consistent policy (either generic or explicit) applied everywhere; no contradictory leakage
False Behavior: Some endpoints reveal existence while others don’t

Trigger: Password transmitted/logged
Correct Behavior: HTTPS in prod; no password in logs/analytics; secure headers
False Behavior: Password appears in console/network logs or sent over insecure channel

---

## Accessibility + UX

Trigger: Keyboard-only registration
Correct Behavior: Logical tab order; Enter submits; errors announced; focus moves to first invalid field
False Behavior: Focus lost, errors not readable, or checkbox unreachable

Trigger: Mobile / small viewport
Correct Behavior: Form remains usable; no clipped inputs; picker works
False Behavior: Horizontal scroll, overlapped controls, or date picker unusable
