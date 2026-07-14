using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Infrastructure.Data.Configurations;

public class AssessmentSubmissionConfiguration : IEntityTypeConfiguration<AssessmentSubmission>
{
    public void Configure(EntityTypeBuilder<AssessmentSubmission> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId)
            .IsRequired();

        // DB-8: was unbounded nvarchar(max). These columns are encrypted at rest (COMP-2,
        // EncryptedStringConverter) — the stored value is ciphertext, not the plaintext DTO
        // measures, and ciphertext is *larger* than plaintext (Data Protection's base64 envelope
        // measured empirically at up to ~2.7x for all-Greek/2-byte-UTF8 input, vs ~1.34x for
        // ASCII). Column caps are sized for worst-case ciphertext of SubmitAssessmentRequest's
        // DTO MaxLength (50_000 / 20_000 plaintext chars) plus headroom, not the plaintext
        // length itself — sizing the column to the plaintext DTO cap directly would truncate
        // every real submission.
        builder.Property(s => s.AnswersJson)
            .IsRequired()
            .HasMaxLength(150_000);

        builder.Property(s => s.SummaryJson)
            .HasMaxLength(60_000);

        builder.HasIndex(s => new { s.UserId, s.SubmittedAt })
            .IsDescending(false, true); // DESC on SubmittedAt

        builder.HasIndex(s => s.AssessmentDefinitionId);

        builder.HasOne(s => s.User)
            .WithMany(u => u.AssessmentSubmissions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.AssessmentDefinition)
            .WithMany(a => a.Submissions)
            .HasForeignKey(s => s.AssessmentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
