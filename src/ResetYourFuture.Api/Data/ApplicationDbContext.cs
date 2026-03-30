using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Data;

/// <summary>
/// EF Core DbContext with ASP.NET Identity configured for ApplicationUser.
/// Includes core domain entities for the psychosocial career guidance platform.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext( DbContextOptions<ApplicationDbContext> options )
        : base( options )
    {
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
    }
}
