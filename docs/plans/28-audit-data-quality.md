# Audit: Data Quality

| | |
|---|---|
| Finding prefix | DQ |
| Created | 2026-07-11 |
| Scope | Data integrity: constraint gaps, validation holes, DTO/column mismatches, orphan and cascade risk, nullable misuse on domain data, seed-data integrity, and JSON-payload validation. |
| Delegated | Schema/migration/index *design* & EF usage → DB (30). GDPR retention/erasure/special-category classification → COMP (29). Technical vulnerabilities → SEC (25). Unhandled-exception behaviour → REL (26). Domain-rule correctness → BIZ (27). |

## 1. Methodology

Reviewed every domain entity (`Domain/Entities/*`, `Identity/ApplicationUser.cs`) against its EF configuration (`Infrastructure/Data/Configurations/*`) and against the DTOs that feed it (`Application/DTOs/**`), looking for (a) DTO validation weaker or stronger than the persisted column, (b) FK `OnDelete` behaviours that orphan or block, (c) unvalidated JSON blobs, (d) nullable domain fields that should be required, and (e) seed idempotency. Read the audit/soft-delete infrastructure in `ApplicationDbContext.cs` (global `IsDeleted` query filter, `ApplyAuditFields`) and the seeders (`SubscriptionPlanSeeder`, `BlogArticleSeeder`, `CourseSeeder`, `StudentSeeder`, `BulkStudentSeeder`).

NOT examined: index selection/coverage and column types for performance → DB (30).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 1 |
| Medium | 4 |
| Low | 3 |
| Info | 1 |

Overall data integrity is well tended: unique constraints back every natural key that matters (one certificate per user-course, one enrollment per user-course, ordered-pair chat uniqueness, one active subscription via filtered index, one call-participant per session), a global soft-delete query filter plus matching dependent filters prevent the classic "principal filtered out" surprise, `AuditableEntity` stamps created/updated automatically, and both certificate and enrollment insert paths handle the duplicate-key race. The weak spots are user-deletion orphan/blocking behaviour, unvalidated JSON payloads (assessment answers, plan features, assessment schema), and a DTO/column length mismatch that will throw on save.

## 3. Findings

### DQ-1: User deletion is blocked by Restrict FKs, orphaning chat/call data and stranding the row  [High] [Effort: M]
- **Evidence:** User FKs cascade for `Enrollment`, `AssessmentSubmission`, `UserSubscription`, `RefreshToken`, `Certificate`, `BillingTransaction`, but **restrict** for `ChatConversation` (Creator/Participant), `ChatMessage` (Sender), `CallSession` (Initiator), and `CallParticipant` (User) — see `ChatConversationConfiguration.cs:19-27`, `ChatMessageConfiguration.cs:24-27`, `CallSessionConfiguration.cs:19-22`, `CallParticipantConfiguration.cs:19-22`. `AdminUserService.DeleteUserAsync` (`:183-199`) issues a hard delete.
- **Impact:** For any user with chat/call history the delete cannot proceed (FK violation), so the user record cannot be removed — while for users without such history the cascade deletes their conversations' counterpart data references inconsistently. The result is an all-or-nothing integrity conflict: either the delete is blocked or (for cascade-eligible data) it silently removes billing/certificate history that may need retention. There is no anonymisation path.
- **Recommendation:** Define an explicit deletion strategy: soft-delete/anonymise the user (scrub PII, keep referential rows) or, for hard delete, first reassign/remove chat & call rows in a transaction. This is the integrity root cause behind REL-1 (the crash) and COMP's erasure gap.

### DQ-2: Assessment answers are stored as unvalidated free-form JSON  [Medium] [Effort: M]
- **Evidence:** `AssessmentService.SubmitAssessmentAsync:134-145` persists `request.AnswersJson` / `SummaryJson` verbatim. The DTO caps length only (`SubmitAssessmentRequest`: `AnswersJson [MaxLength(50_000)]`, `SummaryJson [MaxLength(20_000)]`, `AssessmentDtos.cs`). There is no validation that the JSON is well-formed or that it conforms to the assessment's `SchemaJson`.
- **Impact:** A client can submit arbitrary (or malformed) JSON that does not match the assessment's questions. Stored submissions may be un-parseable or semantically meaningless, corrupting any later analysis/history rendering and breaking the "answers correspond to a schema" invariant.
- **Recommendation:** Validate `AnswersJson` parses as JSON and matches the referenced assessment schema (question keys/types) server-side before persisting; reject with 400 otherwise.

### DQ-3: DTO `MaxLength` exceeds the persisted column length on testimonials → save-time failure  [Medium] [Effort: S]
- **Evidence:** `SaveTestimonialRequest` allows `FullName`/`RoleOrTitle`/`CompanyOrContext` up to `MaxLength(200)` (`DTOs/Testimonials/SaveTestimonialRequest.cs:10-12`), but `TestimonialConfiguration.cs:13-21` caps those columns at `HasMaxLength(150)`.
- **Impact:** A 151–200 character value passes DTO validation and then throws on `SaveChanges` (SQL Server string-truncation error → 500). The two length rules disagree, so valid-per-DTO input is invalid-per-schema.
- **Recommendation:** Align the DTO `MaxLength` to the column (150), or widen the column to 200. Audit other DTO/column pairs for the same drift.

