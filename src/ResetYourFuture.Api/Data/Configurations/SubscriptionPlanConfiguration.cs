using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Data.Configurations;

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

        // Features stored as JSON (flexible schema for limits/flags)
        // Leave provider-specific column type unspecified so EF maps to the
        // appropriate database type (TEXT for SQLite, nvarchar(max) for SQL Server).
        builder.Property(sp => sp.FeaturesJson);

        // Index for querying active plans
        builder.HasIndex(sp => sp.IsActive);

        // Unique name for plans
        builder.HasIndex(sp => sp.Name)
            .IsUnique();
    }
}
