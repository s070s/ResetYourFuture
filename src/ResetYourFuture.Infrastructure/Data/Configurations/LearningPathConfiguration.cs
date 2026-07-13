using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

public class LearningPathConfiguration : IEntityTypeConfiguration<LearningPath>
{
    public void Configure(EntityTypeBuilder<LearningPath> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TitleEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.TitleEl)
            .HasMaxLength(200);

        builder.Property(p => p.DescriptionEn)
            .HasMaxLength(2000);

        builder.Property(p => p.DescriptionEl)
            .HasMaxLength(2000);

        builder.HasIndex(p => p.IsPublished);

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
