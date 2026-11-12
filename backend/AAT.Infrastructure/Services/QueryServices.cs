using AAT.Application;
using AAT.Domain;
using AAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AAT.Infrastructure.Services;

public sealed class ExecutionQueryService(AatDbContext db) : IExecutionQueryService
{
    public async Task<PagedResult<ExecutionDto>> QueryAsync(Guid organizationId, ExecutionQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var traces =
            from trace in db.ExecutionTraces.AsNoTracking()
            join agent in db.Agents.AsNoTracking() on trace.AgentId equals agent.Id
            join environment in db.Environments.AsNoTracking() on trace.EnvironmentId equals environment.Id
            join model in db.Models.AsNoTracking() on trace.ModelId equals model.Id
            where trace.OrganizationId == organizationId
                && agent.OrganizationId == organizationId
                && environment.OrganizationId == organizationId
                && model.OrganizationId == organizationId
                && trace.StartedAtUtc >= query.FromUtc
                && trace.StartedAtUtc < query.ToUtc
            select new { trace, agent, environment, model };

        if (query.AgentId is Guid agentId)
            traces = traces.Where(row => row.trace.AgentId == agentId);
        if (query.EnvironmentId is Guid environmentId)
            traces = traces.Where(row => row.trace.EnvironmentId == environmentId);
        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            var modelFilter = query.Model.Trim().ToLowerInvariant();
            traces = traces.Where(row => row.model.ModelName.ToLower().Contains(modelFilter) || row.model.Provider.ToLower().Contains(modelFilter));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ExecutionStatus>(query.Status, true, out var status))
            traces = traces.Where(row => row.trace.Status == status);

        var total = await traces.CountAsync(cancellationToken);
        var rows = await traces
            .OrderByDescending(row => row.trace.StartedAtUtc)
            .ThenBy(row => row.trace.EventId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new ExecutionDto(
                row.trace.EventId,
                row.trace.RequestId,
                row.agent.Id,
                row.agent.Name,
                row.environment.Id,
                row.environment.Name,
                row.model.Provider,
                row.model.ModelName,
                row.trace.PromptTokens,
                row.trace.CompletionTokens,
                row.trace.TimeToFirstTokenMs,
                row.trace.TotalLatencyMs,
                row.trace.TokensPerSecond,
                row.trace.Status.ToString(),
                row.trace.ErrorCode,
                row.trace.StartedAtUtc,
                row.trace.CompletedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ExecutionDto>(rows, page, pageSize, total);
    }
}

public sealed class AnalyticsService(AatDbContext db, TimeProvider time) : IAnalyticsService
{
    public async Task<OverviewDto> OverviewAsync(Guid organizationId, DateTime fromUtc, DateTime toUtc, Guid? environmentId, CancellationToken cancellationToken)
    {
        var query = ScopedTraces(organizationId, fromUtc, toUtc, environmentId);
        var rows = await query
            .Select(trace => new { trace.Status, trace.PromptTokens, trace.CompletionTokens, trace.TotalLatencyMs })
            .ToListAsync(cancellationToken);
        var rollup = ExecutionAccounting.Summarize(rows
            .Select(row => new ExecutionAccounting.Row(row.Status, row.PromptTokens, row.CompletionTokens, row.TotalLatencyMs))
            .ToArray());
        var agentsQuery = db.Agents.AsNoTracking()
            .Where(agent => agent.OrganizationId == organizationId && agent.RevokedAtUtc == null);
        if (environmentId is Guid selectedEnvironment)
            agentsQuery = agentsQuery.Where(agent => agent.EnvironmentId == selectedEnvironment);
        var agents = await agentsQuery
            .Select(agent => agent.LastSeenUtc)
            .ToListAsync(cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;
        var online = agents.Count(lastSeen => AgentConnectivity.Derive(lastSeen, now) == ConnectivityStatus.Online);

        return new OverviewDto(
            fromUtc,
            toUtc,
            rollup.Count,
            rollup.FailedCount,
            rollup.Count == 0 ? 0 : (double)rollup.FailedCount / rollup.Count,
            rollup.PromptTokens,
            rollup.CompletionTokens,
            rollup.AverageLatencyMs,
            rollup.P95LatencyMs,
            online,
            agents.Count,
            rollup.SucceededCount,
            rollup.CancelledCount,
            rollup.SucceededTokens,
            rollup.FailedTokens,
            rollup.CancelledTokens);
    }

    public async Task<AgentActivityDto?> AgentActivityAsync(Guid organizationId, Guid agentId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var ownsAgent = await db.Agents.AsNoTracking()
            .AnyAsync(agent => agent.Id == agentId && agent.OrganizationId == organizationId, cancellationToken);
        if (!ownsAgent)
            return null;

        var rows = await db.ExecutionTraces.AsNoTracking()
            .Where(trace => trace.OrganizationId == organizationId
                && trace.AgentId == agentId
                && trace.StartedAtUtc >= fromUtc
                && trace.StartedAtUtc < toUtc)
            .Select(trace => new { trace.Status, trace.PromptTokens, trace.CompletionTokens, trace.TotalLatencyMs, trace.TokensPerSecond })
            .ToListAsync(cancellationToken);
        var rollup = ExecutionAccounting.Summarize(rows
            .Select(row => new ExecutionAccounting.Row(row.Status, row.PromptTokens, row.CompletionTokens, row.TotalLatencyMs))
            .ToArray());
        var rates = rows.Where(row => row.TokensPerSecond is not null).Select(row => row.TokensPerSecond!.Value).ToArray();
        var latest = await db.HardwareMetrics.AsNoTracking()
            .Where(metric => metric.OrganizationId == organizationId && metric.AgentId == agentId)
            .OrderByDescending(metric => metric.SampledAtUtc)
            .Select(metric => new HardwarePointDto(
                metric.SampledAtUtc,
                metric.CpuPercent,
                metric.MemoryUsedBytes,
                metric.MemoryTotalBytes,
                metric.GpuUtilizationPercent,
                metric.GpuMemoryUsedBytes,
                metric.GpuMemoryTotalBytes,
                metric.GpuName,
                metric.CollectionSource))
            .FirstOrDefaultAsync(cancellationToken);

        return new AgentActivityDto(
            agentId,
            fromUtc,
            toUtc,
            rollup.Count,
            rollup.SucceededCount,
            rollup.FailedCount,
            rollup.CancelledCount,
            rollup.PromptTokens,
            rollup.CompletionTokens,
            rollup.SucceededTokens,
            rollup.FailedTokens,
            rollup.CancelledTokens,
            rollup.AverageLatencyMs,
            rollup.P95LatencyMs,
            rates.Length == 0 ? null : rates.Average(),
            latest,
            HardwareAttribution.EstimateLabel);
    }

    public async Task<IReadOnlyList<ModelAnalyticsDto>> ModelsAsync(Guid organizationId, DateTime fromUtc, DateTime toUtc, Guid? environmentId, CancellationToken cancellationToken)
    {
        var rows = await (
            from trace in ScopedTraces(organizationId, fromUtc, toUtc, environmentId)
            join model in db.Models.AsNoTracking() on trace.ModelId equals model.Id
            where model.OrganizationId == organizationId
            select new
            {
                model.Provider,
                model.ModelName,
                model.Runtime,
                trace.Status,
                trace.PromptTokens,
                trace.CompletionTokens,
                trace.TotalLatencyMs
            }).ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => new { row.Provider, row.ModelName, row.Runtime })
            .Select(group =>
            {
                var failed = group.Count(row => row.Status == ExecutionStatus.Failed);
                return new ModelAnalyticsDto(
                    group.Key.Provider,
                    group.Key.ModelName,
                    group.Key.Runtime,
                    group.Count(),
                    failed,
                    group.Count() == 0 ? 0 : (double)failed / group.Count(),
                    group.Sum(row => (long)row.PromptTokens + row.CompletionTokens),
                    group.Average(row => row.TotalLatencyMs));
            })
            .OrderByDescending(row => row.ExecutionCount)
            .ToList();
    }

