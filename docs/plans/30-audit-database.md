# Audit: Database

| | |
|---|---|
| Finding prefix | DB |
| Created | 2026-07-11 |
| Scope | Schema design, EF Core entity configurations, migrations, indexes, data types, cascade/delete behavior, and EF usage patterns (tracking, mapping-level Include strategy) across `src/ResetYourFuture.Domain/Domain/Entities/`, `src/ResetYourFuture.Infrastructure/Data/`, and the services/controllers that exercise them |
| Delegated | Query performance / N+1 → 34 (PERF). Injection & access-control vulnerabilities → 25 (SEC). Business-rule validation, seed & orphan data integrity → 28 (DQ). Multi-instance startup-migration races → 35 (SCALE) / 42 (OPS). REST/status-code/OpenAPI issues → 31 (API). |

## 1. Methodology

Examined in full: `src/ResetYourFuture.Infrastructure/Data/ApplicationDbContext.cs`; all 21 configuration classes in `src/ResetYourFuture.Infrastructure/Data/Configurations/`; all 22 entity classes in `src/ResetYourFuture.Domain/Domain/Entities/` plus `src/ResetYourFuture.Domain/Identity/ApplicationUser.cs`; `DesignTimeDbContextFactory.cs`; the `AddActiveSubscriptionUniqueIndex` migration and the current `ApplicationDbContextModelSnapshot.cs` (column types, indexes, FK behaviors); startup migration/seeding (`src/ResetYourFuture.Web/Startup/DatabaseSeedingExtensions.cs`, `AuthenticationSetupExtensions.cs`); and EF usage in `src/ResetYourFuture.Application/ApiServices/` (AdminCategoryService, AdminCourseService, AdminUserService, AuthApiService, SubscriptionService, ChatQueryService, BlogArticleService, TestimonialService) and the DbContext-using controllers (AdminModules, AdminLessons, AdminAssessments, Certificates, LessonAssets, SiteSettings). Test providers checked via `tests/ResetYourFuture.TestSupport/DbContextFactory.cs`.

NOT examined: the full body of every migration (`InitialCreate` was inspected via the model snapshot, which supersedes it); runtime query plans (no live database was queried — index-usage claims are reasoned from the model, not measured).

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 5 |
| Info | 2 |

The schema is well above student-project average: every entity has an explicit `IEntityTypeConfiguration`, string lengths are capped (including, now, Category names via a filtered unique index — former DB-5, fixed), uniqueness that matters (assessment key, blog slug, enrollment pair, certificate pair, one-active-subscription, category name) is enforced with real DB indexes — including correctly filtered unique indexes — and list-query indexes are deliberate and commented with their intended query shape. Migrations are clean, pinned to a tooling version, and applied consistently. `DateTimeOffset` columns are native `datetimeoffset` (DB-2, fixed) and the `DateTime` group now materializes with `Kind=Utc` enforced (former DB-6, fixed). All six original Medium findings are now fixed or substantially addressed: the audit-stamping bug is fixed (DB-3, fixed), unbounded JSON/string columns are capped (former DB-8, fixed), every admin-edited aggregate has a DB-generated concurrency token with one controller (Lesson) demonstrating the full stale-write-rejection pattern end to end (DB-7, mechanism complete and proven, per-controller rollout mechanical — downgraded to Low), and course soft-delete now cascades to its modules/lessons (DB-4, the concrete bug fixed; the broader entity-interface refactor remains a design decision — downgraded to Low).

## 3. Findings

### DB-4: `AuditableEntity`'s publish/soft-delete columns are still shared by entities that don't use them  [Low] [Effort: M]
- **Evidence:** Every `AuditableEntity` gets `IsPublished`/`PublishedAt`/`IsDeleted`/`DeletedAt` columns whether or not an aggregate uses them (`Category.cs` documents `IsPublished` as unused; `BlogArticle`/`AssessmentDefinition` are hard-deleted, never soft-deleted, so their `IsDeleted`/`DeletedAt` columns are always their default). **Fixed (2026-07-14):** the concrete bug — a soft-deleted course leaving its Modules/Lessons live (`IsDeleted=false`, still readable/editable via the admin module/lesson endpoints directly by id) — no longer exists: `AdminCourseService.DeleteCourseAsync` now cascades `IsDeleted`/`DeletedAt`/`UpdatedByUserId` to every module and lesson in the same `SaveChanges` call.
- **Impact:** What remains is dead-column noise (unused `IsPublished`/`IsDeleted` pairs on entities that never toggle them), not a live-data-visibility bug. Splitting `IPublishable`/`ISoftDeletable` out of `AuditableEntity` so entities only carry columns they use — and deciding whether BlogArticle/AssessmentDefinition should adopt soft-delete instead of hard-delete — is an entity-design decision, not a correctness fix, so it's downgraded from Medium.
- **Recommendation:** If picked up later: move `IsPublished`/`PublishedAt` and `IsDeleted`/`DeletedAt` into separate `IPublishable`/`ISoftDeletable` interfaces/marker configurations, opted into per entity, and decide BlogArticle/AssessmentDefinition's delete semantics at the same time.

