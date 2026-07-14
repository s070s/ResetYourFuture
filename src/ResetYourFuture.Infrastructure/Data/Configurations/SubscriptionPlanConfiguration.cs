using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for SubscriptionPlan entity.
/// </summary>
public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.HasKey(sp => sp.Id);

        builder.Property(sp => sp.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sp => sp.Description)
            .HasMaxLength(1000);

        // Price with precision for currency
        builder.Property(sp => sp.Price)
            .HasPrecision(18, 2);

        // BillingPeriod stored as int
        builder.Property(sp => sp.BillingPeriod)
            .HasConversion<int>();

        // Tier stored as int
        builder.Property(sp => sp.Tier)
            .HasConversion<int>();

        // Features stored as JSON (flexible schema for limits/flags). DB-8: capped — only ever
        // written by SubscriptionPlanSeeder from a fixed, small PlanFeaturesDto, so 2000 chars
        // leaves generous headroom without being unbounded.
        builder.Property(sp => sp.FeaturesJson)
            .HasMaxLength(2_000);

        // Index for querying active plans
        builder.HasIndex(sp => sp.IsActive);

        // Unique name for plans
        builder.HasIndex(sp => sp.Name)
            .IsUnique();
    }
}