    public async Task<HardwareSeriesDto> HardwareAsync(Guid organizationId, Guid? agentId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var query = db.HardwareMetrics.AsNoTracking()
            .Where(metric => metric.OrganizationId == organizationId
                && metric.SampledAtUtc >= fromUtc
                && metric.SampledAtUtc < toUtc);
        if (agentId is Guid id)
            query = query.Where(metric => metric.AgentId == id);

        var latest = await query
            .OrderByDescending(metric => metric.SampledAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);
        latest.Reverse();

        var points = latest.Select(metric => new HardwarePointDto(
            metric.SampledAtUtc,
            metric.CpuPercent,
            metric.MemoryUsedBytes,
            metric.MemoryTotalBytes,
            metric.GpuUtilizationPercent,
            metric.GpuMemoryUsedBytes,
            metric.GpuMemoryTotalBytes,
            metric.GpuName,
            metric.CollectionSource)).ToList();

        return new HardwareSeriesDto(HardwareAttribution.EstimateLabel, agentId, points);
    }

    private IQueryable<ExecutionTrace> ScopedTraces(Guid organizationId, DateTime fromUtc, DateTime toUtc, Guid? environmentId)
    {
        var query = db.ExecutionTraces.AsNoTracking()
            .Where(trace => trace.OrganizationId == organizationId
                && trace.StartedAtUtc >= fromUtc
                && trace.StartedAtUtc < toUtc);
        if (environmentId is Guid id)
            query = query.Where(trace => trace.EnvironmentId == id);
        return query;
    }

}

public sealed class AuditQueryService(AatDbContext db) : IAuditQueryService
{
    public async Task<PagedResult<AuditDto>> QueryAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.AuditLogs.AsNoTracking().Where(log => log.OrganizationId == organizationId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new AuditDto(log.Id, log.Action, log.Target, log.TimestampUtc, log.ActorUserId, log.ActorAgentId))
            .ToListAsync(cancellationToken);
        return new PagedResult<AuditDto>(items, page, pageSize, total);
    }
}
