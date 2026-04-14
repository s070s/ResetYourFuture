using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Domain.Entities;

namespace ResetYourFuture.Web.Data;

public interface IApplicationDbContext
{
    DbSet<Course>                Courses                { get; }
    DbSet<Module>                Modules                { get; }
    DbSet<Lesson>                Lessons                { get; }
    DbSet<AssessmentDefinition>  AssessmentDefinitions  { get; }
    DbSet<AssessmentSubmission>  AssessmentSubmissions  { get; }
    DbSet<Enrollment>            Enrollments            { get; }
    DbSet<SubscriptionPlan>      SubscriptionPlans      { get; }
    DbSet<UserSubscription>      UserSubscriptions      { get; }
    DbSet<LessonCompletion>      LessonCompletions      { get; }
    DbSet<RefreshToken>          RefreshTokens          { get; }
    DbSet<BillingTransaction>    BillingTransactions    { get; }
    DbSet<SiteSetting>           SiteSettings           { get; }
    DbSet<Certificate>           Certificates           { get; }
    DbSet<ChatConversation>      ChatConversations      { get; }
    DbSet<ChatMessage>           ChatMessages           { get; }
    DbSet<BlogArticle>           BlogArticles           { get; }
    DbSet<Testimonial>           Testimonials           { get; }

    // Identity role tables used by ChatQueryService for role lookups
    DbSet<IdentityUserRole<string>> UserRoles           { get; }
    DbSet<IdentityRole>             Roles               { get; }

    Task<int> SaveChangesAsync( CancellationToken cancellationToken = default );
}
