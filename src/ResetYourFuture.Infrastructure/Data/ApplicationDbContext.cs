using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Identity;
using System.Linq.Expressions;
using System.Security.Claims;

namespace ResetYourFuture.Infrastructure.Data;

/// <summary>
/// EF Core DbContext with ASP.NET Identity configured for ApplicationUser.
/// Includes core domain entities for the psychosocial career guidance platform.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext,
    Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    // Encrypts the special-category assessment answer/summary columns at rest (COMP-2). Null when
    // no DataProtection provider is injected (e.g. lightweight test contexts constructed directly),
    // in which case those columns are stored as plaintext. The provider is a DI singleton, so every
    // context in a given process makes the same choice — the built model stays consistent.
    private readonly EncryptedStringConverter? _sensitiveDataConverter;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
        IHttpContextAccessor? httpContextAccessor = null,
        IDataProtectionProvider? dataProtectionProvider = null)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _sensitiveDataConverter = dataProtectionProvider is null
            ? null
            : new EncryptedStringConverter(
                dataProtectionProvider.CreateProtector("ResetYourFuture.AssessmentSubmission.SensitiveData.v1"));
    }

    /// <summary>
    /// True when this context encrypts the special-category assessment columns at rest (COMP-2),
    /// i.e. a DataProtection provider was injected. Read by <see cref="EncryptionAwareModelCacheKeyFactory"/>
    /// so an encrypted and a plaintext context never share a cached model in the same process.
    /// </summary>
    public bool SensitiveDataEncryptionEnabled => _sensitiveDataConverter is not null;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, EncryptionAwareModelCacheKeyFactory>();
    }

    // --- Core Domain DbSets ---
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<AssessmentDefinition> AssessmentDefinitions => Set<AssessmentDefinition>();
    public DbSet<AssessmentSubmission> AssessmentSubmissions => Set<AssessmentSubmission>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<LessonCompletion> LessonCompletions => Set<LessonCompletion>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<BillingTransaction> BillingTransactions => Set<BillingTransaction>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<Certificate> Certificates => Set<Certificate>();

    // --- Chat ---
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // --- Calls ---
    public DbSet<CallSession> CallSessions => Set<CallSession>();
    public DbSet<CallParticipant> CallParticipants => Set<CallParticipant>();

    // --- Blog ---
    public DbSet<BlogArticle> BlogArticles => Set<BlogArticle>();

    // --- Testimonials ---
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();

    // --- AI Assistant ---
    public DbSet<AssistantContentChunk> AssistantContentChunks => Set<AssistantContentChunk>();

    // --- Notifications ---
    public DbSet<Notification> Notifications => Set<Notification>();

    // --- Course Reviews ---
    public DbSet<CourseReview> CourseReviews => Set<CourseReview>();

    // --- Learning Paths ---
    public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
    public DbSet<LearningPathStep> LearningPathSteps => Set<LearningPathStep>();

    // --- Scheduled Sessions ---
    public DbSet<ScheduledSession> ScheduledSessions => Set<ScheduledSession>();
    public DbSet<SessionRegistration> SessionRegistrations => Set<SessionRegistration>();

    // --- DataProtection keys (SCALE-4) ---
    // Shared DB-backed key ring instead of the local filesystem: keys survive a redeploy/container
    // rebuild and (unlike PersistKeysToFileSystem) are automatically visible to every instance
    // sharing this database. Required by IDataProtectionKeyContext.
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys => Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();

    /// <summary>
    /// Register value converters that apply to all entities. SQLite cannot translate
    /// DateTimeOffset comparisons/ordering to SQL, so under SQLite (used only by tests) these
    /// columns are stored as ISO-8601 strings to keep ORDER BY working. SQL Server has native
    /// <c>datetimeoffset</c> support and uses it — the string storage this convention used to
    /// force on every provider degraded index density, range scans, and type safety in the real
    /// (SQL Server) schema for a test-provider's benefit (DB-2 / TEST-5).
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToStringConverter>();
            configurationBuilder.Properties<DateTimeOffset?>()
                .HaveConversion<DateTimeOffsetToStringConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all entity configurations from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Encrypt the special-category assessment answers/summary at rest (COMP-2) when a
        // DataProtection provider is available. string -> string keeps the nvarchar column type,
        // so no migration is needed; these columns are never filtered/ordered/indexed on.
        if (_sensitiveDataConverter is not null)
        {
            builder.Entity<AssessmentSubmission>(entity =>
            {
                entity.Property(s => s.AnswersJson).HasConversion(_sensitiveDataConverter);
                entity.Property(s => s.SummaryJson).HasConversion(_sensitiveDataConverter);
            });
        }

        // ApplicationUser configuration (Identity-specific)
        builder.Entity<ApplicationUser>(entity =>
        {
            // Ignore computed property
            entity.Ignore(u => u.Age);

            // Store DateOfBirth as DATE column
            entity.Property(u => u.DateOfBirth)
                  .HasConversion(
                      d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                      d => d.HasValue ? DateOnly.FromDateTime(d.Value) : null)
                  .HasColumnType("date");

            // Store Status as int
            entity.Property(u => u.Status)
                  .HasConversion<int>();

            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);

            // Indexes for server-side sorting performance
            entity.HasIndex(u => u.Email).HasDatabaseName("IX_AspNetUsers_Email");
            entity.HasIndex(u => u.FirstName).HasDatabaseName("IX_AspNetUsers_FirstName");
            entity.HasIndex(u => u.LastName).HasDatabaseName("IX_AspNetUsers_LastName");
            entity.HasIndex(u => u.CreatedAt).HasDatabaseName("IX_AspNetUsers_CreatedAt");
        });

        // Global soft-delete filter for all AuditableEntity subtypes
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var param = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(param, nameof(AuditableEntity.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(prop), param);
                builder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }

        // Matching soft-delete filters for dependent entities that reference AuditableEntity
        // principals. Without these, EF Core warns (10622) that the required principal may
        // be silently filtered out, producing unexpected results when navigations are loaded.
        builder.Entity<AssessmentSubmission>()
            .HasQueryFilter(s => !s.AssessmentDefinition.IsDeleted);

        builder.Entity<Enrollment>()
            .HasQueryFilter(e => !e.Course.IsDeleted);

        builder.Entity<LessonCompletion>()
            .HasQueryFilter(lc => !lc.Lesson.IsDeleted);

        builder.Entity<LearningPathStep>()
            .HasQueryFilter(s => !s.Course!.IsDeleted);
    }

    // ---------------------------------------------------------------------------
    // Audit: stamp CreatedAt / UpdatedAt / *ByUserId on every save.
    // Called from both sync and async overrides to ensure neither path is missed.
    // ---------------------------------------------------------------------------

    private void ApplyAuditFields()
    {
        var currentUserId = _httpContextAccessor?.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedByUserId ??= currentUserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                // DB-3: was `??=`, a permanent no-op once CreatedByUserId's sibling stamp on
                // Added made UpdatedByUserId non-null from insertion onward — every later edit
                // kept recording the *creator*, not the actual last editor. Assign unconditionally
                // (falling back to the entity's existing value only if no user is in context, e.g.
                // a background job). UpdatedAt/UpdatedByUserId are also no longer stamped on Added,
                // so they stay null until the row is genuinely modified for the first time.
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedByUserId = currentUserId ?? entry.Entity.UpdatedByUserId;
            }
        }
    }

    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }
}
