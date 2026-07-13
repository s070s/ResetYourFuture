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
| Medium | 6 |
| Low | 4 |
| Info | 2 |

> **Fixed since audit:** DB-1 (Critical — Restrict/NoAction FKs broke admin user deletion) — `DeleteUserAsync` now stages chat/call/certificate/enrollment cleanup in the change tracker so Identity's `SaveChanges` commits everything in one transaction, maps `DbUpdateException` → 409, and is covered by SQLite-backed FK tests (`AdminUserServiceTests`). DB-2 (High — all `DateTimeOffset` columns stored as `nvarchar(48)` because of a SQLite test workaround) — `ConfigureConventions` now applies the `DateTimeOffsetToStringConverter` only when the provider is SQLite, so SQL Server gets native `datetimeoffset`; migration `ConvertDateTimeOffsetToNativeType` converts all 43 columns and was verified applying cleanly to the seeded dev LocalDB (ISO-8601 strings convert without loss). This also closes TEST-5 (the workaround no longer dictates production storage) and PERF-2 (ordered/range date queries now run on the native type). SQLite-backed tests still pass because the converter remains active for that provider.

The schema is well above student-project average: every entity has an explicit `IEntityTypeConfiguration`, string lengths are mostly capped, uniqueness that matters (assessment key, blog slug, enrollment pair, certificate pair, one-active-subscription) is enforced with real DB indexes — including a correctly filtered unique index — and list-query indexes are deliberate and commented with their intended query shape. Migrations are clean, pinned to a tooling version, and applied consistently. `DateTimeOffset` columns are now native `datetimeoffset` on SQL Server (DB-2, fixed); the remaining problems are that cross-cutting policies — soft-delete, audit stamping, temporal types, concurrency — are each applied to only part of the entity set.

## 3. Findings

### DB-3: Audit stamping bug — UpdatedByUserId is never updated after the first write  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Infrastructure/Data/ApplicationDbContext.cs:152-156` — for `Added or Modified` entries: `entry.Entity.UpdatedByUserId ??= currentUserId;`. Because the property is also stamped on `Added`, it is non-null from insertion onward, so `??=` is a permanent no-op on every subsequent modification: the column records the *first* writer forever, not the last. `UpdatedAt` (line 154) is also set on `Added`, so freshly created rows look "updated". Several controllers mask this by assigning `UpdatedByUserId = UserId` manually (`AdminLessonsController.cs:495`, `AdminModulesController`, `AdminAssessmentsController`), while pure-service paths (e.g. `AdminCategoryService.UpdateCategoryAsync`) rely on the broken interceptor and keep stale attribution.
- **Impact:** The audit trail silently lies: "last updated by" shows the creator regardless of who edited. Manual per-controller stamping means two competing mechanisms with different results.
- **Recommendation:** In `ApplyAuditFields`, assign unconditionally on `Modified` (`entry.Entity.UpdatedByUserId = currentUserId ?? entry.Entity.UpdatedByUserId;`), leave `UpdatedAt`/`UpdatedByUserId` null on `Added`, and delete the redundant manual `UpdatedAt`/`UpdatedByUserId` assignments in controllers/services so the DbContext is the single owner.

### DB-4: Soft-delete design applied inconsistently across AuditableEntity subtypes  [Medium] [Effort: M]
- **Evidence:** Every `AuditableEntity` gets `IsDeleted` columns and a global query filter (`ApplicationDbContext.cs:107-117`), but the actual delete paths diverge: Course and Category are soft-deleted (`AdminCourseService.cs:126`, `AdminCategoryService.cs:106`), while BlogArticle (`BlogArticleService.cs:230`), Module and Lesson (`AdminModulesController.cs`, `AdminLessonsController.cs` — `Remove(...)`), and AssessmentDefinition (`AdminAssessmentsController.cs` delete action) are hard-deleted. Soft-deleting a Course does not touch its Modules/Lessons (`AdminCourseService.cs:114-136`), so they stay `IsDeleted=false` and remain fully readable via `GET /api/admin/modules/course/{courseId}` and the lesson endpoints. `Category.cs` documents that `IsPublished` is unused for categories.
- **Impact:** No single answer to "what does delete mean" per aggregate; children of deleted courses linger as live rows (admin UIs can still edit lessons of a deleted course); dead schema columns (`IsPublished`/`PublishedAt` on Category, `IsDeleted`/`DeletedAt` on BlogArticle) suggest behavior that doesn't exist. Certificate/enrollment retention only works for the soft-deleted entities.
- **Recommendation:** Decide per aggregate root and make children follow: course soft-delete should stamp `IsDeleted` on its Modules and Lessons (single `ExecuteUpdate` or tracked loop, same transaction); either soft-delete BlogArticle/AssessmentDefinition too (they have submissions/history worth keeping) or move publish/soft-delete fields out of `AuditableEntity` into interfaces (`IPublishable`, `ISoftDeletable`) so entities only carry columns they use.

