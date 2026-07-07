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

        // Not a unique DB index: uniqueness is enforced case-insensitively at the
        // service layer (see AdminCategoryService), since a filtered unique index
        // (excluding soft-deleted rows) would require provider-specific SQL that
        // breaks the SQLite test provider.
        builder.HasIndex(c => c.NameEn);

        // Relationships configured in CourseConfiguration/AssessmentDefinitionConfiguration (dependent side)
    }
}
