using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for AssistantContentChunk entity.
/// </summary>
public class AssistantContentChunkConfiguration : IEntityTypeConfiguration<AssistantContentChunk>
{
    public void Configure(EntityTypeBuilder<AssistantContentChunk> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.SourceType)
            .HasConversion<int>();

        builder.Property(c => c.Language)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(c => c.Text)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.ContentHash)
            .IsRequired()
            .HasMaxLength(64);

        // Looked up together when diffing a source's chunk set on re-index.
        builder.HasIndex(c => new { c.SourceType, c.SourceId, c.Language });

        // Looked up to short-circuit re-embedding when a source's content hash is unchanged.
        builder.HasIndex(c => c.ContentHash);
    }
}
