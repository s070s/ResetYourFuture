using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Identity;
using System.Linq.Expressions;
using System.Security.Claims;

namespace ResetYourFuture.Web.Data;

/// <summary>
/// EF Core DbContext with ASP.NET Identity configured for ApplicationUser.
/// Includes core domain entities for the psychosocial career guidance platform.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ApplicationDbContext( DbContextOptions<ApplicationDbContext> options ,
        IHttpContextAccessor? httpContextAccessor = null )
        : base( options )
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // --- Core Domain DbSets ---
    public DbSet<Course> Courses => Set<Course>();
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

    // --- Blog ---
    public DbSet<BlogArticle> BlogArticles => Set<BlogArticle>();

    // --- Testimonials ---
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();

    /// <summary>
    /// Register value converters that apply to all entities.
    /// SQLite cannot translate DateTimeOffset comparisons/ordering to SQL;
    /// storing as ISO 8601 strings makes ORDER BY work natively.
    /// </summary>
    protected override void ConfigureConventions( ModelConfigurationBuilder configurationBuilder )
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToStringConverter>();
        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<DateTimeOffsetToStringConverter>();
    }

    protected override void OnModelCreating( ModelBuilder builder )
    {
        base.OnModelCreating( builder );

        // Apply all entity configurations from this assembly
        builder.ApplyConfigurationsFromAssembly( typeof( ApplicationDbContext ).Assembly );

        // ApplicationUser configuration (Identity-specific)
        builder.Entity<ApplicationUser>( entity =>
        {
            // Ignore computed property
            entity.Ignore( u => u.Age );

            // Store DateOfBirth as DATE column
            entity.Property( u => u.DateOfBirth )
                  .HasConversion(
                      d => d.HasValue ? d.Value.ToDateTime( TimeOnly.MinValue ) : ( DateTime? ) null ,
                      d => d.HasValue ? DateOnly.FromDateTime( d.Value ) : null )
                  .HasColumnType( "date" );

            // Store Status as int
            entity.Property( u => u.Status )
                  .HasConversion<int>();

            entity.Property( u => u.FirstName ).HasMaxLength( 100 );
            entity.Property( u => u.LastName ).HasMaxLength( 100 );

                    // Indexes for server-side sorting performance
                        entity.HasIndex( u => u.Email ).HasDatabaseName( "IX_AspNetUsers_Email" );
                        entity.HasIndex( u => u.FirstName ).HasDatabaseName( "IX_AspNetUsers_FirstName" );
                        entity.HasIndex( u => u.LastName ).HasDatabaseName( "IX_AspNetUsers_LastName" );
                        entity.HasIndex( u => u.CreatedAt ).HasDatabaseName( "IX_AspNetUsers_CreatedAt" );
                    } );

                // Global soft-delete filter for all AuditableEntity subtypes
                foreach ( var entityType in builder.Model.GetEntityTypes() )
                {
                    if ( typeof( AuditableEntity ).IsAssignableFrom( entityType.ClrType ) )
                    {
                        var param = Expression.Parameter( entityType.ClrType, "e" );
                        var prop = Expression.Property( param, nameof( AuditableEntity.IsDeleted ) );
                        var filter = Expression.Lambda( Expression.Not( prop ), param );
                        builder.Entity( entityType.ClrType ).HasQueryFilter( filter );
                    }
                }

                // Matching soft-delete filters for dependent entities that reference AuditableEntity
                // principals. Without these, EF Core warns (10622) that the required principal may
                // be silently filtered out, producing unexpected results when navigations are loaded.
                builder.Entity<AssessmentSubmission>()
                    .HasQueryFilter( s => !s.AssessmentDefinition.IsDeleted );

                builder.Entity<Enrollment>()
                    .HasQueryFilter( e => !e.Course.IsDeleted );

                builder.Entity<Certificate>()
                    .HasQueryFilter( c => !c.Course.IsDeleted );

                builder.Entity<LessonCompletion>()
                    .HasQueryFilter( lc => !lc.Lesson.IsDeleted );
                }

                public override Task<int> SaveChangesAsync( CancellationToken cancellationToken = default )
                {
                    var currentUserId = _httpContextAccessor?.HttpContext?.User
                        .FindFirstValue( ClaimTypes.NameIdentifier );

                    var now = DateTimeOffset.UtcNow;

                    foreach ( var entry in ChangeTracker.Entries<AuditableEntity>() )
                    {
                        if ( entry.State == EntityState.Added )
                        {
                            entry.Entity.CreatedAt = now;
                            entry.Entity.CreatedByUserId ??= currentUserId;
                        }

                        if ( entry.State is EntityState.Added or EntityState.Modified )
                        {
                            entry.Entity.UpdatedAt = now;
                            entry.Entity.UpdatedByUserId ??= currentUserId;
                        }
                    }

                    return base.SaveChangesAsync( cancellationToken );
                }
            }
