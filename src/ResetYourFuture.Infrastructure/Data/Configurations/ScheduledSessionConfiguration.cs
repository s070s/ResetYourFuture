using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

public class ScheduledSessionConfiguration : IEntityTypeConfiguration<ScheduledSession>
{
    public void Configure(EntityTypeBuilder<ScheduledSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TitleEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.TitleEl)
            .HasMaxLength(200);

        builder.Property(s => s.Status)
            .HasConversion<int>();

        builder.HasIndex(s => s.StartsAtUtc);
        builder.HasIndex(s => s.Status);

        // Deleting the host is a genuine conflict (AdminUserService.DeleteUserAsync already
        // converts any DbUpdateException to 409) rather than silently orphaning the session.
        builder.HasOne(s => s.Host)
            .WithMany()
            .HasForeignKey(s => s.HostUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Course)
            .WithMany()
            .HasForeignKey(s => s.CourseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.CallSession)
            .WithMany()
            .HasForeignKey(s => s.CallSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
