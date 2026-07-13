# Audit: Compliance

| | |
|---|---|
| Finding prefix | COMP |
| Created | 2026-07-11 |
| Scope | Regulatory posture: GDPR personal-data inventory, lawful basis & consent capture, special-category (psychosocial) data, right to erasure/access/portability, data retention, cookie consent, and the presence of privacy/terms notices. |
| Delegated | Technical security controls (encryption transport, token handling) → SEC (25). Cascade/orphan mechanics of deletion → DQ (28). The unhandled-exception behaviour of the delete path → REL (26). Consent-capture *UX* wording/flow polish → UX (33). |

## 1. Methodology

Inventoried personal data by reading the entities that store it (`Identity/ApplicationUser.cs`, `Domain/Entities/{ChatMessage,ChatConversation,CallSession,CallParticipant,AssessmentSubmission,Certificate,BillingTransaction,RefreshToken}.cs`) and the consent fields (`ApplicationUser.GdprConsentGiven/Date`, `ParentalConsentGiven`). Traced the consent-capture path (`RegisterRequestDto` `GdprConsent` validation, `Web/Pages/Register.razor`, `AuthApiService.RegisterAsync` / `AuthService.RegisterAsync`), the deletion path (`AdminController.DeleteUser` → `AdminUserService.DeleteUserAsync`, "GDPR data deletion" per its own doc-comment), and searched the Razor pages/layout for any privacy-policy, terms, cookie-consent, or data-export UI. Checked cookie declarations (`.RYF.Auth`, `.RYF.AdminUserId`, culture cookie) for essential/consent classification.

NOT examined: contractual DPA/processor agreements and jurisdiction-specific obligations (legal, not code). This report flags posture gaps evidenced in code; it is not legal advice.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 1 |
| Medium | 3 |
| Low | 2 |
| Info | 1 |

The project shows GDPR *awareness* — registration requires explicit `GdprConsent` (validated true via `[Range(typeof(bool),"true","true")]`), consent date is captured, an under-18 branch and a `ParentalConsentGiven` flag exist, cookies in use are functional/essential (no analytics or marketing trackers), and the admin delete is labelled a GDPR action. But awareness outpaces implementation: there is now a bilingual privacy/terms notice for the consent to point at (COMP-1, fixed), but there is still no data-access/portability export, erasure is incomplete and technically blocked, sensitive psychosocial assessment answers are stored in plaintext with no special-category handling, and there is no retention policy or purge. For a university certificate project these are appropriately mid-severity, but one would be blocking on any real deployment handling real people's data.

> **Fixed since audit:** COMP-1 (High — consent captured against no policy) — added bilingual `/privacy` (Privacy Policy) and `/terms` (Terms of Service) pages backed by a new `LegalRes` resource set (EN + EL, hand-edited Designer), reflecting the COMP-8 data inventory, the special-category assessment handling, retention, rights, and essential-cookies posture. The registration consent checkbox now carries a help line — "By registering, you agree to our Privacy Policy and our Terms of Service" — linking both pages, and the public landing footer links them too. Verified live in EN and EL. The pages are honest about being a non-commercial university project and note they are not legal advice.

## 3. Findings

### COMP-2: Sensitive psychosocial data stored in plaintext with no special-category handling  [High] [Effort: L]
- **Evidence:** `Domain/Entities/AssessmentSubmission.cs` stores `AnswersJson`/`SummaryJson` for a "psychosocial career counseling platform" (`AssistantService.cs:138` describes the platform); persisted verbatim by `AssessmentService.SubmitAssessmentAsync:134-145`. No encryption-at-rest, access logging, or GDPR Art. 9 special-category classification exists. The assistant system prompt itself acknowledges users may describe "a crisis or serious distress" (`AssistantService.cs:141`).
- **Impact:** Psychological/wellbeing assessment responses are likely special-category data under GDPR Art. 9, which requires explicit consent for that specific processing and heightened safeguards. Storing them as plain JSON alongside ordinary data, readable by any admin and included in unfiltered backups, is a significant compliance and privacy exposure on a real deployment.
- **Recommendation:** Classify assessment answers as special-category; obtain explicit, separate consent for processing them; encrypt at rest (column/field-level) and restrict/audit admin access. Define who may read submissions and why.

### COMP-3: Right to erasure is incomplete and technically blocked  [Medium] [Effort: M]
- **Evidence:** `AdminController.DeleteUser` is documented "GDPR data deletion" but `AdminUserService.DeleteUserAsync:183-199` hard-deletes via Identity and is blocked by Restrict FKs on chat/call rows (see DQ-1/REL-1). Even when it succeeds, chat message content, call records, and assessment answers referencing the user are either cascade-deleted inconsistently or left in place; there is no anonymisation and no self-service erasure request.
- **Impact:** A data subject's erasure request cannot be reliably fulfilled: the operation fails for active users (those with chat/call history) and leaves residual personal data or over-deletes retained records for others. There is no user-initiated erasure at all.
- **Recommendation:** Implement a deterministic erasure workflow (anonymise PII, remove or pseudonymise chat/assessment content, retain only what a lawful basis requires) that succeeds for all users, and expose a user-facing erasure request path. Depends on the DQ-1 deletion-strategy decision.

