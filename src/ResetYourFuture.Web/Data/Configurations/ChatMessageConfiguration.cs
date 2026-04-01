using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Web.Domain.Entities;

namespace ResetYourFuture.Web.Data.Configurations;

/// <summary>
/// EF Core configuration for ChatMessage entity.
/// </summary>
public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure( EntityTypeBuilder<ChatMessage> builder )
    {
        builder.HasKey( m => m.Id );

        builder.Property( m => m.Content )
            .IsRequired()
            .HasMaxLength( 4000 );

        builder.HasOne( m => m.Conversation )
            .WithMany( c => c.Messages )
            .HasForeignKey( m => m.ConversationId )
            .OnDelete( DeleteBehavior.Cascade );

        builder.HasOne( m => m.Sender )
            .WithMany()
            .HasForeignKey( m => m.SenderId )
            .OnDelete( DeleteBehavior.Restrict );

        // Index for loading messages in chronological order.
        builder.HasIndex( m => new { m.ConversationId , m.SentAt } );

        // Index for unread message queries.
        builder.HasIndex( m => new { m.ConversationId , m.IsRead } );
    }
}
