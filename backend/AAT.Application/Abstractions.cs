using AAT.Telemetry;

namespace AAT.Application;

public interface IPasswordHashing
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ISecretGenerator
{
    string CreateEnrollmentToken();
    string CreateAgentCredential();
    string Hash(string secret);
}

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

public abstract record OperationResult<T>
{
    public sealed record Ok(T Value) : OperationResult<T>;
    public sealed record Fail(string Error) : OperationResult<T>;
}

public interface IUserAdminService
{
    Task<OperationResult<UserCreated>> CreateAsync(Guid organizationId, Guid actorUserId, CreateUserRequest request, CancellationToken cancellationToken);
}

public interface IEnvironmentService
{
    Task<IReadOnlyList<EnvironmentDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<OperationResult<EnvironmentDto>> CreateAsync(Guid organizationId, Guid actorUserId, CreateEnvironmentRequest request, CancellationToken cancellationToken);
}

public interface IAgentService
{
    Task<OperationResult<EnrollmentTokenCreated>> CreateEnrollmentTokenAsync(Guid organizationId, Guid actorUserId, CreateEnrollmentTokenRequest request, CancellationToken cancellationToken);
    Task<EnrollResult> EnrollAsync(EnrollAgentRequest request, CancellationToken cancellationToken);
    Task<bool> HeartbeatAsync(Guid agentId, HeartbeatRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<AgentDetailDto?> GetAsync(Guid organizationId, Guid agentId, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(Guid organizationId, Guid actorUserId, Guid agentId, CancellationToken cancellationToken);
}

public interface ITelemetryIngestionService
{
    Task<IngestionResultDto> IngestExecutionsAsync(Guid agentId, ExecutionBatchRequest batch, CancellationToken cancellationToken);
    Task<IngestionResultDto> IngestHardwareAsync(Guid agentId, HardwareBatchRequest batch, CancellationToken cancellationToken);
}

public interface IExecutionQueryService
{
    Task<PagedResult<ExecutionDto>> QueryAsync(Guid organizationId, ExecutionQuery query, CancellationToken cancellationToken);
}

public interface IAnalyticsService
{
    Task<OverviewDto> OverviewAsync(Guid organizationId, DateTime fromUtc, DateTime toUtc, Guid? environmentId, CancellationToken cancellationToken);
    Task<AgentActivityDto?> AgentActivityAsync(Guid organizationId, Guid agentId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelAnalyticsDto>> ModelsAsync(Guid organizationId, DateTime fromUtc, DateTime toUtc, Guid? environmentId, CancellationToken cancellationToken);
    Task<HardwareSeriesDto> HardwareAsync(Guid organizationId, Guid? agentId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}

public interface IAuditQueryService
{
    Task<PagedResult<AuditDto>> QueryAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken);
}
