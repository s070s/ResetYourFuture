using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Data.Configurations;

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

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Description)
            .HasMaxLength(1000);

        builder.Property(a => a.SchemaJson)
            .IsRequired();

        builder.HasMany(a => a.Submissions)
            .WithOne(s => s.AssessmentDefinition)
            .HasForeignKey(s => s.AssessmentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
