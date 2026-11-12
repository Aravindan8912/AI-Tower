using AAT.Application;
using AAT.Domain;
using AAT.Infrastructure.Persistence;
using AAT.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAT.Infrastructure.Services;

public sealed class TelemetryIngestionService(
    AatDbContext db,
    TimeProvider time,
    IOptions<TelemetryOptions> options,
    ILogger<TelemetryIngestionService> logger) : ITelemetryIngestionService
{
    public async Task<IngestionResultDto> IngestExecutionsAsync(Guid agentId, ExecutionBatchRequest batch, CancellationToken cancellationToken)
    {
        var events = batch.Events;
        var sizeError = ValidateBatchSize(events?.Count ?? 0);
        if (sizeError is not null)
            return Reject(sizeError);
        if (events is null)
            return Reject("Batch must contain between 1 and the configured maximum events.");

        var agent = await db.Agents.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == agentId && item.RevokedAtUtc == null, cancellationToken);
        if (agent is null)
            return Reject("Agent is not enrolled.");

        var now = time.GetUtcNow().UtcDateTime;
        var errors = new List<string>();
        var accepted = 0;
        var duplicates = 0;
        var rejected = 0;
        var eventIds = events.Select(item => item.EventId).Where(id => id != Guid.Empty).ToArray();
        var existing = await db.ExecutionTraces.AsNoTracking()
            .Where(trace => trace.OrganizationId == agent.OrganizationId && eventIds.Contains(trace.EventId))
            .Select(trace => trace.EventId)
            .ToListAsync(cancellationToken);
        var existingIds = existing.ToHashSet();
        var seen = new HashSet<Guid>();
        var models = await db.Models.Where(model => model.OrganizationId == agent.OrganizationId).ToListAsync(cancellationToken);
        var applicationIds = await db.Applications.AsNoTracking()
            .Where(application => application.OrganizationId == agent.OrganizationId && application.AgentId == agent.Id)
            .Select(application => application.Id)
            .ToListAsync(cancellationToken);
        var ownedApplications = applicationIds.ToHashSet();

        for (var index = 0; index < events.Count; index++)
        {
            var evt = events[index];
            var validation = TelemetryValidation.ValidateExecution(evt, index);
            if (validation.Count > 0)
            {
                rejected++;
                errors.AddRange(validation);
                continue;
            }

            if (!seen.Add(evt.EventId) || existingIds.Contains(evt.EventId))
            {
                duplicates++;
                continue;
            }

            var applicationId = evt.ApplicationId ?? agent.DefaultApplicationId;
            if (evt.ApplicationId is Guid requested && !ownedApplications.Contains(requested))
            {
                rejected++;
                errors.Add($"events[{index}].applicationId is not owned by this agent.");
                continue;
            }

            TelemetryValidation.TryCanonicalStatus(evt.Status, out var status);
            var provider = evt.Provider.Trim();
            var modelName = evt.Model.Trim();
            var runtime = string.IsNullOrWhiteSpace(evt.Runtime) ? provider : evt.Runtime.Trim();
            var model = models.FirstOrDefault(item => item.Provider == provider && item.ModelName == modelName);
            if (model is null)
            {
                model = new ModelCatalogEntry
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = agent.OrganizationId,
                    Provider = provider,
                    ModelName = modelName,
                    Runtime = runtime
                };
                db.Models.Add(model);
                models.Add(model);
            }

            db.ExecutionTraces.Add(new ExecutionTrace
            {
                Id = Guid.NewGuid(),
                EventId = evt.EventId,
                OrganizationId = agent.OrganizationId,
                AgentId = agent.Id,
                EnvironmentId = agent.EnvironmentId,
                ApplicationId = applicationId,
                ModelId = model.Id,
                RequestId = evt.RequestId.Trim(),
                PromptTokens = evt.PromptTokens,
                CompletionTokens = evt.CompletionTokens,
                TimeToFirstTokenMs = evt.TimeToFirstTokenMs,
                TotalLatencyMs = evt.TotalLatencyMs,
                TokensPerSecond = evt.TokensPerSecond,
                Status = Enum.Parse<ExecutionStatus>(status),
                ErrorCode = string.IsNullOrWhiteSpace(evt.ErrorCode) ? null : evt.ErrorCode.Trim(),
                StartedAtUtc = evt.StartedAtUtc.UtcDateTime,
                CompletedAtUtc = evt.CompletedAtUtc.UtcDateTime,
                ReceivedAtUtc = now
            });
            accepted++;
        }

        return await SaveAsync(agent.Id, "executions", accepted, duplicates, rejected, errors, cancellationToken);
    }

    public async Task<IngestionResultDto> IngestHardwareAsync(Guid agentId, HardwareBatchRequest batch, CancellationToken cancellationToken)
    {
        var samples = batch.Samples;
        var sizeError = ValidateBatchSize(samples?.Count ?? 0);
        if (sizeError is not null || samples is null)
            return Reject(sizeError ?? "Batch must contain between 1 and the configured maximum samples.");

        var agent = await db.Agents.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == agentId && item.RevokedAtUtc == null, cancellationToken);
        if (agent is null)
            return Reject("Agent is not enrolled.");

        var now = time.GetUtcNow().UtcDateTime;
        var errors = new List<string>();
        var accepted = 0;
        var duplicates = 0;
        var rejected = 0;
        var eventIds = samples.Select(item => item.EventId).Where(id => id != Guid.Empty).ToArray();
        var existing = await db.HardwareMetrics.AsNoTracking()
            .Where(metric => metric.OrganizationId == agent.OrganizationId && eventIds.Contains(metric.EventId))
            .Select(metric => metric.EventId)
            .ToListAsync(cancellationToken);
        var existingIds = existing.ToHashSet();
        var seen = new HashSet<Guid>();

        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            var validation = TelemetryValidation.ValidateHardware(sample, index);
            if (validation.Count > 0)
            {
                rejected++;
                errors.AddRange(validation);
                continue;
            }

            if (!seen.Add(sample.EventId) || existingIds.Contains(sample.EventId))
            {
                duplicates++;
                continue;
            }

            db.HardwareMetrics.Add(new HardwareMetric
            {
                Id = Guid.NewGuid(),
                EventId = sample.EventId,
                OrganizationId = agent.OrganizationId,
                AgentId = agent.Id,
                SampledAtUtc = sample.SampledAtUtc.UtcDateTime,
                CpuPercent = sample.CpuPercent,
                MemoryUsedBytes = sample.MemoryUsedBytes,
                MemoryTotalBytes = sample.MemoryTotalBytes,
                GpuUtilizationPercent = sample.GpuUtilizationPercent,
                GpuMemoryUsedBytes = sample.GpuMemoryUsedBytes,
                GpuMemoryTotalBytes = sample.GpuMemoryTotalBytes,
                GpuName = string.IsNullOrWhiteSpace(sample.GpuName) ? null : sample.GpuName.Trim(),
                CollectionSource = sample.CollectionSource.Trim(),
                ReceivedAtUtc = now
            });
            accepted++;
        }

        return await SaveAsync(agent.Id, "hardware", accepted, duplicates, rejected, errors, cancellationToken);
    }

    private async Task<IngestionResultDto> SaveAsync(
        Guid agentId,
        string kind,
        int accepted,
        int duplicates,
        int rejected,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        if (accepted > 0)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
            {
                logger.LogInformation("Idempotent conflict while storing {Kind} for agent {AgentId}", kind, agentId);
                return new IngestionResultDto
                {
                    Conflict = true,
                    BatchError = "Concurrent ingestion conflict. Retry the batch."
                };
            }
        }

        logger.LogInformation(
            "Ingested {Kind} for agent {AgentId}. Accepted {Accepted}, duplicates {Duplicates}, rejected {Rejected}",
            kind,
            agentId,
            accepted,
            duplicates,
            rejected);

        return new IngestionResultDto
        {
            Accepted = accepted,
            Duplicates = duplicates,
            Rejected = rejected,
            Errors = errors
        };
    }

    private string? ValidateBatchSize(int count)
    {
        var max = Math.Clamp(options.Value.MaxBatchSize, 1, TelemetryLimits.MaxBatchSize);
        return count < 1 || count > max
            ? $"Batch must contain between 1 and {max} items."
            : null;
    }

    private static IngestionResultDto Reject(string error) =>
        new() { BatchRejected = true, BatchError = error };
}
