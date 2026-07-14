using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.Data;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// COMP-5: periodically deletes <c>RefreshToken</c> rows past their <c>ExpiresAt</c> — the
/// "quick win" slice of the data-retention finding (revoked/expired refresh tokens accumulate
/// with no purge). Safe to delete purely by expiry: <c>AuthApiService.RefreshAsync</c> checks
/// <c>ExpiresAt &lt;= now</c> before it ever looks at <c>RevokedAt</c>, so an expired row (revoked
/// or not) can no longer succeed or contribute to reuse detection — presenting its token again
/// after deletion gets the same "invalid or expired" rejection as presenting it before deletion
/// (row-not-found vs. row-expired look identical to the caller).
///
/// Uses a plain Task.Delay loop, matching <see cref="SubscriptionExpirySweeper"/>'s convention.
/// </summary>
public sealed class RefreshTokenPurgeService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);

    private readonly IServiceProvider _services;
    private readonly ILogger<RefreshTokenPurgeService> _logger;

    public RefreshTokenPurgeService(IServiceProvider services, ILogger<RefreshTokenPurgeService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeExpiredTokensAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshTokenPurgeService: Error during purge.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // graceful shutdown (Ctrl+C) — exit the loop quietly
            }
        }
    }

    private async Task PurgeExpiredTokensAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        // Tracked mutation + SaveChanges (not ExecuteDeleteAsync) so this runs on every provider
        // this app targets, including EF Core InMemory (used by the Web.Tests factory).
        var now = DateTimeOffset.UtcNow;
        var expired = await db.RefreshTokens
            .Where(rt => rt.ExpiresAt <= now)
            .ToListAsync(stoppingToken);

        if (expired.Count == 0)
            return;

        db.RefreshTokens.RemoveRange(expired);
        await db.SaveChangesAsync(stoppingToken);

        _logger.LogInformation("RefreshTokenPurgeService: Purged {Count} expired refresh token(s).", expired.Count);
    }
}
