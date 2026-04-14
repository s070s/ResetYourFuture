using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Web.Domain.Entities;

namespace ResetYourFuture.Web.Data.Configurations;

public class BlogArticleConfiguration : IEntityTypeConfiguration<BlogArticle>
{
    public void Configure( EntityTypeBuilder<BlogArticle> builder )
    {
        builder.HasKey( a => a.Id );

        builder.Property( a => a.TitleEn )
            .IsRequired()
            .HasMaxLength( 200 );

        builder.Property( a => a.TitleEl )
            .HasMaxLength( 200 );

        builder.Property( a => a.Slug )
            .IsRequired()
            .HasMaxLength( 220 );

        builder.Property( a => a.SummaryEn )
            .IsRequired()
            .HasMaxLength( 500 );

        builder.Property( a => a.SummaryEl )
            .HasMaxLength( 500 );

        builder.Property( a => a.ContentEn )
            .IsRequired();

        // ContentEl has no DB length cap

        builder.Property( a => a.CoverImageUrl )
            .HasMaxLength( 500 );

        builder.Property( a => a.AuthorName )
            .IsRequired()
            .HasMaxLength( 100 );

        builder.Property( a => a.IsPublished )
            .HasDefaultValue( false );

        builder.HasIndex( a => a.Slug )
            .IsUnique()
            .HasDatabaseName( "IX_BlogArticles_Slug" );

        builder.HasIndex( a => new { a.IsPublished, a.PublishedAt } )
            .HasDatabaseName( "IX_BlogArticles_Published_PublishedAt" );
    }
}
