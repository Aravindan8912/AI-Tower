using AAT.API.Security;
using AAT.Application;
using AAT.Domain;
using AAT.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AAT.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService auth, IUserAdminService users, JwtTokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request, cancellationToken);
        return result switch
        {
            AuthResult.Ok success => Ok(ToResponse(success.Value)),
            AuthResult.Fail failure => BadRequest(new { error = failure.Error }),
            _ => BadRequest(new { error = "Registration failed." })
        };
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return result switch
        {
            AuthResult.Ok success => Ok(ToResponse(success.Value)),
            _ => Unauthorized(new { error = "Invalid email or password." })
        };
    }

    [HttpPost("/api/v1/users")]
    [Authorize(Policy = "Admin")]
    [EnableRateLimiting("dashboard")]
    public async Task<ActionResult<UserCreated>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await users.CreateAsync(User.GetOrganizationId(), User.GetUserId(), request, cancellationToken);
        return result switch
        {
            OperationResult<UserCreated>.Ok success => Created($"/api/v1/users/{success.Value.UserId}", success.Value),
            OperationResult<UserCreated>.Fail failure => BadRequest(new { error = failure.Error }),
            _ => BadRequest(new { error = "User was not created." })
        };
    }

    private AuthResponse ToResponse(AuthSuccess success) =>
        new(tokens.Create(success), success.OrganizationId, success.UserId, success.Email, success.DisplayName, success.Role, success.OrganizationName);
}

public sealed record AuthResponse(
    string Token,
    Guid OrganizationId,
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    string OrganizationName);

