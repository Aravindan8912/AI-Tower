using AAT.Telemetry;

namespace AAT.Application;

public static class HardwareAttribution
{
    public const string EstimateLabel =
        "Host-level samples. Overlap with a request window is an estimate, not exact per-request GPU or CPU ownership.";
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record RegisterRequest(string OrganizationName, string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthSuccess(Guid OrganizationId, Guid UserId, string Email, string DisplayName, string Role, string OrganizationName);

public abstract record AuthResult
{
    public sealed record Ok(AuthSuccess Value) : AuthResult;
    public sealed record Fail(string Error) : AuthResult;
}

public sealed record CreateUserRequest(string Email, string DisplayName, string Password, string Role);
public sealed record UserCreated(Guid UserId, string Email, string Role);

public sealed record CreateEnvironmentRequest(string Name, string Type);
public sealed record EnvironmentDto(Guid Id, string Name, string Type, DateTime CreatedAtUtc);

public sealed record CreateEnrollmentTokenRequest(Guid EnvironmentId, string AgentName);
public sealed record EnrollmentTokenCreated(string Token, DateTime ExpiresAtUtc, Guid EnvironmentId, string AgentName);
public sealed record EnrollAgentRequest(string EnrollmentToken, string Name, string Version);
public sealed record EnrollAgentSuccess(Guid AgentId, Guid OrganizationId, Guid EnvironmentId, Guid ApplicationId, string AgentApiKey);
public sealed record HeartbeatRequest(string AgentVersion, string HealthState);

public abstract record EnrollResult
{
    public sealed record Ok(EnrollAgentSuccess Value) : EnrollResult;
    public sealed record Invalid() : EnrollResult;
    public sealed record Expired() : EnrollResult;
    public sealed record AlreadyUsed() : EnrollResult;
}

public sealed record AgentDto(
    Guid Id,
    string Name,
    string Version,
    string HealthState,
    string Connectivity,
    DateTime? LastSeenUtc,
    Guid EnvironmentId,
    string EnvironmentName,
    DateTime CreatedAtUtc,
    bool Revoked);

public sealed record HeartbeatDto(DateTime ReportedAtUtc, string AgentVersion, string HealthState);

public sealed record AgentDetailDto(AgentDto Agent, IReadOnlyList<HeartbeatDto> RecentHeartbeats);

public sealed record ExecutionQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? AgentId,
    Guid? EnvironmentId,
    string? Model,
    string? Status,
    int Page,
    int PageSize);

public sealed record ExecutionDto(
    Guid EventId,
    string RequestId,
    Guid AgentId,
    string AgentName,
    Guid EnvironmentId,
    string EnvironmentName,
    string Provider,
    string ModelName,
    int PromptTokens,
    int CompletionTokens,
    int? TimeToFirstTokenMs,
    int TotalLatencyMs,
    double? TokensPerSecond,
    string Status,
    string? ErrorCode,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc);

public sealed record OverviewDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int ExecutionCount,
    int FailedCount,
    double ErrorRate,
    long PromptTokens,
    long CompletionTokens,
    double? AverageLatencyMs,
    double? P95LatencyMs,
    int OnlineAgents,
    int AgentCount,
    int SucceededCount,
    int CancelledCount,
    long SucceededTokens,
    long FailedTokens,
    long CancelledTokens);

public sealed record AgentActivityDto(
    Guid AgentId,
    DateTime FromUtc,
    DateTime ToUtc,
    int ExecutionCount,
    int SucceededCount,
    int FailedCount,
    int CancelledCount,
    long PromptTokens,
    long CompletionTokens,
    long SucceededTokens,
    long FailedTokens,
    long CancelledTokens,
    double? AverageLatencyMs,
    double? P95LatencyMs,
    double? AverageTokensPerSecond,
    HardwarePointDto? LatestSample,
    string HardwareAttribution);

public sealed record ModelAnalyticsDto(
    string Provider,
    string ModelName,
    string Runtime,
    int ExecutionCount,
    int FailedCount,
    double ErrorRate,
    long TotalTokens,
    double? AverageLatencyMs);

public sealed record HardwarePointDto(
    DateTime SampledAtUtc,
    double CpuPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    double? GpuUtilizationPercent,
    long? GpuMemoryUsedBytes,
    long? GpuMemoryTotalBytes,
    string? GpuName,
    string CollectionSource);

public sealed record HardwareSeriesDto(string Attribution, Guid? AgentId, IReadOnlyList<HardwarePointDto> Points);

public sealed record AuditDto(Guid Id, string Action, string Target, DateTime TimestampUtc, Guid? ActorUserId, Guid? ActorAgentId);

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";
    public int MaxBatchSize { get; set; } = TelemetryLimits.MaxBatchSize;
    public int RetentionDays { get; set; } = 30;
    public int AuditRetentionDays { get; set; } = 90;
}