### DB-5: Category name uniqueness enforced only in the service layer despite an in-repo filtered-index precedent  [Medium] [Effort: S]
- **Evidence:** `CategoryConfiguration.cs` — non-unique index on `NameEn` with a comment that a filtered unique index "would require provider-specific SQL that breaks the SQLite test provider", so uniqueness is checked case-insensitively in `AdminCategoryService.NameExistsAsync` (`AdminCategoryService.cs:150-156`) and in the get-or-create helper `ResolveCategoryAsync` (`AdminCategoryService.cs:125-148`). But `UserSubscriptionConfiguration.cs:37-42` already ships a `HasFilter("[IsActive] = 1")` unique index, and the SQLite test factory (`tests/ResetYourFuture.TestSupport/DbContextFactory.cs:32-45`) runs `EnsureCreated` against the same model — SQLite supports partial indexes with this exact syntax, so the stated blocker doesn't hold.
- **Impact:** Check-then-insert races can create duplicate category names (two admins, or an admin save that uses `NewCategoryName` concurrently with another); nothing at the DB level prevents it. The two documented rationales contradict each other, which will confuse the next person adding a constraint.
- **Recommendation:** Add `HasIndex(c => c.NameEn).IsUnique().HasFilter("[IsDeleted] = 0")` (mirroring the UserSubscription pattern); for case-insensitivity rely on the database's CI collation (SQL Server default) and keep the service check for friendly error messages. DQ (28) owns the data-cleanup side if duplicates already exist.

### DB-6: Mixed DateTime / DateTimeOffset usage across entities with no Kind enforcement  [Medium] [Effort: S]
- **Evidence:** `DateTimeOffset` group: `AuditableEntity.cs`, `RefreshToken.cs`, `SiteSetting.cs`, `Testimonial.cs`, `AssessmentSubmission.SubmittedAt`. `DateTime` group: `Enrollment.cs`, `LessonCompletion.cs`, `Certificate.cs` (IssuedAt/RevokedAt), `BillingTransaction.cs`, `SubscriptionPlan.cs`, `UserSubscription.cs`, `ChatConversation.cs`, `ChatMessage.cs`, `CallSession.cs`, `CallParticipant.cs`, `ApplicationUser.cs` (CreatedAt/LastSeenAt/GdprConsentDate). All initialized with `*.UtcNow` by convention, but nothing (converter, `HasConversion`, or value comparer) pins `DateTimeKind.Utc` on materialization for the `DateTime` group — EF materializes them as `Kind=Unspecified`.
- **Impact:** Two timestamp philosophies in one schema; `Kind=Unspecified` values round-trip fine until someone calls `.ToLocalTime()`/`.ToUniversalTime()` or serializes them expecting a `Z` suffix — DTOs already expose both types (`AdminTestimonialDto` uses `DateTimeOffset`, `CertificateDto` uses `DateTime`).
- **Recommendation:** Standardize on UTC `DateTime` + a model-wide `UtcDateTimeConverter` (sets Kind on read), or on `DateTimeOffset` (now native on SQL Server after DB-2). A follow-up migration would consolidate the `DateTime` group; DB-2 already converted the `DateTimeOffset` group.

