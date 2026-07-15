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
| High | 0 |
| Medium | 0 |
| Low | 2 |
| Info | 1 |

Overall data integrity is well tended: unique constraints back every natural key that matters (one certificate per user-course, one enrollment per user-course, ordered-pair chat uniqueness, one active subscription via filtered index, one call-participant per session), a global soft-delete query filter plus matching dependent filters prevent the classic "principal filtered out" surprise, `AuditableEntity` stamps created/updated automatically, and both certificate and enrollment insert paths handle the duplicate-key race. All three Medium findings are now fixed: assessment answers are validated against the referenced schema on submit (DQ-2), a sweep for the same DTO/column length-mismatch pattern found and fixed it in three places, not just testimonials (DQ-3), and both the assessment-schema write path and the plan-features/schema-resolution error logs now carry enough context to catch and diagnose corrupt JSON (DQ-4). DQ-7 is now fixed too: the Certificate/CourseReview→Course relationship was actually required (INNER JOIN, silently dropping rows on soft-delete) despite documentation claiming otherwise — it's now genuinely optional.

## 3. Findings

### DQ-5: Subscription plans are re-seeded with fresh GUIDs and never repaired  [Low] [Effort: S]
- **Evidence:** `SubscriptionPlanSeeder.SeedAsync:17-21` is idempotent by "any plans exist" — it assigns `Guid.NewGuid()` per plan on first seed and skips entirely thereafter.
- **Impact:** If a plan row is deleted or its features drift, re-running the seeder will not restore or reconcile it (the "any exist" short-circuit). Plan IDs are non-deterministic across fresh databases, complicating any fixed references.
- **Recommendation:** Seed with deterministic IDs and upsert by tier/name so the seeder can repair/rebalance the feature matrix rather than only bootstrap it.

### DQ-6: One-active-subscription unique index exists only on SQL Server  [Low] [Effort: S]
- **Evidence:** `UserSubscriptionConfiguration.cs:38-45` — the filtered unique index (`[IsActive] = 1`) is documented as skipped for the SQLite/InMemory test context because `HasFilter` isn't supported there.
- **Impact:** The "at most one active subscription per user" invariant is enforced by the DB in production but only by application logic (`AssignPlanAsync` deactivation loop) under test. Tests cannot catch a regression that would create two active subscriptions; behaviour diverges between environments.
- **Recommendation:** Add an application-level guard/assertion that holds in both providers, or a test that asserts the single-active invariant explicitly against the relational provider.

### DQ-7: `Certificate.Course` was configured optional but the FK stayed `IsRequired` — the intent never actually took effect  [Fixed]
- **Evidence:** `CertificateConfiguration.cs:64-72` set `CourseId` `IsRequired()`, which made the relationship required despite the comment claiming a LEFT JOIN. EF Core's own model-validation warning (10622, "Course... is the required end of a relationship... may lead to unexpected results when the required entity is filtered out") flagged exactly this. A new test proved the real behavior: `Include(c => c.Course)` used an INNER JOIN, so soft-deleting a course silently dropped the certificate row out of query results entirely — not the "survives soft-delete" behavior the comment described. `CourseReview.CourseId` had the same problem (required by convention, no explicit override).
- **Fixed (2026-07-15):** `Certificate.CourseId` and `CourseReview.CourseId` are now `Guid?`, with `IsRequired(false)` on both relationships (migration `DB_CertificateCourseReview_OptionalCourse`). EF now generates a real LEFT JOIN with the soft-delete filter applied in the `ON` clause, so both rows survive and `Course` comes back `null` when the course is gone — matching the snapshot-first pattern (`CourseTitleEn`/`El` on Certificate) that already assumed this. Covered by `ApplicationDbContextTests.Certificate_SurvivesCourseSoftDelete_WithNullCourseNavigation` and `.CourseReview_SurvivesCourseSoftDelete_WithNullCourseNavigation`.
- **Recommendation:** None remaining. Consuming code must still null-check `Course` after this change — `CertificatesController.Verify`/`GetMyCertificates` already do via the snapshot fields; `CourseReviewService.GetPagedAsync`/`ApproveAsync` already use `r.Course?`/`r.Course!` appropriately.

### DQ-8: Chat/call `Restrict` FKs mean deleting a conversation leaves call sessions dangling by design  [Info] [Effort: S]
- **Evidence:** `CallSessionConfiguration.cs:24-28` nulls `ConversationId` on conversation delete (`SetNull`); `ChatMessage.CallSessionId` is `SetNull` (`ChatMessageConfiguration.cs:31-35`). Call history intentionally survives conversation/message deletion.
- **Impact:** Intentional and consistent (call history outlives chat), noted so it is not mistaken for an orphan bug. Note: *user* deletion (former DQ-1, fixed) does remove the user's call history; this finding is about *conversation* deletion only.
- **Recommendation:** No action; retention intent recorded here.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| DQ-5 | Low | S | Deterministic-ID upsert seeding for subscription plans |
| DQ-6 | Low | S | Guard/test the single-active-subscription invariant across both providers |
| DQ-8 | Info | S | Record the call-history retention intent |

## 5. Related Findings Elsewhere

- **COMP (29):** PII in chat/assessment rows; COMP owns the regulatory obligation and special-category classification of assessment answers. GDPR erasure itself is unblocked (former DQ-1/REL-1, fixed).
- **BIZ (27):** DQ-6 backs the one-active-subscription rule.
- **DB (30):** Index/column type design, migration history, and provider-specific schema differences.
- **SEC (25):** Input validation on rich-text/JSON write paths overlaps with sanitizer coverage.
