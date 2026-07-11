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
| High | 1 |
| Medium | 3 |
| Low | 4 |
| Info | 0 |

Overall the domain rules are coherent and defensively coded in the parts that exist: tier gating is consistently enforced on enrollment, assessment submission, and certificate issuance; the one-active-subscription invariant is backed by a filtered unique index; enrollment and certificate issuance both handle the duplicate-insert race correctly; and billing transactions are recorded for every plan change with a sensible transaction-type taxonomy. The headline gaps are lifecycle, not gating: paid subscriptions never actually expire, cancellation immediately forfeits paid time, and the real payment path is inert (checkout 503s in production and the webhook does not activate anything).

## 3. Findings

### BIZ-1: Paid subscriptions never expire — `ExpiresAt` is never enforced  [High] [Effort: M]
- **Evidence:** `Application/ApiServices/SubscriptionService.cs:68-121` — `GetUserStatusAsync` and `GetUserTierAsync` select the subscription where `us.IsActive` and return its plan tier/features **without any `ExpiresAt > now` check**. Nothing in the codebase deactivates a subscription when `ExpiresAt` passes (no background job; `AssignPlanAsync` only sets `IsActive=false` when a *new* plan is assigned).
- **Impact:** Once a user is on a paid plan, `IsActive` stays true indefinitely. A monthly Plus/Pro subscription grants full access forever regardless of the `ExpiresAt` the code carefully computed at `AssignPlanAsync:222-229`. There is no renewal charge (mock) and no expiry, so paid entitlements never lapse. On any real deployment this is lost revenue and incorrect entitlement.
- **Recommendation:** Treat a subscription as active only when `IsActive && (ExpiresAt == null || ExpiresAt > UtcNow)` in both status/tier reads, and add a background sweep (mirroring `CallRingMonitor`) that deactivates expired subscriptions and reverts users to Free (recording a `Downgrade`/expiry `BillingTransaction`).

### BIZ-2: Cancellation immediately revokes paid access, contradicting the documented "active until ExpiresAt" intent  [Medium] [Effort: S]
- **Evidence:** `Application/ApiServices/SubscriptionService.cs:283-345` (`CancelSubscriptionAsync`) sets the current sub `IsActive=false`, stamps `CancelledAt`, and immediately creates a new **Free** active subscription. The `UserSubscription.CancelledAt` XML doc states "Subscription remains active until ExpiresAt after cancellation" — the code does the opposite.
- **Impact:** A user who paid for a month and cancels on day 2 loses Plus/Pro access instantly and forfeits the remaining paid period. This is both a domain-rule inconsistency and a likely user-trust/refund issue on a real deployment.
- **Recommendation:** Decide the intended policy. If "cancel = keep access until period end," set `CancelledAt` but leave `IsActive`/tier intact until an expiry sweep (BIZ-1) flips it at `ExpiresAt`. If "cancel = immediate," update the entity documentation to match.

### BIZ-3: Real payment path is inert — production checkout 503s and the webhook activates nothing  [Medium] [Effort: L]
- **Evidence:** `Web/Controllers/SubscriptionController.cs:79-93` returns 503 (`pending_payment`) when `Payment:MockEnabled` is off (the production default). `SubscriptionController.cs:147-154` verifies the Stripe signature but then logs "Event processing not yet implemented" and returns 200 without dispatching to `AssignPlanAsync`.
- **Impact:** There is no working way to purchase a plan in a non-mock (production) configuration: checkout cannot complete, and even a correctly signed `checkout.session.completed` event does not grant a tier. Monetisation is non-functional outside Development.
- **Recommendation:** Implement the documented event dispatch (`checkout.session.completed → AssignPlanAsync`, `customer.subscription.updated → tier update`, `customer.subscription.deleted → revert to Free`) inside a transaction, and wire a real checkout-session creation. Ensure the webhook fails closed first (SEC-4).

### BIZ-4: Mock checkout grants any plan with zero payment  [Medium] [Effort: S]
- **Evidence:** `Application/ApiServices/SubscriptionService.cs:145-202` — when `Payment:MockEnabled` is true, `CreateCheckoutSessionAsync` calls `AssignPlanAsync` and records a paid-looking `BillingTransaction` **without any charge**. `MockEnabled=true` is set in `appsettings.Development.json`.
- **Impact:** In Development any authenticated student can POST `/api/subscriptions/checkout` and instantly receive Pro. This is intended for demos and is off by default in production, so it is not exploitable there — but it is a domain rule worth flagging because the "purchase" produces a real `BillingTransaction` row indistinguishable from a paid one, and any environment that accidentally enables the flag grants free upgrades.
- **Recommendation:** Keep the mock, but guard it so it can only ever run under `IsDevelopment()` (not merely a config flag), and mark mock transactions distinctly (e.g. a `Description`/type marker) so they are never mistaken for real payments in reporting.

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
| BIZ-1 | High | M | Enforce `ExpiresAt` in status/tier reads + add an expiry sweep that reverts to Free |
| BIZ-2 | Medium | S | Align cancellation behaviour with intended policy (keep-until-expiry vs immediate) |
| BIZ-3 | Medium | L | Implement webhook event dispatch + real checkout session for production |
| BIZ-4 | Medium | S | Gate mock checkout to `IsDevelopment()` and mark mock transactions distinctly |
| BIZ-5 | Low | S | Filter/lock assessments by `RequiredTier` in the list view |
| BIZ-6 | Low | M | (Optional) enforce enrollment cap under stricter isolation |
| BIZ-7 | Low | M | (Optional) re-evaluate completion/certificates on curriculum change |
| BIZ-8 | Low | M | Add proration when real billing lands |

## 5. Related Findings Elsewhere

- **SEC (25):** Webhook fails open when the signing secret is unset (SEC-4) — the security counterpart to BIZ-3.
- **REL (26):** Certificate auto-generation failure is swallowed on completion (REL-5); admin-seed failure ignored (REL-4).
- **DQ (28):** Billing/subscription referential integrity and the one-active-subscription filtered index diverging between SQL Server and SQLite tests.
- **COMP (29):** Minor (under-18) registration proceeds without parental-consent enforcement — a domain rule owned by COMP for its regulatory nature.