### DB-7: No optimistic concurrency control on any domain entity  [Low] [Effort: M]
- **Evidence:** ~~No `IsRowVersion()`/`[Timestamp]`/`IsConcurrencyToken()` anywhere~~. **Fixed (2026-07-14):** every `AuditableEntity` subtype now has a DB-generated `RowVersion` (SQL Server `rowversion`) column, checked automatically in every UPDATE's WHERE clause by EF Core — no DTO/client round-trip needed, since the existing find-mutate-save controller pattern already loads and saves within one request/DbContext scope. Verified with a test (`ConcurrentEdit_StaleRowVersion_ThrowsConcurrencyException`) that manufactures the stale-original-value state a real concurrent SQL Server write leaves behind. `AdminLessonsController.UpdateLesson` catches `DbUpdateConcurrencyException` and returns 409 as the one aggregate demonstrating the complete pattern.
- **Impact:** What remains is wiring the same three-line try/catch into the other six admin-edited controllers (Course, Module, AssessmentDefinition, BlogArticle, Testimonial, Category) — every one of them already has the RowVersion column and will throw the same way, so this is now mechanical, low-risk rollout rather than an open design question. Downgraded from Medium: the hard part (does the mechanism work, does it need DTO/UI changes) is answered and proven.
- **Recommendation:** Add the same `try { SaveChangesAsync() } catch (DbUpdateConcurrencyException) { return Conflict(...); }` to the remaining six controllers' update actions.

### DB-9: The plain UserSubscriptions.UserId index was silently replaced by the filtered unique index  [Low] [Effort: S]
- **Evidence:** `UserSubscriptionConfiguration.cs` declares both `HasIndex(us => us.UserId)` ("Index for querying by user") and, later, `HasIndex(us => us.UserId).HasFilter("[IsActive] = 1").IsUnique()...` — EF treats a second `HasIndex` on the same property set as the same index, so only the filtered unique one exists: snapshot `ApplicationDbContextModelSnapshot.cs:1177-1180` shows a single UserId index, and migration `20260619145323_AddActiveSubscriptionUniqueIndex.cs:13-15` explicitly dropped `IX_UserSubscriptions_UserId`. The config comment also claims the test DbContext "skips this index via a separate configuration" — no such configuration exists (`tests/ResetYourFuture.TestSupport/DbContextFactory.cs` uses `ApplicationDbContext` directly; SQLite creates the partial index fine).
- **Impact:** Queries over a user's *inactive* subscriptions (history) can't seek on the filtered index; today's queries all filter `IsActive` so the practical cost is near zero, but the config no longer says what the schema does, and the stale comment misleads.
- **Recommendation:** Either delete the first `HasIndex(us => us.UserId)` call and fix the comments, or give it a distinct `HasDatabaseName` if unfiltered user lookups are wanted. `CategoryConfiguration.cs`'s matching stale comment is already fixed (former DB-5); only this one remains.

### DB-10: Delete paths mix redundant client-side cascades with genuinely required ones  [Low] [Effort: S]
- **Evidence:** `AdminModulesController.cs` `DeleteModule` and `AdminLessonsController.cs` `DeleteLesson` load `LessonCompletions` into memory and `RemoveRange` them "before deleting (FK constraint)" — but `LessonCompletionConfiguration.cs` sets `OnDelete(Cascade)` from Lesson, so the DB does this itself; the manual pass just adds round-trips and memory. Conversely, `AdminAssessmentsController.cs` `DeleteAssessment` removing `Submissions` first *is* required (`AssessmentDefinitionConfiguration.cs:45` is Restrict). The comments show the cascade model isn't understood consistently — the same misunderstanding that produced DB-1.
- **Impact:** Harmless today but noisy: entity loads purely to delete, misleading comments, and no single pattern to copy when adding the next delete endpoint.
- **Recommendation:** Rely on DB cascade where configured (drop the manual LessonCompletion removal); where client-side deletion is required (Restrict), prefer `ExecuteDeleteAsync` over load-then-RemoveRange. Document each aggregate's delete strategy next to its configuration.