### DQ-4: Plan features and assessment schema JSON are parsed defensively but never validated on write  [Medium] [Effort: M]
- **Evidence:** `SubscriptionService.DeserializeFeatures:387-403` swallows a malformed `FeaturesJson` and returns `null`, which then silently falls back to default Free features. `AssessmentService.ResolveSchemaJsonByLang:197-295` catches parse errors and returns the original string. `SaveAssessmentDefinitionRequest.SchemaJson` is `[Required] string` with no structural validation (`AssessmentDtos.cs`).
- **Impact:** A plan row with corrupt `FeaturesJson` silently degrades every subscriber on it to Free-tier features with no error (entitlement data-integrity bug). A malformed assessment schema is stored and only fails at render time. Bad data enters the system unremarked.
- **Recommendation:** Validate `FeaturesJson`/`SchemaJson` structure at write time (seeder + admin save), and log at Error (not silently default) when an existing row fails to deserialize so corruption is visible.

### DQ-5: Subscription plans are re-seeded with fresh GUIDs and never repaired  [Low] [Effort: S]
- **Evidence:** `SubscriptionPlanSeeder.SeedAsync:17-21` is idempotent by "any plans exist" — it assigns `Guid.NewGuid()` per plan on first seed and skips entirely thereafter.
- **Impact:** If a plan row is deleted or its features drift, re-running the seeder will not restore or reconcile it (the "any exist" short-circuit). Plan IDs are non-deterministic across fresh databases, complicating any fixed references.
- **Recommendation:** Seed with deterministic IDs and upsert by tier/name so the seeder can repair/rebalance the feature matrix rather than only bootstrap it.

### DQ-6: One-active-subscription unique index exists only on SQL Server  [Low] [Effort: S]
- **Evidence:** `UserSubscriptionConfiguration.cs:38-45` — the filtered unique index (`[IsActive] = 1`) is documented as skipped for the SQLite/InMemory test context because `HasFilter` isn't supported there.
- **Impact:** The "at most one active subscription per user" invariant is enforced by the DB in production but only by application logic (`AssignPlanAsync` deactivation loop) under test. Tests cannot catch a regression that would create two active subscriptions; behaviour diverges between environments.
- **Recommendation:** Add an application-level guard/assertion that holds in both providers, or a test that asserts the single-active invariant explicitly against the relational provider.

### DQ-7: `Certificate.Course` is optional while `CourseId` is required — deliberate but fragile  [Low] [Effort: S]
- **Evidence:** `CertificateConfiguration.cs:64-72` makes the `Course` navigation optional (LEFT JOIN, survives soft-delete) with `CourseId` `IsRequired` + `NoAction`.
- **Impact:** Correct for preserving certificates when a course is soft-deleted, but consuming code must always null-check `Course`; `CertificatesController.Verify`/`GetMyCertificates` rely on the denormalised `CourseTitleEn`/`El` snapshot instead (good). The risk is future code dereferencing `certificate.Course` after a soft-delete.
- **Recommendation:** Keep the snapshot-first pattern; add a code comment/analyzer note so future queries don't assume `Course` is loaded.

### DQ-8: Chat/call `Restrict` FKs mean deleting a conversation leaves call sessions dangling by design  [Info] [Effort: S]
- **Evidence:** `CallSessionConfiguration.cs:24-28` nulls `ConversationId` on conversation delete (`SetNull`); `ChatMessage.CallSessionId` is `SetNull` (`ChatMessageConfiguration.cs:31-35`). Call history intentionally survives conversation/message deletion.
- **Impact:** Intentional and consistent (call history outlives chat), noted so it is not mistaken for an orphan bug during the DQ-1 fix.
- **Recommendation:** No action; document the retention intent alongside the DQ-1 deletion-strategy decision.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| DQ-1 | High | M | Define user-deletion strategy (anonymise or transactional dependent cleanup) |
| DQ-2 | Medium | M | Validate assessment answers against schema on submit |
| DQ-3 | Medium | S | Reconcile testimonial DTO `MaxLength` with column length |
| DQ-4 | Medium | M | Validate feature/schema JSON on write; log (not silently default) on corrupt rows |
| DQ-5 | Low | S | Deterministic-ID upsert seeding for subscription plans |
| DQ-6 | Low | S | Guard/test the single-active-subscription invariant across both providers |
| DQ-7 | Low | S | Document the snapshot-first certificate pattern |
| DQ-8 | Info | S | Record the call-history retention intent |

## 5. Related Findings Elsewhere

- **REL (26):** DQ-1's Restrict FKs cause the unhandled `DeleteUser` exception (REL-1); DQ-3's mismatch produces a save-time 500.
- **COMP (29):** DQ-1 also blocks GDPR erasure and leaves PII in chat/assessment rows; COMP owns the regulatory obligation and special-category classification of assessment answers.
- **BIZ (27):** DQ-4's silent Free-feature fallback affects entitlement correctness (BIZ tier gating); DQ-6 backs the one-active-subscription rule.
- **DB (30):** Index/column type design, migration history, and provider-specific schema differences.
- **SEC (25):** Input validation on rich-text/JSON write paths overlaps with sanitizer coverage.
