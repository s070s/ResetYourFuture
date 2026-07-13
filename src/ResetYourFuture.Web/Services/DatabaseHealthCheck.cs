using Microsoft.Extensions.Diagnostics.HealthChecks;
using ResetYourFuture.Infrastructure.Data;

namespace ResetYourFuture.Web.Services;

/// <summary>Readiness check (AVAIL-1): confirms the database is actually reachable, rather than
/// only discoverable at the next request that happens to touch it.</summary>
public sealed class DatabaseHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connectivity check threw.", ex);
        }
    }
}
