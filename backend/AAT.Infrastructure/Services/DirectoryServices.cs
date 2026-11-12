using AAT.Application;
using AAT.Domain;
using AAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AAT.Infrastructure.Services;

public sealed class UserAdminService(AatDbContext db, IPasswordHashing passwords, TimeProvider time) : IUserAdminService
{
    public async Task<OperationResult<UserCreated>> CreateAsync(Guid organizationId, Guid actorUserId, CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        var displayName = request.DisplayName?.Trim() ?? "";
        if (!PasswordPolicy.IsEmail(email))
            return new OperationResult<UserCreated>.Fail("A valid email address is required.");
        if (displayName.Length is < 1 or > 120)
            return new OperationResult<UserCreated>.Fail("Display name must be between 1 and 120 characters.");
        var passwordError = PasswordPolicy.Validate(request.Password);
        if (passwordError is not null)
            return new OperationResult<UserCreated>.Fail(passwordError);
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || request.Role is null)
            return new OperationResult<UserCreated>.Fail("Role must be Admin or Viewer.");
        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
            return new OperationResult<UserCreated>.Fail("Email is already registered.");

        var now = time.GetUtcNow().UtcDateTime;
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = email,
            DisplayName = displayName,
            PasswordHash = passwords.Hash(request.Password!),
            Role = role,
            CreatedAtUtc = now
        };
        db.Users.Add(user);
        db.AuditLogs.Add(AuthService.Audit(organizationId, actorUserId, null, "user.created", email, now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            return new OperationResult<UserCreated>.Fail("Email is already registered.");
        }

        return new OperationResult<UserCreated>.Ok(new UserCreated(user.Id, user.Email, user.Role.ToString()));
    }
}

public sealed class EnvironmentService(AatDbContext db, TimeProvider time) : IEnvironmentService
{
    public async Task<IReadOnlyList<EnvironmentDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await db.Environments.AsNoTracking()
            .Where(environment => environment.OrganizationId == organizationId)
            .OrderBy(environment => environment.Name)
            .Select(environment => new EnvironmentDto(environment.Id, environment.Name, environment.Type.ToString(), environment.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult<EnvironmentDto>> CreateAsync(Guid organizationId, Guid actorUserId, CreateEnvironmentRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 2 or > 120)
            return new OperationResult<EnvironmentDto>.Fail("Environment name must be between 2 and 120 characters.");
        if (!Enum.TryParse<EnvironmentType>(request.Type, true, out var type))
            return new OperationResult<EnvironmentDto>.Fail("Environment type must be Development, Staging, Production, or Custom.");
        if (await db.Environments.AnyAsync(environment => environment.OrganizationId == organizationId && environment.Name == name, cancellationToken))
            return new OperationResult<EnvironmentDto>.Fail("An environment with that name already exists.");

        var now = time.GetUtcNow().UtcDateTime;
        var environment = new TenantEnvironment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Type = type,
            CreatedAtUtc = now
        };
        db.Environments.Add(environment);
        db.AuditLogs.Add(AuthService.Audit(organizationId, actorUserId, null, "environment.created", name, now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            return new OperationResult<EnvironmentDto>.Fail("An environment with that name already exists.");
        }

        return new OperationResult<EnvironmentDto>.Ok(new EnvironmentDto(environment.Id, environment.Name, environment.Type.ToString(), environment.CreatedAtUtc));
    }
}
