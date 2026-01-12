using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Data.Configurations;

/// <summary>
/// EF Core configuration for Enrollment entity.
/// </summary>
public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.HasKey(e => e.Id);

        // Status stored as int
        builder.Property(e => e.Status)
            .HasConversion<int>();

        // Progress stored as JSON (flexible schema)
        builder.Property(e => e.ProgressJson);

        // Relationship: Enrollment belongs to a User
        builder.HasOne(e => e.User)
            .WithMany(u => u.Enrollments)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship: Enrollment belongs to a Course
        builder.HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: one enrollment per user per course
        builder.HasIndex(e => new { e.UserId, e.CourseId })
            .IsUnique();

        // Index for reporting by enrollment date
        builder.HasIndex(e => e.EnrolledAt);

        // Index for filtering by status
        builder.HasIndex(e => e.Status);
    }
}
