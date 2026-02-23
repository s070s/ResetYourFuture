using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using ResetYourFuture.Api.Identity;
using ResetYourFuture.Shared.Seed;

namespace ResetYourFuture.Api.Services;

/// <summary>
/// Seeds student users from JSON files for development and testing purposes.
/// </summary>
public static class StudentSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Seeds student users from all JSON files in the specified folder.
    /// </summary>
    public static async Task SeedFromJsonAsync(
        UserManager<ApplicationUser> userManager,
        string jsonFolderPath,
        string password,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var resolvedPath = Path.GetFullPath(jsonFolderPath);

        if (!Directory.Exists(resolvedPath))
        {
            logger.LogWarning("Student JSON seed folder not found: {Path}", resolvedPath);
            return;
        }

        var jsonFiles = Directory.GetFiles(resolvedPath, "*.json");

        if (jsonFiles.Length == 0)
        {
            logger.LogWarning("No student JSON seed files found in: {Path}", jsonFolderPath);
            return;
        }

        var seededCount = 0;

        foreach (var filePath in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var students = JsonSerializer.Deserialize<List<StudentSeedDto>>(json, JsonOptions);

                if (students is null || students.Count == 0)
                {
                    logger.LogWarning("No students found in: {FilePath}", filePath);
                    continue;
                }

                foreach (var dto in students)
                {
                    var email = dto.Email
                                ?? $"{dto.FirstName.ToLowerInvariant()}.{dto.LastName.ToLowerInvariant()}@resetyourfuture.local";

                    if (await userManager.FindByEmailAsync(email) is not null)
                        continue;

                    var student = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FirstName = dto.FirstName,
                        LastName = dto.LastName,
                        EmailConfirmed = true,
                        IsEnabled = true,
                        GdprConsentGiven = true,
                        GdprConsentDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };

                    var result = await userManager.CreateAsync(student, password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(student, "Student");
                        seededCount++;
                    }
                    else
                    {
                        logger.LogWarning(
                            "Failed to seed student '{Email}': {Errors}",
                            email,
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to seed students from file: {FilePath}", filePath);
            }
        }

        if (seededCount > 0)
        {
            logger.LogInformation("Seeded {Count} student user(s) from JSON files.", seededCount);
        }
    }
}
