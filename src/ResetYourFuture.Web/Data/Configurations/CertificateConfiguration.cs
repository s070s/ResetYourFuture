using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Web.Domain.Entities;

namespace ResetYourFuture.Web.Data.Configurations;

/// <summary>
/// EF Core configuration for Certificate entity.
/// </summary>
public class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure( EntityTypeBuilder<Certificate> builder )
    {
        builder.HasKey( c => c.Id );

        builder.Property( c => c.RecipientName )
            .IsRequired()
            .HasMaxLength( 256 );

        builder.Property( c => c.CourseTitleEn )
            .IsRequired()
            .HasMaxLength( 512 );

        builder.Property( c => c.CourseTitleEl )
            .HasMaxLength( 512 );

        builder.Property( c => c.PdfPath )
            .HasMaxLength( 1024 );

        // Status stored as int; consistent with EnrollmentStatus convention
        builder.Property( c => c.Status )
            .HasConversion<int>();

        // Public verification lookup — must be unique and fast
        builder.HasIndex( c => c.VerificationId )
            .IsUnique();

        // Enforce one certificate per user per course at the DB level
        builder.HasIndex( c => new { c.UserId, c.CourseId } )
            .IsUnique();

        // Reporting indexes
        builder.HasIndex( c => c.IssuedAt );
        builder.HasIndex( c => c.Status );

        // Relationship: Certificate belongs to a User (cascade: deleting the user removes the certificate)
        builder.HasOne( c => c.User )
            .WithMany()
            .HasForeignKey( c => c.UserId )
            .OnDelete( DeleteBehavior.Cascade );

        // Relationship: Certificate references the Enrollment that triggered it
        // NoAction avoids a multiple-cascade-path conflict on SQL Server
        // (Enrollment already cascades from User via EnrollmentConfiguration)
        builder.HasOne( c => c.Enrollment )
            .WithMany()
            .HasForeignKey( c => c.EnrollmentId )
            .OnDelete( DeleteBehavior.NoAction );

        // Relationship: Certificate references the Course
        // NoAction for the same reason — Course already cascades to Enrollment
        builder.HasOne( c => c.Course )
            .WithMany()
            .HasForeignKey( c => c.CourseId )
            .OnDelete( DeleteBehavior.NoAction );
    }
}
