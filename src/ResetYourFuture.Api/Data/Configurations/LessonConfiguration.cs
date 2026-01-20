using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Data.Configurations;

/// <summary>
/// EF Core configuration for Lesson entity.
/// </summary>
public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(200);

        // Content can be large (rich text / markdown)
        builder.Property(l => l.Content)
            .HasMaxLength(50000);

        // File paths for PDF and video
        builder.Property(l => l.PdfPath)
            .HasMaxLength(500);

        builder.Property(l => l.VideoPath)
            .HasMaxLength(500);

        // Relationship: Lesson belongs to exactly one Module
        builder.HasOne(l => l.Module)
            .WithMany(m => m.Lessons)
            .HasForeignKey(l => l.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for ordered retrieval within a module
        builder.HasIndex(l => new { l.ModuleId, l.SortOrder });
    }
}
