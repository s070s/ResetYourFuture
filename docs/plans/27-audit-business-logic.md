# Audit: Business Logic

| | |
|---|---|
| Finding prefix | BIZ |
| Created | 2026-07-11 |
| Scope | Domain-rule correctness: subscription tier gating & lifecycle, payment/checkout/webhook flow, enrollment rules, lesson completion & progress, certificate issuance, assessment access rules. |
| Delegated | Webhook signature/authentication hardening → SEC (25). Unhandled exceptions / swallowed errors in these paths → REL (26). Constraint/validation integrity → DQ (28). GDPR/consent/minor-age regulatory rules → COMP (29). |

## 1. Methodology

Read the subscription domain end to end: `Application/ApiServices/SubscriptionService.cs` (status, tier, checkout, assign, cancel, billing), `Web/Controllers/SubscriptionController.cs` (checkout, webhook, cancel), `Infrastructure/Seeding/SubscriptionPlanSeeder.cs` (plan/feature matrix), and the `UserSubscription` / `BillingTransaction` entities + configurations. Traced tier gating through `CourseService.EnrollAsync`/`GetLessonDetailAsync`/`CompleteLessonAsync`, `AssessmentService` (list/get/submit), and `CertificatesController` + `Infrastructure/ApiServices/CertificateService.cs`. Checked the filtered unique index for one-active-subscription (`UserSubscriptionConfiguration.cs`) and the mock-payment gating (`Payment:MockEnabled`).

NOT examined: real Stripe API semantics (no live integration exists) and pricing/tax correctness (out of scope for a mock).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 4 |
| Info | 0 |

> **Accepted / deferred (blocked — will not implement in this pass):** BIZ-3 (the real, non-mock payment path is inert — production checkout 503s and the signed webhook activates nothing). This is Effort L and **externally blocked**: there is no Stripe secret key or SDK anywhere in the solution and never has been, so a real checkout-session call and a webhook-to-`AssignPlanAsync` dispatch cannot be built or verified here — both halves need a live Stripe test account (secret key + a dashboard product/price catalog to correlate events against). Writing the dispatch against a guessed payload with no account to send a real event and confirm it would produce exactly the unverified, likely-wrong code this audit series warns against. The safety precondition is already in place (the webhook fails closed without a signing secret — SEC-4, fixed). Consciously deferred until a Stripe test account exists; retained in full below with its assessment note. See the sibling accepted large-effort findings in [23-audit-maintainability.md](23-audit-maintainability.md) (MAINT-2/3).

Overall the domain rules are coherent and defensively coded in the parts that exist: tier gating is consistently enforced on enrollment, assessment submission, and certificate issuance; the one-active-subscription invariant is backed by a filtered unique index; enrollment and certificate issuance both handle the duplicate-insert race correctly; and billing transactions are recorded for every plan change with a sensible transaction-type taxonomy. Cancellation now keeps paid access until `ExpiresAt` instead of forfeiting it immediately (BIZ-2, fixed), and mock checkout can no longer grant a plan outside Development (BIZ-4, fixed). The remaining gap is the inert real payment path (BIZ-3), consciously accepted above as blocked on a live Stripe account this environment doesn't have.

## 3. Findings

### BIZ-3: Real payment path is inert — production checkout 503s and the webhook activates nothing  [Medium — Accepted/deferred] [Effort: L]
- **Evidence:** `Web/Controllers/SubscriptionController.cs:79-93` returns 503 (`pending_payment`) when `Payment:MockEnabled` is off (the production default). `SubscriptionController.cs:147-154` verifies the Stripe signature but then logs "Event processing not yet implemented" and returns 200 without dispatching to `AssignPlanAsync`.
- **Impact:** There is no working way to purchase a plan in a non-mock (production) configuration: checkout cannot complete, and even a correctly signed `checkout.session.completed` event does not grant a tier. Monetisation is non-functional outside Development.
- **Assessed (2026-07-14):** Not implemented. Confirmed there is no Stripe secret key anywhere in configuration (only `Payment:WebhookSecret`, used solely for signature verification) and no Stripe SDK package reference in the solution — this app has never held a live/test Stripe credential. Both halves of the recommendation are coupled and both need one: a real checkout-session call requires the Stripe API (secret key + a product/price catalog configured in an actual Stripe dashboard), and the webhook dispatch needs a real checkout session's `client_reference_id`/`metadata` to correlate an incoming event back to a local `userId`/`planId` — there is nothing to correlate against without the first half. Writing the dispatch logic against a guessed payload shape, with no live account to send a real event and verify it, would produce exactly the class of unverified, likely-wrong code this audit series warns against elsewhere (see MAINT-2). Deferred until a live Stripe account (test-mode secret key at minimum) is available to develop and verify against.
- **Recommendation:** Unchanged: implement the documented event dispatch (`checkout.session.completed → AssignPlanAsync`, `customer.subscription.updated → tier update`, `customer.subscription.deleted → revert to Free`) inside a transaction, and wire a real checkout-session creation, once a Stripe test account is available. The webhook already fails closed without a configured signing secret (SEC-4, fixed) — the safety precondition for wiring dispatch is in place.