### COMP-4: No data access/portability export  [Medium] [Effort: M]
- **Evidence:** Reviewed the full controller set — there is no endpoint that returns a user's personal data (profile, enrollments, submissions, chat, billing) in a portable form. `ProfileController` only returns the basic profile.
- **Impact:** GDPR Art. 15 (access) and Art. 20 (portability) require providing the subject's data on request in a machine-readable format. There is no way to satisfy this today.
- **Recommendation:** Add an authenticated "download my data" export (JSON) aggregating profile, consent record, enrollments/completions, assessment submissions, certificates, billing history, and chat.

### COMP-5: No data-retention policy or purge for personal data  [Medium] [Effort: M]
- **Evidence:** No scheduled purge exists for `RefreshToken` (revoked/expired rows accumulate — `AuthApiService` never deletes them), `ChatMessage`/`CallSession` history, `AssessmentSubmission`, or `BillingTransaction`. `CallRingMonitor` sweeps *dangling* sessions but never prunes old data.
- **Impact:** Personal data is retained indefinitely with no defined retention period, contrary to GDPR storage-limitation (Art. 5(1)(e)). Revoked refresh-token rows also grow unbounded.
- **Recommendation:** Define retention periods per data category and add a purge job (revoked/expired refresh tokens first — quick win; then aged chat/call/assessment data per policy).

### COMP-6: Under-18 registration proceeds without parental-consent enforcement  [Low] [Effort: M]
- **Evidence:** `AuthApiService.RegisterAsync:49-54` logs "Under-18 user registered … Parental consent not yet implemented" and allows the registration; `ApplicationUser.ParentalConsentGiven:65-69` is a documented placeholder never set or checked.
- **Impact:** GDPR Art. 8 requires parental consent for children (age threshold 13–16 by member state) for information-society services. Minors can register and submit psychosocial assessments with no parental-consent gate.
- **Recommendation:** Enforce the age gate: block or restrict under-threshold registrations pending verifiable parental consent, and wire the `ParentalConsentGiven` flag into the flow.

### COMP-7: Consent is all-or-nothing with no granular purpose or withdrawal mechanism  [Low] [Effort: M]
- **Evidence:** A single `GdprConsentGiven` boolean is captured at registration (`ApplicationUser.cs:57-63`). There is no separate consent for special-category assessment processing (see COMP-2), no marketing/email-communications consent, and no UI to withdraw consent.
- **Impact:** GDPR requires consent to be specific, granular, and as easy to withdraw as to give. A single blanket flag with no withdrawal path does not meet this.
- **Recommendation:** Split consent by purpose (account processing, special-category assessments, optional communications), record each with timestamp, and provide a withdrawal control in the profile.

### COMP-8: Personal-data inventory (from this audit)  [Info] [Effort: S]
- **Evidence:** Personal data identified across entities: identity/profile (`ApplicationUser`: name, email, DOB, avatar, last-seen), enrollments & completions, **assessment submissions (special-category)**, certificates (recipient name snapshot), billing transactions, chat conversations & message content, call sessions/participants, refresh tokens, and audit stamps (`CreatedByUserId`/`UpdatedByUserId`). Cookies: `.RYF.Auth` (essential), `.RYF.AdminUserId` (essential, admin), culture cookie (essential, `IsEssential=true`).
- **Impact:** No standing data-map/RoPA (Record of Processing Activities) exists in the repo; this inventory is the starting point.
- **Recommendation:** Maintain a RoPA covering the categories above with lawful basis and retention per category; feed it into the privacy policy (COMP-1).

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| COMP-2 | High | L | Classify/encrypt assessment answers as special-category; explicit consent + access controls |
| COMP-3 | Medium | M | Implement complete, reliable erasure (anonymise + dependent cleanup) + user request path |
| COMP-4 | Medium | M | Add "download my data" export (access/portability) |
| COMP-5 | Medium | M | Define retention periods + purge job (start with revoked/expired refresh tokens) |
| COMP-6 | Low | M | Enforce under-18 parental-consent gate |
| COMP-7 | Low | M | Granular, withdrawable consent by purpose |
| COMP-8 | Info | S | Maintain a Record of Processing Activities |

## 5. Related Findings Elsewhere

- **DQ (28):** DQ-1 — the FK/cascade design that makes erasure (COMP-3) technically impossible today; owns the constraint decision.
- **REL (26):** REL-1 — the unhandled exception that surfaces when erasure is attempted on a user with chat/call history.
- **SEC (25):** SEC-1 (refresh-token lifecycle) and SEC-10 (secret handling) — the token store whose retention COMP-5 also addresses.
- **BIZ (27):** BIZ — subscription/billing records are part of the retained personal-data set.
- **UX (33):** Consent-flow wording and the presence/visibility of policy links in the registration UX.
