using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CoralLedger.Blue.Infrastructure.Jobs;

/// <summary>
/// Scheduled job that cleans up expired and used tokens (email verification and password reset)
/// Runs daily to prevent table bloat
/// </summary>
[DisallowConcurrentExecution]
public class ExpiredTokenCleanupJob : IJob
{
    public static readonly JobKey Key = new("ExpiredTokenCleanupJob", "Maintenance");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredTokenCleanupJob> _logger;

    public ExpiredTokenCleanupJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredTokenCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting expired token cleanup at {Time}", DateTimeOffset.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MarineDbContext>();

        try
        {
            // Delete tokens that are either:
            // 1. Expired (regardless of IsUsed status)
            // 2. Used more than 24 hours ago (already consumed, safe to delete)
            var cutoffDate = DateTime.UtcNow;
            var usedTokenCutoff = DateTime.UtcNow.AddHours(-24);

            var expiredTokens = await dbContext.EmailVerificationTokens
                .Where(t => t.ExpiresAt < cutoffDate ||
                           (t.IsUsed && t.UsedAt < usedTokenCutoff))
                .ToListAsync(context.CancellationToken)
                .ConfigureAwait(false);

            if (expiredTokens.Count > 0)
            {
                _logger.LogInformation("Found {Count} expired/used email verification tokens to clean up", expiredTokens.Count);

                dbContext.EmailVerificationTokens.RemoveRange(expiredTokens);
                await dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully cleaned up {Count} expired/used email verification tokens", expiredTokens.Count);
            }
            else
            {
                _logger.LogInformation("No expired email verification tokens found to clean up");
            }

            // Clean up password reset tokens
            var expiredPasswordResetTokens = await dbContext.PasswordResetTokens
                .Where(t => t.ExpiresAt < cutoffDate ||
                           (t.IsUsed && t.UsedAt < usedTokenCutoff))
                .ToListAsync(context.CancellationToken)
                .ConfigureAwait(false);

            if (expiredPasswordResetTokens.Count > 0)
            {
                _logger.LogInformation("Found {Count} expired/used password reset tokens to clean up", expiredPasswordResetTokens.Count);

                dbContext.PasswordResetTokens.RemoveRange(expiredPasswordResetTokens);
                await dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully cleaned up {Count} expired/used password reset tokens", expiredPasswordResetTokens.Count);
            }
            else
            {
                _logger.LogInformation("No expired password reset tokens found to clean up");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during expired token cleanup");
            throw; // Re-throw to trigger Quartz retry policy if configured
        }
    }
}
