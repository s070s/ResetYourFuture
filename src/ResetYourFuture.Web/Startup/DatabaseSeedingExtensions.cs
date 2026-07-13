using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Infrastructure.Seeding;

namespace ResetYourFuture.Web.Startup;

/// <summary>
/// Startup migration + seed: pre-warms LocalDB in Development, migrates the relational
/// database, and seeds roles, subscription plans, and the admin user unconditionally, plus
/// (Development-only, SeedData:Enabled) demo blog articles and JSON-driven course/assessment/
/// student sample data — none of which belongs in a real deployment.
/// </summary>
public static class DatabaseSeedingExtensions
{
    /// <summary>
    /// Bounded retry-with-backoff wrapper around <see cref="PrewarmAndSeedDatabaseAsync"/> (AVAIL-2):
    /// without this, an unreachable database at boot crashes the process before it binds a port,
    /// forcing an external restart even when the database becomes reachable moments later.
    /// Every seed step below is idempotent (checks existence before creating), so retrying the
    /// whole call — not just the migration — is safe even if an earlier attempt partially seeded.
    /// </summary>
    public static async Task PrewarmAndSeedDatabaseWithRetryAsync(
        this WebApplication app, int maxAttempts = 5, TimeSpan? initialDelay = null)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(2);
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await app.PrewarmAndSeedDatabaseAsync();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex,
                    "Startup migrate/seed failed on attempt {Attempt}/{MaxAttempts} — retrying in {Delay}.",
                    attempt, maxAttempts, delay);
                await Task.Delay(delay);
                delay += delay; // exponential backoff
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "Startup migrate/seed failed after {MaxAttempts} attempts — the application cannot start.",
                    maxAttempts);
                throw;
            }
        }
    }

    public static async Task PrewarmAndSeedDatabaseAsync(this WebApplication app)
    {
        // --- Pre-warm LocalDB ---
        if (app.Environment.IsDevelopment())
        {
            try
            {
                using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sqllocaldb",
                    Arguments = "start MSSQLLocalDB",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                proc?.WaitForExit(10_000);
            }
            catch { /* sqllocaldb not on PATH — non-fatal */ }
        }

        // --- Migrate & Seed ---
        using var scope = app.Services.CreateScope();
        var config = app.Configuration;
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Relational-only: integration tests swap in the EF Core InMemory provider, which
        // cannot run migrations. Guarding keeps the test host bootable; in production the
        // SQL Server provider is always relational so behavior is unchanged.
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();

        // Seed Roles
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roles = ["Admin", "Student"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed Subscription Plans
        await SubscriptionPlanSeeder.SeedAsync(db, startupLogger);

        // Seed Admin User
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminEmail = config["AdminUser:Email"] ?? "admin@resetyourfuture.local";
        var adminPassword = config["AdminUser:Password"];
        if (string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException(
                "AdminUser:Password is required. Set it via User Secrets (dev) or environment variable AdminUser__Password (prod).");

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Administrator",
                EmailConfirmed = true,
                IsEnabled = true,
                GdprConsentGiven = true,
                GdprConsentDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                startupLogger.LogInformation("Seeded admin user '{Email}'.", adminEmail);
            }
        }

        // Development-only seed data (OPS-1: blog demo content must never touch a real deployment —
        // roles/plans/admin above are the only seeds safe to run unconditionally)
        if (app.Environment.IsDevelopment() && config.GetValue<bool>("SeedData:Enabled"))
        {
            await BlogArticleSeeder.SeedAsync(db, startupLogger);

            var jsonSeedPath = config.GetValue<string>("SeedData:JsonPaths:Courses")
                               ?? Path.Combine(app.Environment.ContentRootPath, "..", "ResetYourFuture.Shared", "JSON", "Courses");
            await CourseSeeder.SeedFromJsonAsync(db, jsonSeedPath, startupLogger);

            var assessmentJsonPath = config.GetValue<string>("SeedData:JsonPaths:Assessments")
                                     ?? Path.Combine(app.Environment.ContentRootPath, "..", "ResetYourFuture.Shared", "JSON", "Assessments");
            await AssessmentSeeder.SeedFromJsonAsync(db, assessmentJsonPath, startupLogger);

            var studentJsonPath = config.GetValue<string>("SeedData:JsonPaths:Students")
                                  ?? Path.Combine(app.Environment.ContentRootPath, "..", "ResetYourFuture.Shared", "JSON", "Students");
            var studentPassword = config["SeedData:StudentPassword"]
                ?? throw new InvalidOperationException(
                    "SeedData:StudentPassword is required when SeedData:Enabled=true. Set it via User Secrets.");
            await StudentSeeder.SeedFromJsonAsync(userManager, studentJsonPath, studentPassword, startupLogger);
        }
    }
}
