using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

public class AssessmentDefinitionConfiguration : IEntityTypeConfiguration<AssessmentDefinition>
{
    public void Configure(EntityTypeBuilder<AssessmentDefinition> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(a => a.Key)
            .IsUnique();

        builder.HasIndex(a => a.IsPublished);

        builder.Property(a => a.TitleEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.TitleEl)
            .HasMaxLength(200);

        builder.Property(a => a.DescriptionEn)
            .HasMaxLength(1000);

        builder.Property(a => a.DescriptionEl)
            .HasMaxLength(1000);

        // DB-8: was unbounded nvarchar(max); capped generously for a hand-authored schema.
        builder.Property(a => a.SchemaJson)
            .IsRequired()
            .HasMaxLength(100_000);

        // RequiredTier stored as int
        builder.Property(a => a.RequiredTier)
            .HasConversion<int>();

        builder.HasMany(a => a.Submissions)
            .WithOne(s => s.AssessmentDefinition)
            .HasForeignKey(s => s.AssessmentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship: AssessmentDefinition optionally belongs to one Category
        builder.HasOne(a => a.Category)
            .WithMany(cat => cat.AssessmentDefinitions)
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.CategoryId);
    }
}
