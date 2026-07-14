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
| High | 0 |
| Medium | 0 |
| Low | 3 |
| Info | 1 |

The project shows GDPR *awareness* — registration requires explicit `GdprConsent` (validated true via `[Range(typeof(bool),"true","true")]`), consent date is captured, an under-18 branch and a `ParentalConsentGiven` flag exist, cookies in use are functional/essential (no analytics or marketing trackers), and the admin delete is labelled a GDPR action. Awareness has now been matched by implementation: bilingual privacy/terms notices exist (COMP-1, fixed), special-category assessment answers are encrypted at rest (COMP-2, fixed), erasure now has a working self-service path (COMP-3, fixed), a "download my data" export covers access/portability (COMP-4, fixed), and expired refresh tokens are purged (COMP-5, fixed for its quick-win slice — the broader per-category retention-policy question is now Low, since it's a policy decision more than a code gap).

## 3. Findings

### COMP-5: No data-retention policy for chat/call/assessment history  [Low] [Effort: M]
- **Evidence:** `ChatMessage`/`CallSession`/`AssessmentSubmission`/`BillingTransaction` rows are retained indefinitely with no defined retention period. **Fixed (2026-07-14):** the "quick win" half of this finding — expired `RefreshToken` rows accumulating forever — is resolved by `RefreshTokenPurgeService` (a background sweep, matching `SubscriptionExpirySweeper`'s convention, that deletes rows past `ExpiresAt`; safe purely by expiry since `AuthApiService.RefreshAsync` checks `ExpiresAt <= now` before ever consulting `RevokedAt`).
- **Impact:** What remains is the broader question of retention periods for chat/call/assessment/billing history — that's a business/legal policy decision (how long is "necessary" for this platform's purposes under GDPR storage-limitation, Art. 5(1)(e)), not a code gap the way the refresh-token accumulation was. Downgraded from Medium: the concrete, code-only piece is done.
- **Recommendation:** Once retention periods per category are decided, add purge jobs for aged chat/call/assessment/billing data following the same `BackgroundService` sweep pattern.

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
| COMP-5 | Low | M | Define retention periods for chat/call/assessment/billing history; add purge jobs once decided |
| COMP-6 | Low | M | Enforce under-18 parental-consent gate |
| COMP-7 | Low | M | Granular, withdrawable consent by purpose |
| COMP-8 | Info | S | Maintain a Record of Processing Activities |

## 5. Related Findings Elsewhere

- **DQ (28):** DQ-1 — the FK/cascade design; COMP-3's erasure path (fixed) explicitly cleans up chat/call/certificate rows before the Identity delete rather than relying on cascade alone.
- **REL (26):** REL-1 — the unhandled exception that used to surface when erasure was attempted on a user with chat/call history; unblocked, same as COMP-3.
- **SEC (25):** SEC-1 (refresh-token lifecycle) and SEC-10 (secret handling) — the token store whose expired-row purge COMP-5 also addresses (fixed for that slice).
- **BIZ (27):** BIZ — subscription/billing records are part of the retained personal-data set (now included in COMP-4's export).
- **UX (33):** Consent-flow wording and the presence/visibility of policy links in the registration UX.
