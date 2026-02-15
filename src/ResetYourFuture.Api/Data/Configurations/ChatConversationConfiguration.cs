using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Data.Configurations;

/// <summary>
/// EF Core configuration for ChatConversation entity.
/// </summary>
public class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure( EntityTypeBuilder<ChatConversation> builder )
    {
        builder.HasKey( c => c.Id );

        builder.Property( c => c.LastMessageContent )
            .HasMaxLength( 500 );

        // Each conversation links to a creator and a participant.
        builder.HasOne( c => c.Creator )
            .WithMany()
            .HasForeignKey( c => c.CreatorId )
            .OnDelete( DeleteBehavior.Restrict );

        builder.HasOne( c => c.Participant )
            .WithMany()
            .HasForeignKey( c => c.ParticipantId )
            .OnDelete( DeleteBehavior.Restrict );

        // Unique constraint: one conversation per user pair (ordered).
        // The controller ensures CreatorId < ParticipantId lexicographically
        // so the same pair always maps to one row regardless of who starts it.
        builder.HasIndex( c => new { c.CreatorId , c.ParticipantId } )
            .IsUnique();

        // Index for listing conversations ordered by last activity.
        builder.HasIndex( c => c.LastMessageAt );
    }
}
