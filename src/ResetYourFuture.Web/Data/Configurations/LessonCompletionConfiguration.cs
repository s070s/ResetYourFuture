using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Web.Domain.Entities;

namespace ResetYourFuture.Web.Data.Configurations;

/// <summary>
/// EF Core configuration for LessonCompletion entity.
/// </summary>
public class LessonCompletionConfiguration : IEntityTypeConfiguration<LessonCompletion>
{
    public void Configure(EntityTypeBuilder<LessonCompletion> builder)
    {
        builder.HasKey(lc => lc.Id);

        // Relationship: LessonCompletion belongs to a User
        builder.HasOne(lc => lc.User)
            .WithMany()
            .HasForeignKey(lc => lc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship: LessonCompletion belongs to a Lesson
        builder.HasOne(lc => lc.Lesson)
            .WithMany()
            .HasForeignKey(lc => lc.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: one completion per user per lesson
        builder.HasIndex(lc => new { lc.UserId, lc.LessonId })
            .IsUnique();

        // Index for querying user's completions
        builder.HasIndex(lc => lc.UserId);
    }
}
