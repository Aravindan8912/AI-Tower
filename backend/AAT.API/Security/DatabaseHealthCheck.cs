using AAT.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AAT.API.Security;

public sealed class DatabaseHealthCheck(AatDbContext db, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connected = await db.Database.CanConnectAsync(cancellationToken);
            return connected
                ? HealthCheckResult.Healthy("Database connection succeeded.")
                : HealthCheckResult.Unhealthy("Database connection failed.");
        }
        catch (Exception exception)
        {
            logger.LogWarning("Database health check failed ({ExceptionType})", exception.GetType().Name);
            return HealthCheckResult.Unhealthy("Database connection failed.");
        }
    }
}
