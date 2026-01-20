using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResetYourFuture.Api.Domain.Entities;

namespace ResetYourFuture.Api.Data.Configurations;

public class AssessmentSubmissionConfiguration : IEntityTypeConfiguration<AssessmentSubmission>
{
    public void Configure(EntityTypeBuilder<AssessmentSubmission> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId)
            .IsRequired();

        builder.Property(s => s.AnswersJson)
            .IsRequired();

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
