using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Data.Configurations;

public class TestimonialConfiguration : IEntityTypeConfiguration<Testimonial>
{
    public void Configure( EntityTypeBuilder<Testimonial> builder )
    {
        builder.HasKey( t => t.Id );

        builder.Property( t => t.FullName )
            .IsRequired()
            .HasMaxLength( 150 );

        builder.Property( t => t.RoleOrTitle )
            .HasMaxLength( 150 );

        builder.Property( t => t.CompanyOrContext )
            .HasMaxLength( 150 );

        builder.Property( t => t.QuoteText )
            .IsRequired()
            .HasMaxLength( 1000 );

        builder.Property( t => t.AvatarPath )
            .HasMaxLength( 500 );

        builder.Property( t => t.IsActive )
            .HasDefaultValue( true );

        builder.HasIndex( t => new { t.IsActive, t.DisplayOrder } )
            .HasDatabaseName( "IX_Testimonials_IsActive_DisplayOrder" );
    }
}