### DB-7: No optimistic concurrency control on any domain entity  [Medium] [Effort: M]
- **Evidence:** No `IsRowVersion()`/`[Timestamp]`/`IsConcurrencyToken()` anywhere in `src/ResetYourFuture.Infrastructure/Data/Configurations/` (only Identity's built-in `ConcurrencyStamp` on users/roles). All admin edit flows are read-modify-write over separate requests (e.g. `AdminLessonsController.UpdateLesson`, `AdminCourseService.UpdateCourseAsync`, testimonial `MoveUp`/`MoveDown` ordering swaps).
- **Impact:** Concurrent admin edits are silent last-write-wins: two admins editing the same lesson overwrite each other with no warning; interleaved `move-up`/`move-down` calls can produce duplicate `DisplayOrder` values. Low probability with one admin seeded, but the platform explicitly supports multiple admins.
- **Recommendation:** Add a `rowversion` column via the base configuration for admin-edited aggregates (Course, Module, Lesson, AssessmentDefinition, BlogArticle, Testimonial, Category), thread it through the Save*Request DTOs, and map `DbUpdateConcurrencyException` to 409 (pairs with API-3's Conflict work).

### DB-8: Unbounded JSON/string blob columns with no schema-level caps  [Medium] [Effort: S]
- **Evidence:** `nvarchar(max)` with no length limit: `AssessmentDefinition.SchemaJson` (required), `AssessmentSubmission.AnswersJson`/`SummaryJson` (student-writable via `POST /api/assessments/{id}/submit`), `Enrollment.ProgressJson`, `SubscriptionPlan.FeaturesJson`, `BlogArticle.Tags` (JSON-serialized array in a string), `ApplicationUser.DisplayName`/`AvatarPath` (snapshot:1193-1207) — while sibling fields are carefully capped (FirstName/LastName 100, Lesson content 50000). DTO caps also drift from column caps: `SaveCourseRequest` limits descriptions to 1000 (`AdminDtos.cs:62-63`) but `CourseConfiguration.cs` allows 2000.
- **Impact:** A student can submit a multi-megabyte `AnswersJson` bounded only by Kestrel/MVC body limits; blob growth hits backup size and buffer pool. The JSON is opaque to the DB (no `ISJSON` constraint, no EF `ToJson()` mapping), so malformed JSON is storable. (Content-level validation of these payloads is DQ-28's finding; the missing schema caps/typing are the DB-side gap.)
- **Recommendation:** Add `HasMaxLength` caps sized to real content (e.g. 64–256 KB for AnswersJson/SchemaJson, 200 for DisplayName, 500 for AvatarPath), align DTO `[MaxLength]` with column caps, and consider EF Core owned-entity `ToJson()` mapping for `FeaturesJson`/`Tags` so the shape is typed.

### DB-9: The plain UserSubscriptions.UserId index was silently replaced by the filtered unique index  [Low] [Effort: S]
- **Evidence:** `UserSubscriptionConfiguration.cs` declares both `HasIndex(us => us.UserId)` ("Index for querying by user") and, later, `HasIndex(us => us.UserId).HasFilter("[IsActive] = 1").IsUnique()...` — EF treats a second `HasIndex` on the same property set as the same index, so only the filtered unique one exists: snapshot `ApplicationDbContextModelSnapshot.cs:1177-1180` shows a single UserId index, and migration `20260619145323_AddActiveSubscriptionUniqueIndex.cs:13-15` explicitly dropped `IX_UserSubscriptions_UserId`. The config comment also claims the test DbContext "skips this index via a separate configuration" — no such configuration exists (`tests/ResetYourFuture.TestSupport/DbContextFactory.cs` uses `ApplicationDbContext` directly; SQLite creates the partial index fine).
- **Impact:** Queries over a user's *inactive* subscriptions (history) can't seek on the filtered index; today's queries all filter `IsActive` so the practical cost is near zero, but the config no longer says what the schema does, and the stale comment misleads.
- **Recommendation:** Either delete the first `HasIndex(us => us.UserId)` call and fix the comments, or give it a distinct `HasDatabaseName` if unfiltered user lookups are wanted. Update the stale test-provider comment in both `UserSubscriptionConfiguration.cs` and `CategoryConfiguration.cs` (see DB-5).

### DB-10: Delete paths mix redundant client-side cascades with genuinely required ones  [Low] [Effort: S]
- **Evidence:** `AdminModulesController.cs` `DeleteModule` and `AdminLessonsController.cs` `DeleteLesson` load `LessonCompletions` into memory and `RemoveRange` them "before deleting (FK constraint)" — but `LessonCompletionConfiguration.cs` sets `OnDelete(Cascade)` from Lesson, so the DB does this itself; the manual pass just adds round-trips and memory. Conversely, `AdminAssessmentsController.cs` `DeleteAssessment` removing `Submissions` first *is* required (`AssessmentDefinitionConfiguration.cs:45` is Restrict). The comments show the cascade model isn't understood consistently — the same misunderstanding that produced DB-1.
- **Impact:** Harmless today but noisy: entity loads purely to delete, misleading comments, and no single pattern to copy when adding the next delete endpoint.
- **Recommendation:** Rely on DB cascade where configured (drop the manual LessonCompletion removal); where client-side deletion is required (Restrict), prefer `ExecuteDeleteAsync` over load-then-RemoveRange. Document each aggregate's delete strategy next to its configuration.

### DB-11: RefreshTokens table grows without bound — no purge of expired/revoked/rotated rows  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Application/ApiServices/AuthApiService.cs:156-165` inserts a row per login; `RefreshAsync` (lines 202-216) revokes the old row and inserts a new one, keeping both (rotation chain via `ReplacedByTokenId`). No code anywhere deletes `RefreshToken` rows (only the user-delete cascade), and there is no cleanup job among the hosted services registered in `ServiceRegistrationExtensions.cs`.
- **Impact:** One row per login/refresh forever; with the bulk student seeder and e2e runs this becomes the largest table for no benefit. Lookup is by indexed `TokenHash` so correctness is unaffected — it's pure accumulation.
- **Recommendation:** Add a small hosted-service sweep (`ExecuteDeleteAsync` where `ExpiresAt < now - grace` or `RevokedAt` older than the audit window), or purge a user's expired tokens inline at login. Retention duration is a COMP (29) decision; the mechanism is DB's.

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
| DB-3 | Medium | S | Fix `ApplyAuditFields` to stamp UpdatedByUserId unconditionally on Modified; remove per-controller manual stamping |
| DB-5 | Medium | S | Add a filtered unique index on Category.NameEn mirroring the UserSubscription pattern |
| DB-6 | Medium | S | Standardize on one temporal type + UTC Kind enforcement (bundle with DB-2 migration) |
| DB-8 | Medium | S | Cap the unbounded JSON/string columns and align DTO MaxLength with column lengths |
| DB-4 | Medium | M | Define delete semantics per aggregate; cascade Course soft-delete to Modules/Lessons; split IPublishable/ISoftDeletable out of AuditableEntity |
| DB-7 | Medium | M | Add rowversion concurrency tokens to admin-edited aggregates and map conflicts to 409 |
| DB-9 | Low | S | Deduplicate the UserSubscriptions.UserId index declarations and fix the stale test-provider comments |
| DB-10 | Low | S | Remove redundant client-side LessonCompletion cascades; use ExecuteDelete for the required Restrict cleanups |
| DB-11 | Low | S | Purge expired/revoked refresh tokens (hosted sweep or on-login cleanup) |
| DB-12 | Low | S | Drop redundant LessonCompletion.UserId index; review single-column bool indexes when plans exist |
| DB-13 | Info | S | (No action) Startup migrate/seed acceptable at this scale |
| DB-14 | Info | S | (No action) Assistant chunk storage appropriate; revisit if volume grows |

## 5. Related Findings Elsewhere

- **31 (API):** API-1 (error envelope) covers the single-envelope response shape; API-3 (missing Conflict semantics) pairs with DB-7's concurrency-conflict mapping.
- **25 (SEC):** ownership/authorization checks on data access (e.g. lesson-asset enrollment checks) and injection posture are SEC's; DB only asserts schema shape here.
- **28 (DQ):** validation of the JSON payload *contents* (AnswersJson/SchemaJson well-formedness, business rules), seed-data integrity, and any existing duplicate-category cleanup.
- **34 (PERF):** query performance, N+1, and measurement of the index observations in DB-12/DB-14.
- **35 (SCALE) / 42 (OPS):** startup auto-migration races across multiple instances (DB-13).
- **29 (COMP):** GDPR retention windows for DB-11 (token retention); user deletion is now a hard delete with history cleanup (former DB-1, fixed).
- **22 (CQ):** stale/contradictory comments noted in DB-5/DB-9 are part of the broader comment-accuracy theme.
