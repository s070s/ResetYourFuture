using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Web.ApiServices;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Identity;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// Boots the real application pipeline against an EF Core InMemory database.
/// Required startup config is supplied as environment variables (Program reads them at
/// build time); the SQL Server DbContext registration is swapped for InMemory; the bulk
/// student seeder hosted service is removed. The Program startup block still seeds the
/// Admin/Student roles, subscription plans and admin user into the InMemory store, so the
/// auth/role prerequisites are met automatically.
/// </summary>
public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "web-tests-" + Guid.NewGuid().ToString("N");

    public const string TestPassword = "Test-Pass-1!";

    static CustomWebAppFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-signing-key-minimum-32-bytes-1234567890");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "ResetYourFuture.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "ResetYourFuture.Tests");
        Environment.SetEnvironmentVariable("AdminUser__Password", "Admin-Test-1!");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=(localdb)\\dummy;Database=dummy;Trusted_Connection=True;");
        Environment.SetEnvironmentVariable("Payment__MockEnabled", "true");
        Environment.SetEnvironmentVariable("Sitemap__BaseUrl", "https://tests.local");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // Swap SQL Server for InMemory. The options-configuration delegate (which calls
            // UseSqlServer) must also be removed, otherwise EF Core applies BOTH providers
            // and throws "Only a single database provider can be registered". That type is
            // internal, so it is matched by name.
            var efDescriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                (d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") ?? false)).ToList();
            foreach (var d in efDescriptors)
                services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(_dbName));

            // The bulk student seeder hosted service is irrelevant to tests.
            var hosted = services.Where(d => d.ImplementationType == typeof(BulkStudentSeedingService)).ToList();
            foreach (var d in hosted)
                services.Remove(d);
        });
    }

    /// <summary>Creates an HttpClient carrying a valid Bearer token for a freshly-seeded user in the given role.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com";
        using var scope = Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            EmailConfirmed = true,
            IsEnabled = true
        };
        await um.CreateAsync(user, TestPassword);
        await um.AddToRoleAsync(user, role);

        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var (token, _) = await tokenService.GenerateAccessTokenAsync(user);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Like <see cref="CreateAuthenticatedClientAsync"/> but also returns the seeded user's id (for seeding owned data).</summary>
    public async Task<(HttpClient Client, string UserId)> CreateAuthenticatedClientWithIdAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com";
        using var scope = Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            EmailConfirmed = true,
            IsEnabled = true
        };
        await um.CreateAsync(user, TestPassword);
        await um.AddToRoleAsync(user, role);

        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var (token, _) = await tokenService.GenerateAccessTokenAsync(user);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, user.Id);
    }

    /// <summary>
    /// Like <see cref="CreateAuthenticatedClientAsync"/> but assigns the given subscription tier
    /// so plan-gated endpoints (e.g. certificate issuance) can be reached.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientWithPlanAsync(string role, SubscriptionTierEnum tier)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com";
        using var scope = Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            EmailConfirmed = true,
            IsEnabled = true
        };
        await um.CreateAsync(user, TestPassword);
        await um.AddToRoleAsync(user, role);

        var subService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var plans = await subService.GetPlansAsync();
        var plan = plans.First(p => p.Tier == tier);
        await subService.AssignPlanAsync(user.Id, plan.Id);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.SaveChangesAsync();

        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var (token, _) = await tokenService.GenerateAccessTokenAsync(user);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Runs an arbitrary seeding action against the shared InMemory database.</summary>
    public async Task SeedAsync(Func<ApplicationDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await seed(db);
    }

    /// <summary>Seeds an email-confirmed, enabled user (for endpoints like /login that require confirmation).</summary>
    public async Task CreateConfirmedUserAsync(string email, string password, string role = "Student")
    {
        using var scope = Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            EmailConfirmed = true,
            IsEnabled = true
        };
        await um.CreateAsync(user, password);
        await um.AddToRoleAsync(user, role);
    }
}

[CollectionDefinition("web")]
public sealed class WebCollection : ICollectionFixture<CustomWebAppFactory>;
