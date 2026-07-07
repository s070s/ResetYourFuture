using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for CallSession entity.
/// </summary>
public class CallSessionConfiguration : IEntityTypeConfiguration<CallSession>
{
    public void Configure(EntityTypeBuilder<CallSession> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.EndReason)
            .HasConversion<int>();

        builder.HasOne(c => c.Initiator)
            .WithMany()
            .HasForeignKey(c => c.InitiatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1:1 calls reference their originating conversation; the call history survives
        // conversation deletion (FK is nulled rather than cascaded).
        builder.HasOne(c => c.Conversation)
            .WithMany()
            .HasForeignKey(c => c.ConversationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Participants)
            .WithOne(p => p.CallSession)
            .HasForeignKey(p => p.CallSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for a user's call history ordered by recency.
        builder.HasIndex(c => new { c.InitiatorId, c.StartedAt });

        // Index for the ring-monitor / dangling-session sweep queries.
        builder.HasIndex(c => c.EndedAt);
    }
}
