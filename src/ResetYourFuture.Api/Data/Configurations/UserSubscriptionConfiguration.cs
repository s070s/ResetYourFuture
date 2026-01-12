using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Data.Configurations;

/// <summary>
/// EF Core configuration for UserSubscription entity.
/// </summary>
public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.HasKey(us => us.Id);

        // Relationship: UserSubscription belongs to a User
        builder.HasOne(us => us.User)
            .WithMany(u => u.UserSubscriptions)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship: UserSubscription references a SubscriptionPlan
        builder.HasOne(us => us.SubscriptionPlan)
            .WithMany(sp => sp.UserSubscriptions)
            .HasForeignKey(us => us.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index for querying active subscriptions
        builder.HasIndex(us => us.IsActive);

        // Index for querying by user
        builder.HasIndex(us => us.UserId);

        // Index for expiration queries (billing/renewal)
        builder.HasIndex(us => us.ExpiresAt);

        // Filtered unique index: only one active subscription per user
        // Note: This is enforced via application logic as SQLite doesn't support filtered indexes
        // For SQL Server, uncomment the following:
        // builder.HasIndex(us => new { us.UserId, us.IsActive })
        //     .HasFilter("[IsActive] = 1")
        //     .IsUnique();
    }
}
