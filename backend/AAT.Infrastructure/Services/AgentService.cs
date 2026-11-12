using AAT.Application;
using AAT.Domain;
using AAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAT.Infrastructure.Services;

public sealed class AgentService(
    AatDbContext db,
    ISecretGenerator secrets,
    TimeProvider time,
    ILogger<AgentService> logger) : IAgentService
{
    public async Task<OperationResult<EnrollmentTokenCreated>> CreateEnrollmentTokenAsync(
        Guid organizationId,
        Guid actorUserId,
        CreateEnrollmentTokenRequest request,
        CancellationToken cancellationToken)
    {
        var agentName = request.AgentName?.Trim() ?? "";
        if (agentName.Length is < 2 or > 120)
            return new OperationResult<EnrollmentTokenCreated>.Fail("Agent name must be between 2 and 120 characters.");

        var environment = await db.Environments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.EnvironmentId && item.OrganizationId == organizationId, cancellationToken);
        if (environment is null)
            return new OperationResult<EnrollmentTokenCreated>.Fail("Environment was not found in this organization.");

        var now = time.GetUtcNow().UtcDateTime;
        var token = secrets.CreateEnrollmentToken();
        db.EnrollmentTokens.Add(new EnrollmentToken
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            EnvironmentId = environment.Id,
            AgentName = agentName,
            TokenHash = secrets.Hash(token),
            ExpiresAtUtc = now.AddHours(24),
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now
        });
        db.AuditLogs.Add(AuthService.Audit(organizationId, actorUserId, null, "agent.enrollment_token_created", agentName, now));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created enrollment token for organization {OrganizationId}", organizationId);
        return new OperationResult<EnrollmentTokenCreated>.Ok(new EnrollmentTokenCreated(token, now.AddHours(24), environment.Id, agentName));
    }

    public async Task<EnrollResult> EnrollAsync(EnrollAgentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EnrollmentToken))
            return new EnrollResult.Invalid();

        var hash = secrets.Hash(request.EnrollmentToken.Trim());
        var token = await db.EnrollmentTokens.AsNoTracking().FirstOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (token is null)
            return new EnrollResult.Invalid();

        var now = time.GetUtcNow().UtcDateTime;
        if (token.UsedAtUtc is not null)
            return new EnrollResult.AlreadyUsed();
        if (token.ExpiresAtUtc <= now)
            return new EnrollResult.Expired();

        var name = string.IsNullOrWhiteSpace(request.Name) ? token.AgentName : request.Name.Trim();
        var version = request.Version?.Trim() ?? "";
        if (name.Length is < 2 or > 120)
            return new EnrollResult.Invalid();
        if (version.Length is < 1 or > 64)
            return new EnrollResult.Invalid();

        var agentId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var credential = secrets.CreateAgentCredential();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var consumed = await db.EnrollmentTokens
            .Where(item => item.Id == token.Id && item.UsedAtUtc == null && item.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.UsedAtUtc, now)
                .SetProperty(item => item.UsedByAgentId, agentId), cancellationToken);
        if (consumed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var current = await db.EnrollmentTokens.AsNoTracking().FirstAsync(item => item.Id == token.Id, cancellationToken);
            return current.UsedAtUtc is not null ? new EnrollResult.AlreadyUsed() : new EnrollResult.Expired();
        }

        db.Agents.Add(new ObserverAgent
        {
            Id = agentId,
            OrganizationId = token.OrganizationId,
            EnvironmentId = token.EnvironmentId,
            DefaultApplicationId = applicationId,
            Name = name,
            Version = version,
            HealthState = AgentHealthState.Healthy,
            CredentialHash = secrets.Hash(credential),
            CreatedAtUtc = now
        });
        db.Applications.Add(new AiApplication
        {
            Id = applicationId,
            OrganizationId = token.OrganizationId,
            AgentId = agentId,
            Name = name,
            IntegrationType = "explicit",
            CreatedAtUtc = now
        });
        db.AuditLogs.Add(AuthService.Audit(token.OrganizationId, null, agentId, "agent.enrolled", name, now));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Enrolled agent {AgentId}", agentId);
        return new EnrollResult.Ok(new EnrollAgentSuccess(agentId, token.OrganizationId, token.EnvironmentId, applicationId, credential));
    }

    public async Task<bool> HeartbeatAsync(Guid agentId, HeartbeatRequest request, CancellationToken cancellationToken)
    {
        var version = request.AgentVersion?.Trim() ?? "";
        if (version.Length is < 1 or > 64 || !Enum.TryParse<AgentHealthState>(request.HealthState, true, out var health))
            return false;

        var agent = await db.Agents.FirstOrDefaultAsync(item => item.Id == agentId && item.RevokedAtUtc == null, cancellationToken);
        if (agent is null)
            return false;

        var now = time.GetUtcNow().UtcDateTime;
        agent.Version = version;
        agent.HealthState = health;
        agent.LastSeenUtc = now;
        db.Heartbeats.Add(new AgentHeartbeat
        {
            Id = Guid.NewGuid(),
            OrganizationId = agent.OrganizationId,
            AgentId = agent.Id,
            ReportedAtUtc = now,
            AgentVersion = version,
            HealthState = health
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AgentDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var environments = await db.Environments.AsNoTracking()
            .Where(environment => environment.OrganizationId == organizationId)
            .ToDictionaryAsync(environment => environment.Id, cancellationToken);
        var agents = await db.Agents.AsNoTracking()
            .Where(agent => agent.OrganizationId == organizationId)
            .OrderBy(agent => agent.Name)
            .ToListAsync(cancellationToken);

        return agents.Select(agent => Map(agent, environments, now)).ToList();
    }

    public async Task<AgentDetailDto?> GetAsync(Guid organizationId, Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await db.Agents.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == agentId && item.OrganizationId == organizationId, cancellationToken);
        if (agent is null)
            return null;

        var environments = await db.Environments.AsNoTracking()
            .Where(environment => environment.Id == agent.EnvironmentId && environment.OrganizationId == organizationId)
            .ToDictionaryAsync(environment => environment.Id, cancellationToken);
        var heartbeats = await db.Heartbeats.AsNoTracking()
            .Where(heartbeat => heartbeat.AgentId == agentId && heartbeat.OrganizationId == organizationId)
            .OrderByDescending(heartbeat => heartbeat.ReportedAtUtc)
            .Take(10)
            .Select(heartbeat => new HeartbeatDto(heartbeat.ReportedAtUtc, heartbeat.AgentVersion, heartbeat.HealthState.ToString()))
            .ToListAsync(cancellationToken);

        return new AgentDetailDto(Map(agent, environments, time.GetUtcNow().UtcDateTime), heartbeats);
    }

    public async Task<bool> RevokeAsync(Guid organizationId, Guid actorUserId, Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(item => item.Id == agentId && item.OrganizationId == organizationId, cancellationToken);
        if (agent is null)
            return false;
        if (agent.RevokedAtUtc is null)
        {
            var now = time.GetUtcNow().UtcDateTime;
            agent.RevokedAtUtc = now;
            db.AuditLogs.Add(AuthService.Audit(organizationId, actorUserId, agentId, "agent.revoked", agent.Name, now));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Revoked agent {AgentId}", agentId);
        }

        return true;
    }

    private static AgentDto Map(ObserverAgent agent, IReadOnlyDictionary<Guid, TenantEnvironment> environments, DateTime utcNow)
    {
        environments.TryGetValue(agent.EnvironmentId, out var environment);
        var connectivity = agent.RevokedAtUtc is null
            ? AgentConnectivity.Derive(agent.LastSeenUtc, utcNow)
            : ConnectivityStatus.Offline;
        return new AgentDto(
            agent.Id,
            agent.Name,
            agent.Version,
            agent.HealthState.ToString(),
            connectivity.ToString(),
            agent.LastSeenUtc,
            agent.EnvironmentId,
            environment?.Name ?? "",
            agent.CreatedAtUtc,
            agent.RevokedAtUtc is not null);
    }
}
