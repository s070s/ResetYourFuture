using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for Category entity.
/// </summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.NameEn)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.NameEl)
            .HasMaxLength(100);

        // DB-5: filtered unique index excluding soft-deleted rows, mirroring
        // UserSubscriptionConfiguration's IsActive-filtered index — SQLite (the test provider)
        // supports this same HasFilter syntax fine, so the earlier "breaks SQLite" rationale for
        // relying on the service-layer check alone didn't hold. Case-insensitivity comes from
        // SQL Server's default CI collation; AdminCategoryService's check stays for a friendly
        // error message instead of a raw DbUpdateException.
        builder.HasIndex(c => c.NameEn)
            .HasFilter("[IsDeleted] = 0")
            .IsUnique()
            .HasDatabaseName("IX_Categories_NameEn_Unique");

        // Relationships configured in CourseConfiguration/AssessmentDefinitionConfiguration (dependent side)
    }
}
