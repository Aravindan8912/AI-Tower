namespace AAT.Domain;

public sealed class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public OrganizationStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class TenantEnvironment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public EnvironmentType Type { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ObserverAgent
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EnvironmentId { get; set; }
    public Guid DefaultApplicationId { get; set; }
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public AgentHealthState HealthState { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public string CredentialHash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

public sealed class EnrollmentToken
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string AgentName { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public Guid? UsedByAgentId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AgentHeartbeat
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public DateTime ReportedAtUtc { get; set; }
    public string AgentVersion { get; set; } = "";
    public AgentHealthState HealthState { get; set; }
}

public sealed class AiApplication
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public string Name { get; set; } = "";
    public string IntegrationType { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ModelCatalogEntry
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Provider { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string Runtime { get; set; } = "";
}

public sealed class ExecutionTrace
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public Guid EnvironmentId { get; set; }
    public Guid? ApplicationId { get; set; }
    public Guid ModelId { get; set; }
    public string RequestId { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int? TimeToFirstTokenMs { get; set; }
    public int TotalLatencyMs { get; set; }
    public double? TokensPerSecond { get; set; }
    public ExecutionStatus Status { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}

public sealed class HardwareMetric
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AgentId { get; set; }
    public DateTime SampledAtUtc { get; set; }
    public double CpuPercent { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long MemoryTotalBytes { get; set; }
    public double? GpuUtilizationPercent { get; set; }
    public long? GpuMemoryUsedBytes { get; set; }
    public long? GpuMemoryTotalBytes { get; set; }
    public string? GpuName { get; set; }
    public string CollectionSource { get; set; } = "";
    public DateTime ReceivedAtUtc { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? ActorAgentId { get; set; }
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
}
