using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Shared.Resources.Messages;

namespace ResetYourFuture.Application.ApiServices;

public class ProfileService(
    UserManager<ApplicationUser> userManager,
    IFileStorage fileStorage,
    IApplicationDbContext db,
    ILogger<ProfileService> logger) : IProfileService
{
    public async Task<ProfileDto?> GetProfileAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : ToDto(user);
    }

    public async Task<ServiceResult<ProfileDto>> UpdateProfileAsync(string userId, UpdateProfileRequest request)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return ServiceResult<ProfileDto>.NotFound();

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.DisplayName = request.DisplayName?.Trim();
        user.DateOfBirth = request.DateOfBirth;
        user.Status = request.Status == UserStatus.Unknown ? UserStatus.Student : request.Status;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return ServiceResult<ProfileDto>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        return ServiceResult<ProfileDto>.Ok(ToDto(user));
    }

    public async Task<ServiceResult<AvatarUploadResultDto>> UploadAvatarAsync(string userId, Stream fileStream, string fileName)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return ServiceResult<AvatarUploadResultDto>.NotFound();

        // Delete old avatar if exists
        if (!string.IsNullOrEmpty(user.AvatarPath))
        {
            await fileStorage.DeleteFileAsync(user.AvatarPath);
        }

        // Save new avatar
        var path = await fileStorage.SaveFileAsync(fileStream, fileName, "avatars");

        user.AvatarPath = path;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            logger.LogError("Failed to persist avatar path for user {UserId}: {Errors}",
                userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            return new ServiceResult<AvatarUploadResultDto>(null, StatusCodes.Status500InternalServerError, ErrorMessagesRes.FailedToSaveAvatar);
        }

        return ServiceResult<AvatarUploadResultDto>.Ok(new AvatarUploadResultDto(path));
    }

    public async Task<(Stream Stream, string ContentType)?> GetAvatarAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || string.IsNullOrEmpty(user.AvatarPath))
            return null;

        if (!fileStorage.FileExists(user.AvatarPath))
            return null;

        return await fileStorage.GetFileAsync(user.AvatarPath);
    }

    public async Task<ServiceResult<bool>> ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return ServiceResult<bool>.NotFound();

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return ServiceResult<bool>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        return ServiceResult<bool>.NoContent();
    }

    public async Task<MyDataExportDto?> ExportMyDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return null;

        var profile = new ProfileExportDto(
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            user.DisplayName,
            user.DateOfBirth,
            user.Status.ToString(),
            user.CreatedAt,
            user.GdprConsentGiven,
            user.GdprConsentDate,
            user.ParentalConsentGiven);

        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Where(e => e.UserId == userId)
            .Select(e => new EnrollmentExportDto(e.Course.TitleEn, e.EnrolledAt, e.Status.ToString(), e.CompletedAt))
            .ToListAsync(cancellationToken);

        var submissions = await db.AssessmentSubmissions
            .AsNoTracking()
            .Include(s => s.AssessmentDefinition)
            .Where(s => s.UserId == userId)
            .Select(s => new AssessmentSubmissionExportDto(
                s.AssessmentDefinition.TitleEn, s.AnswersJson, s.SummaryJson, s.SubmittedAt))
            .ToListAsync(cancellationToken);

        var certificates = await db.Certificates
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => new CertificateExportDto(c.CourseTitleEn, c.VerificationId, c.IssuedAt, c.Status.ToString()))
            .ToListAsync(cancellationToken);

        var billing = await db.BillingTransactions
            .AsNoTracking()
            .Include(t => t.SubscriptionPlan)
            .Where(t => t.UserId == userId)
            .Select(t => new BillingTransactionExportDto(
                t.SubscriptionPlan.Name, t.Amount, t.Currency, t.Type.ToString(), t.Description, t.CreatedAt))
            .ToListAsync(cancellationToken);

        // Own messages only — the other party's messages in a shared conversation are their
        // personal data, not this user's to export.
        var chatMessages = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.SenderId == userId)
            .Select(m => new ChatMessageExportDto(m.ConversationId, m.Content, m.SentAt))
            .ToListAsync(cancellationToken);

        return new MyDataExportDto(profile, enrollments, submissions, certificates, billing, chatMessages);
    }

    private static ProfileDto ToDto(ApplicationUser user) => new(
        user.Id,
        user.Email ?? "",
        user.FirstName,
        user.LastName,
        user.DisplayName,
        user.AvatarPath,
        user.DateOfBirth,
        user.Status
    );
}
