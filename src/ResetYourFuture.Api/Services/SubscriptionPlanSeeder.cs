using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Api.Domain.Enums;
using ResetYourFuture.Shared.Subscriptions;

namespace ResetYourFuture.Api.Services;

/// <summary>
/// Seeds the three default subscription plans (Free, Plus, Pro).
/// Idempotent: skips if plans already exist.
/// </summary>
public static class SubscriptionPlanSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await db.SubscriptionPlans.AnyAsync())
        {
            logger.LogInformation("Subscription plans already seeded. Skipping.");
            return;
        }

        var plans = new List<SubscriptionPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Free",
                Description = "Get started with basic access to free courses.",
                Price = 0m,
                BillingPeriod = BillingPeriod.Lifetime,
                Tier = SubscriptionTier.Free,
                FeaturesJson = JsonSerializer.Serialize(new PlanFeaturesDto
                {
                    MaxCourses = 1,
                    AssessmentAccess = false,
                    CertificateAccess = false,
                    PrioritySupport = false
                }),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Plus",
                Description = "Unlock all courses and assessments.",
                Price = 9.99m,
                BillingPeriod = BillingPeriod.Monthly,
                Tier = SubscriptionTier.Plus,
                FeaturesJson = JsonSerializer.Serialize(new PlanFeaturesDto
                {
                    MaxCourses = 10,
                    AssessmentAccess = true,
                    CertificateAccess = false,
                    PrioritySupport = false
                }),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Pro",
                Description = "Full access with certificates and priority support.",
                Price = 19.99m,
                BillingPeriod = BillingPeriod.Monthly,
                Tier = SubscriptionTier.Pro,
                FeaturesJson = JsonSerializer.Serialize(new PlanFeaturesDto
                {
                    MaxCourses = int.MaxValue,
                    AssessmentAccess = true,
                    CertificateAccess = true,
                    PrioritySupport = true
                }),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        db.SubscriptionPlans.AddRange(plans);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} subscription plans (Free, Plus, Pro).", plans.Count);
    }
}