### BIZ-5: Assessment list surfaces items above the user's `RequiredTier`  [Low] [Effort: S]
- **Evidence:** `Application/ApiServices/AssessmentService.cs:22-79` (`GetPublishedAssessmentsAsync`) gates only on `Features.AssessmentAccess`, not per-assessment `RequiredTier`. `SubmitAssessmentAsync:117-132` correctly enforces both `AssessmentAccess` and `Tier >= RequiredTier`.
- **Impact:** A Plus user sees Pro-tier assessments in the list and detail view but is rejected (403) only on submit — a confusing "visible but unusable" experience and a minor gating inconsistency.
- **Recommendation:** Either filter the list by `RequiredTier <= userTier`, or surface a clear "requires Pro" lock state in the list rather than failing at submit.

### BIZ-6: Enrollment course-limit is a TOCTOU soft limit  [Low] [Effort: M]
- **Evidence:** `Application/ApiServices/CourseService.cs:169-183` — count-then-insert with an in-code comment acknowledging two concurrent enrollments for *different* courses can both pass the `MaxCourses` gate.
- **Impact:** A user can briefly exceed their plan's course cap by racing requests. Explicitly accepted in code as a benign edge case; noted for completeness.
- **Recommendation:** Accept as documented, or enforce with serializable isolation / a per-user enrollment-count constraint if the cap becomes revenue-relevant.

### BIZ-7: Completed enrollment and issued certificate are not re-evaluated when lessons are added later  [Low] [Effort: M]
- **Evidence:** `Application/ApiServices/CourseService.cs:331-360` marks the enrollment `Completed` (and auto-issues a certificate) when `completedCount >= totalLessons` at that moment. `CertificateService.GetOrGenerateAsync` is idempotent per (user, course) and snapshots duration at issuance.
- **Impact:** If an admin adds lessons to a course after a student completed it, the enrollment stays `Completed` and the already-issued certificate reflects the smaller course. Progress can show <100% again while the certificate says complete — an inconsistency between certificate and current curriculum.
- **Recommendation:** On lesson/module changes, optionally reset affected enrollments' completion state (or version certificates by curriculum snapshot). Low priority for the current content-stable model.

### BIZ-8: No proration or credit on upgrade/downgrade/plan-switch  [Low] [Effort: M]
- **Evidence:** `SubscriptionService.cs:158-202` computes a transaction type (`Upgrade`/`Downgrade`/`PlanSwitch`/`Renewal`) and charges the new plan's full `Price`; `AssignPlanAsync` resets `ExpiresAt` from "now" for the new period.
- **Impact:** Switching plans mid-period forfeits remaining paid time and charges full price with no credit. Acceptable for a mock; would need proration logic for real billing.
- **Recommendation:** Defer until real payments; document as a known limitation.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| BIZ-3 | Medium — Accepted | L | Implement webhook event dispatch + real checkout session for production — blocked on a live Stripe test account; consciously deferred |
| BIZ-5 | Low | S | Filter/lock assessments by `RequiredTier` in the list view |
| BIZ-6 | Low | M | (Optional) enforce enrollment cap under stricter isolation |
| BIZ-7 | Low | M | (Optional) re-evaluate completion/certificates on curriculum change |
| BIZ-8 | Low | M | Add proration when real billing lands |

## 5. Related Findings Elsewhere

- **SEC (25):** Webhook now fails closed when the signing secret is unset (SEC-4, fixed) — the security counterpart to BIZ-3.
- **REL (26):** Certificate auto-generation failure (REL-5) and admin-seed failure (REL-4) are now surfaced rather than swallowed — both fixed.
- **DQ (28):** Billing/subscription referential integrity and the one-active-subscription filtered index diverging between SQL Server and SQLite tests.
- **COMP (29):** Minor (under-18) registration proceeds without parental-consent enforcement — a domain rule owned by COMP for its regulatory nature.
