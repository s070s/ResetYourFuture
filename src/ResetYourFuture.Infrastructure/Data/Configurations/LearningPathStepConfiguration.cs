using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

public class LearningPathStepConfiguration : IEntityTypeConfiguration<LearningPathStep>
{
    public void Configure(EntityTypeBuilder<LearningPathStep> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.LearningPathId, s.StepOrder })
            .IsUnique()
            .HasDatabaseName("IX_LearningPathSteps_LearningPathId_StepOrder");

        builder.HasOne(s => s.LearningPath)
            .WithMany(p => p.Steps)
            .HasForeignKey(s => s.LearningPathId)
            .OnDelete(DeleteBehavior.Cascade);

        // Courses are only ever soft-deleted (AdminCourseService.DeleteCourseAsync), so this
        // Restrict never actually fires in practice — it documents intent rather than guarding
        // a real hard-delete path.
        builder.HasOne(s => s.Course)
            .WithMany()
            .HasForeignKey(s => s.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