### DB-12: Redundant and low-value indexes  [Low] [Effort: S]
- **Evidence:** `LessonCompletionConfiguration.cs` declares `HasIndex(lc => lc.UserId)` even though the unique composite `(UserId, LessonId)` directly above it already serves UserId-prefix seeks. Several single-column bool/enum indexes have low selectivity and rarely satisfy the real predicates alone: `CourseConfiguration` `IsPublished` (catalog queries also filter `IsDeleted` via the query filter and sort), `UserSubscriptionConfiguration` `IsActive`, `EnrollmentConfiguration` `Status`, `CertificateConfiguration` `Status`.
- **Impact:** Extra write cost and a schema that suggests these were added speculatively rather than for measured query shapes; contrast with the well-targeted composites elsewhere (`(IsPublished, PublishedAt)` on BlogArticles, `(ConversationId, SentAt)` on ChatMessages).
- **Recommendation:** Drop the redundant `LessonCompletion.UserId` index; review the single-column bool indexes once real query plans exist (PERF-34's measurement, DB's schema change). Not urgent — cheap to leave, cheap to fix.

### DB-13: Startup auto-migrate + seed, and a hardcoded fallback connection string in the design-time factory  [Info] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Startup/DatabaseSeedingExtensions.cs:46-47` runs `MigrateAsync()` on every boot (guarded for non-relational test providers); `DesignTimeDbContextFactory.cs:23-24` falls back to a hardcoded LocalDB connection string when configuration is absent.
- **Impact:** Correct and convenient for a single-instance university project (per 00-INDEX severity calibration). The multi-instance migration race and prod-vs-dev separation concerns are delegated to SCALE (35) / OPS (42) / CFG (39).
- **Recommendation:** None required now; if deployment ever matters, move migrations to a deploy step (`dotnet ef database update` or migration bundles).

### DB-14: AssistantContentChunk storage design is sound for its scale  [Info] [Effort: S]
- **Evidence:** `AssistantContentChunkConfiguration.cs` — embeddings as `varbinary(max)` (raw float[] bytes), `Text` capped at 2000, composite index `(SourceType, SourceId, Language)` matching the re-index diff query and a `ContentHash` index for the re-embedding short-circuit; the entity is deliberately hard-delete/derived data (documented in `AssistantContentChunk.cs`).
- **Impact:** Retrieval is a full-table scan with in-process cosine similarity — appropriate while chunk counts are small; there is no SQL Server vector index. Growth behavior is PERF-34's territory.
- **Recommendation:** Nothing now. If content volume grows, revisit with SQL Server 2025 `vector` type or an external index.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| DB-7 | Low | M | Wire the proven try/catch → 409 pattern into the remaining six admin controllers |
| DB-4 | Low | M | (Optional) split IPublishable/ISoftDeletable out of AuditableEntity; decide BlogArticle/AssessmentDefinition delete semantics |
| DB-9 | Low | S | Deduplicate the UserSubscriptions.UserId index declarations and fix the stale test-provider comments |
| DB-10 | Low | S | Remove redundant client-side LessonCompletion cascades; use ExecuteDelete for the required Restrict cleanups |
| DB-12 | Low | S | Drop redundant LessonCompletion.UserId index; review single-column bool indexes when plans exist |
| DB-13 | Info | S | (No action) Startup migrate/seed acceptable at this scale |
| DB-14 | Info | S | (No action) Assistant chunk storage appropriate; revisit if volume grows |

## 5. Related Findings Elsewhere

- **31 (API):** all nine Medium findings fixed, including the single ProblemDetails envelope shape/content-type and 409-for-conflicts semantics, which now pair with DB-7's concurrency-conflict mapping (fixed for Lesson, mechanical rollout remaining across the other six controllers).
- **25 (SEC):** ownership/authorization checks on data access (e.g. lesson-asset enrollment checks) and injection posture are SEC's; DB only asserts schema shape here.
- **28 (DQ):** validation of the JSON payload *contents* (AnswersJson/SchemaJson well-formedness, business rules — DQ-2/DQ-4, fixed), seed-data integrity, and any existing duplicate-category cleanup (DB-5's index is now the DB-level backstop).
- **34 (PERF):** query performance, N+1, and measurement of the index observations in DB-12/DB-14.
- **35 (SCALE) / 42 (OPS):** startup auto-migration races across multiple instances (DB-13).
- **29 (COMP):** former DB-11 (refresh-token accumulation) is fixed by COMP-5's `RefreshTokenPurgeService`, built for COMP-5's own "quick win" retention slice; user deletion is a hard delete with history cleanup (former DB-1, fixed).
- **22 (CQ):** stale/contradictory comments noted in DB-5/DB-9 are part of the broader comment-accuracy theme (DB-5's stale comment is now fixed along with the finding itself).
