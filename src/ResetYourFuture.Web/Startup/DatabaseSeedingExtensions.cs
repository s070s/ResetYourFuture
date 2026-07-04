using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Infrastructure.Seeding;

namespace ResetYourFuture.Web.Startup;

/// <summary>
/// Startup migration + seed: pre-warms LocalDB in Development, migrates the relational
/// database, and seeds roles, subscription plans, blog articles, the admin user, and
/// (Development-only) JSON-driven course/assessment/student sample data.
/// </summary>
public static class DatabaseSeedingExtensions
{
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

        // Seed Blog Articles
        await BlogArticleSeeder.SeedAsync(db, startupLogger);

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

        // Development-only seed data
        if (app.Environment.IsDevelopment() && config.GetValue<bool>("SeedData:Enabled"))
        {
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
