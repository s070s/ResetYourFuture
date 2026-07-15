using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

public class CourseReviewConfiguration : IEntityTypeConfiguration<CourseReview>
{
    public void Configure(EntityTypeBuilder<CourseReview> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Body)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.Status)
            .HasConversion<int>();

        // One review per student per course; also the natural lookup index for
        // "does this user already have a review on this course" and moderation queries.
        builder.HasIndex(r => new { r.CourseId, r.UserId })
            .IsUnique()
            .HasDatabaseName("IX_CourseReviews_CourseId_UserId");

        builder.HasIndex(r => new { r.CourseId, r.Status })
            .HasDatabaseName("IX_CourseReviews_CourseId_Status");

        // Optional (nullable CourseId) so EF uses LEFT JOIN when Course is soft-deleted (query
        // filter), preserving the review row instead of inner-joining it away.
        builder.HasOne(r => r.Course)
            .WithMany()
            .HasForeignKey(r => r.CourseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
