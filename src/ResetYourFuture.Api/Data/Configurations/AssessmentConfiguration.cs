using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Data.Configurations;

/// <summary>
/// EF Core configuration for Assessment entity.
/// </summary>
public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Description)
            .HasMaxLength(2000);

        // Results stored as JSON (flexible schema)
        builder.Property(a => a.ResultsJson);

        // Relationship: Assessment belongs to a User (required)
        builder.HasOne(a => a.User)
            .WithMany(u => u.Assessments)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional relationship to Course
        builder.HasOne(a => a.Course)
            .WithMany()
            .HasForeignKey(a => a.CourseId)
            .OnDelete(DeleteBehavior.SetNull);

        // Optional relationship to Module
        builder.HasOne(a => a.Module)
            .WithMany()
            .HasForeignKey(a => a.ModuleId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for querying assessments by user
        builder.HasIndex(a => a.UserId);

        // Index for analytics by completion date
        builder.HasIndex(a => a.CompletedAt);
    }
}