[ApiController]
[Route("api/v1/environments")]
[Authorize(Policy = "Dashboard")]
[EnableRateLimiting("dashboard")]
public sealed class EnvironmentsController(IEnvironmentService environments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EnvironmentDto>>> List(CancellationToken cancellationToken) =>
        Ok(await environments.ListAsync(User.GetOrganizationId(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<EnvironmentDto>> Create(CreateEnvironmentRequest request, CancellationToken cancellationToken)
    {
        var result = await environments.CreateAsync(User.GetOrganizationId(), User.GetUserId(), request, cancellationToken);
        return result switch
        {
            OperationResult<EnvironmentDto>.Ok success => Created($"/api/v1/environments/{success.Value.Id}", success.Value),
            OperationResult<EnvironmentDto>.Fail failure => BadRequest(new { error = failure.Error }),
            _ => BadRequest(new { error = "Environment was not created." })
        };
    }
}

[ApiController]
[Route("api/v1/agents")]
public sealed class AgentsController(IAgentService agents) : ControllerBase
{
    [HttpPost("enrollment-tokens")]
    [Authorize(Policy = "Admin")]
    [EnableRateLimiting("dashboard")]
    public async Task<ActionResult<EnrollmentTokenCreated>> CreateToken(CreateEnrollmentTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await agents.CreateEnrollmentTokenAsync(User.GetOrganizationId(), User.GetUserId(), request, cancellationToken);
        return result switch
        {
            OperationResult<EnrollmentTokenCreated>.Ok success => Ok(success.Value),
            OperationResult<EnrollmentTokenCreated>.Fail failure => BadRequest(new { error = failure.Error }),
            _ => BadRequest(new { error = "Enrollment token was not created." })
        };
    }

    [HttpPost("enroll")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<EnrollAgentSuccess>> Enroll(EnrollAgentRequest request, CancellationToken cancellationToken)
    {
        var result = await agents.EnrollAsync(request, cancellationToken);
        return result switch
        {
            EnrollResult.Ok success => Ok(success.Value),
            EnrollResult.Expired => Unauthorized(new { error = "Enrollment token has expired." }),
            EnrollResult.AlreadyUsed => Conflict(new { error = "Enrollment token has already been used." }),
            _ => Unauthorized(new { error = "Enrollment token is invalid." })
        };
    }

    [HttpPost("{agentId:guid}/heartbeat")]
    [Authorize(Policy = "Agent")]
    [EnableRateLimiting("agent")]
    public async Task<IActionResult> Heartbeat(Guid agentId, HeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (User.GetAgentId() != agentId)
            return Forbid();
        var updated = await agents.HeartbeatAsync(agentId, request, cancellationToken);
        return updated ? NoContent() : BadRequest(new { error = "Heartbeat was rejected." });
    }

    [HttpPost("{agentId:guid}/revoke")]
    [Authorize(Policy = "Admin")]
    [EnableRateLimiting("dashboard")]
    public async Task<IActionResult> Revoke(Guid agentId, CancellationToken cancellationToken)
    {
        var revoked = await agents.RevokeAsync(User.GetOrganizationId(), User.GetUserId(), agentId, cancellationToken);
        return revoked ? NoContent() : NotFound();
    }

    [HttpGet]
    [Authorize(Policy = "Dashboard")]
    [EnableRateLimiting("dashboard")]
    public async Task<ActionResult<IReadOnlyList<AgentDto>>> List(CancellationToken cancellationToken) =>
        Ok(await agents.ListAsync(User.GetOrganizationId(), cancellationToken));

    [HttpGet("{agentId:guid}")]
    [Authorize(Policy = "Dashboard")]
    [EnableRateLimiting("dashboard")]
    public async Task<ActionResult<AgentDetailDto>> Get(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await agents.GetAsync(User.GetOrganizationId(), agentId, cancellationToken);
        return agent is null ? NotFound() : Ok(agent);
    }
}

[ApiController]
[Route("api/v1/telemetry")]
[Authorize(Policy = "Agent")]
[EnableRateLimiting("agent")]
[RequestSizeLimit(TelemetryLimits.MaxRequestBytes)]
public sealed class TelemetryController(ITelemetryIngestionService ingestion) : ControllerBase
{
    [HttpPost("executions/batch")]
    public async Task<ActionResult<IngestionResultDto>> Executions(ExecutionBatchRequest batch, CancellationToken cancellationToken)
    {
        var result = await ingestion.IngestExecutionsAsync(User.GetAgentId(), batch, cancellationToken);
        return ToAction(result);
    }

    [HttpPost("hardware/batch")]
    public async Task<ActionResult<IngestionResultDto>> Hardware(HardwareBatchRequest batch, CancellationToken cancellationToken)
    {
        var result = await ingestion.IngestHardwareAsync(User.GetAgentId(), batch, cancellationToken);
        return ToAction(result);
    }

    private ActionResult<IngestionResultDto> ToAction(IngestionResultDto result)
    {
        if (result.Conflict)
            return Conflict(new { error = result.BatchError });
        if (result.BatchRejected)
            return BadRequest(new { error = result.BatchError });
        return Ok(result);
    }
}

[ApiController]
[Route("api/v1")]
[Authorize(Policy = "Dashboard")]
[EnableRateLimiting("dashboard")]
public sealed class ReadModelsController(
    IExecutionQueryService executions,
    IAnalyticsService analytics,
    IAuditQueryService audits) : ControllerBase
{
    [HttpGet("executions")]
    public async Task<ActionResult<PagedResult<ExecutionDto>>> Executions(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? agentId,
        [FromQuery] Guid? environmentId,
        [FromQuery] string? model,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!QueryWindow.TryResolve(fromUtc, toUtc, out var from, out var to, out var error))
            return BadRequest(new { error });
        if (!string.IsNullOrWhiteSpace(status) && !Enum.TryParse<ExecutionStatus>(status, true, out _))
            return BadRequest(new { error = "Status must be Succeeded, Failed, or Cancelled." });

        var result = await executions.QueryAsync(
            User.GetOrganizationId(),
            new ExecutionQuery(from, to, agentId, environmentId, model, status, page < 1 ? 1 : page, pageSize == 0 ? 25 : Math.Clamp(pageSize, 1, 100)),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("analytics/overview")]
    public async Task<ActionResult<OverviewDto>> Overview(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? environmentId,
        CancellationToken cancellationToken)
    {
        if (!QueryWindow.TryResolve(fromUtc, toUtc, out var from, out var to, out var error))
            return BadRequest(new { error });
        return Ok(await analytics.OverviewAsync(User.GetOrganizationId(), from, to, environmentId, cancellationToken));
    }

    [HttpGet("analytics/agents/{agentId:guid}")]
    public async Task<ActionResult<AgentActivityDto>> AgentActivity(
        Guid agentId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        if (!QueryWindow.TryResolve(fromUtc, toUtc, out var from, out var to, out var error))
            return BadRequest(new { error });
        var activity = await analytics.AgentActivityAsync(User.GetOrganizationId(), agentId, from, to, cancellationToken);
        return activity is null ? NotFound() : Ok(activity);
    }

    [HttpGet("analytics/models")]
    public async Task<ActionResult<IReadOnlyList<ModelAnalyticsDto>>> Models(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? environmentId,
        CancellationToken cancellationToken)
    {
        if (!QueryWindow.TryResolve(fromUtc, toUtc, out var from, out var to, out var error))
            return BadRequest(new { error });
        return Ok(await analytics.ModelsAsync(User.GetOrganizationId(), from, to, environmentId, cancellationToken));
    }

    [HttpGet("analytics/hardware")]
    public async Task<ActionResult<HardwareSeriesDto>> Hardware(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? agentId,
        CancellationToken cancellationToken)
    {
        if (!QueryWindow.TryResolve(fromUtc, toUtc, out var from, out var to, out var error))
            return BadRequest(new { error });
        return Ok(await analytics.HardwareAsync(User.GetOrganizationId(), agentId, from, to, cancellationToken));
    }

    [HttpGet("audit")]
    public async Task<ActionResult<PagedResult<AuditDto>>> Audit([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await audits.QueryAsync(User.GetOrganizationId(), page < 1 ? 1 : page, pageSize == 0 ? 25 : Math.Clamp(pageSize, 1, 100), cancellationToken));
}

public static class QueryWindow
{
    public static bool TryResolve(DateTime? fromUtc, DateTime? toUtc, out DateTime from, out DateTime to, out string? error)
    {
        if (fromUtc?.Kind == DateTimeKind.Local || toUtc?.Kind == DateTimeKind.Local)
        {
            from = default;
            to = default;
            error = "Timestamps must be UTC.";
            return false;
        }

        to = SpecifyUtc(toUtc ?? DateTime.UtcNow);
        from = SpecifyUtc(fromUtc ?? to.AddHours(-24));
        if (from >= to)
        {
            error = "fromUtc must be earlier than toUtc.";
            return false;
        }

        error = null;
        return true;
    }

    private static DateTime SpecifyUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
