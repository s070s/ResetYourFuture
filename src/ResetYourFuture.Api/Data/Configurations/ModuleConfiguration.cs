using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Data.Configurations;

/// <summary>
/// EF Core configuration for Module entity.
/// </summary>
public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.TitleEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.TitleEl)
            .HasMaxLength(200);

        builder.Property(m => m.DescriptionEn)
            .HasMaxLength(2000);

        builder.Property(m => m.DescriptionEl)
            .HasMaxLength(2000);

        // Relationship: Module belongs to exactly one Course
        builder.HasOne(m => m.Course)
            .WithMany(c => c.Modules)
            .HasForeignKey(m => m.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for ordered retrieval within a course
        builder.HasIndex(m => new { m.CourseId, m.SortOrder });
    }
}
