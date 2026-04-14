using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Web.Domain.Entities;

namespace ResetYourFuture.Web.Data.Configurations;

/// <summary>
/// EF Core configuration for BillingTransaction entity.
/// </summary>
public class BillingTransactionConfiguration : IEntityTypeConfiguration<BillingTransaction>
{
    public void Configure(EntityTypeBuilder<BillingTransaction> builder)
    {
        builder.HasKey(bt => bt.Id);

        builder.Property(bt => bt.Amount)
            .HasPrecision(18, 2);

        builder.Property(bt => bt.Currency)
            .HasMaxLength(3)
            .HasDefaultValue("EUR");

        builder.Property(bt => bt.Type)
            .HasConversion<int>();

        builder.Property(bt => bt.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(bt => bt.StripeSessionId)
            .HasMaxLength(200);

        builder.HasOne(bt => bt.User)
            .WithMany()
            .HasForeignKey(bt => bt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bt => bt.SubscriptionPlan)
            .WithMany()
            .HasForeignKey(bt => bt.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(bt => bt.UserId);
        builder.HasIndex(bt => bt.CreatedAt);
    }
}
