using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Web.Domain.Entities;

namespace ResetYourFuture.Web.Data.Configurations;

/// <summary>
/// EF Core configuration for Course entity.
/// </summary>
public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TitleEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.TitleEl)
            .HasMaxLength(200);

        builder.Property(c => c.DescriptionEn)
            .HasMaxLength(2000);

        builder.Property(c => c.DescriptionEl)
            .HasMaxLength(2000);

        // RequiredTier stored as int
        builder.Property(c => c.RequiredTier)
            .HasConversion<int>();

        // Index on IsPublished for catalog queries
        builder.HasIndex(c => c.IsPublished);

        // Relationships configured in ModuleConfiguration (dependent side)
    }
}
