using AAT.Application;
using AAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAT.Worker;

public sealed class TelemetryRetentionService(
    IServiceScopeFactory scopes,
    IOptions<TelemetryOptions> options,
    TimeProvider time,
    IHostEnvironment environment,
    ILogger<TelemetryRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                await PurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Telemetry retention sweep failed");
            }
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        var retentionDays = Math.Clamp(options.Value.RetentionDays, 1, 3650);
        var auditDays = Math.Clamp(options.Value.AuditRetentionDays, 1, 3650);
        var now = time.GetUtcNow().UtcDateTime;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AatDbContext>();
        var telemetryCutoff = now.AddDays(-retentionDays);
        var auditCutoff = now.AddDays(-auditDays);
        var executions = await db.ExecutionTraces.Where(trace => trace.ReceivedAtUtc < telemetryCutoff).ExecuteDeleteAsync(cancellationToken);
        var hardware = await db.HardwareMetrics.Where(metric => metric.ReceivedAtUtc < telemetryCutoff).ExecuteDeleteAsync(cancellationToken);
        var heartbeats = await db.Heartbeats.Where(heartbeat => heartbeat.ReportedAtUtc < telemetryCutoff).ExecuteDeleteAsync(cancellationToken);
        var audits = await db.AuditLogs.Where(log => log.TimestampUtc < auditCutoff).ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation(
            "Retention removed {Executions} executions, {Hardware} hardware samples, {Heartbeats} heartbeats, and {Audits} audit rows",
            executions,
            hardware,
            heartbeats,
            audits);
    }
}
