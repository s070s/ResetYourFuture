using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

public class SessionRegistrationConfiguration : IEntityTypeConfiguration<SessionRegistration>
{
    public void Configure(EntityTypeBuilder<SessionRegistration> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasIndex(r => new { r.SessionId, r.UserId })
            .IsUnique()
            .HasDatabaseName("IX_SessionRegistrations_SessionId_UserId");

        // Two independent parents (Session + User), same proven-safe shape as Enrollment/CourseReview.
        builder.HasOne(r => r.Session)
            .WithMany(s => s.Registrations)
            .HasForeignKey(r => r.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
