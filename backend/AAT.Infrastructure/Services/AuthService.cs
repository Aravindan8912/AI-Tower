using AAT.Application;
using AAT.Domain;
using AAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAT.Infrastructure.Services;

public sealed class AuthService(
    AatDbContext db,
    IPasswordHashing passwords,
    TimeProvider time,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var organizationName = request.OrganizationName?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        var displayName = request.DisplayName?.Trim() ?? "";
        if (organizationName.Length is < 2 or > 120)
            return new AuthResult.Fail("Organization name must be between 2 and 120 characters.");
        if (!PasswordPolicy.IsEmail(email))
            return new AuthResult.Fail("A valid email address is required.");
        if (displayName.Length is < 1 or > 120)
            return new AuthResult.Fail("Display name must be between 1 and 120 characters.");
        var passwordError = PasswordPolicy.Validate(request.Password);
        if (passwordError is not null)
            return new AuthResult.Fail(passwordError);
        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
            return new AuthResult.Fail("Email is already registered.");

        var now = time.GetUtcNow().UtcDateTime;
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = organizationName,
            Status = OrganizationStatus.Active,
            CreatedAtUtc = now
        };
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Email = email,
            DisplayName = displayName,
            PasswordHash = passwords.Hash(request.Password!),
            Role = UserRole.Admin,
            CreatedAtUtc = now
        };
        var environment = new TenantEnvironment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Development",
            Type = EnvironmentType.Development,
            CreatedAtUtc = now
        };
        db.Organizations.Add(organization);
        db.Users.Add(user);
        db.Environments.Add(environment);
        db.AuditLogs.Add(Audit(organization.Id, user.Id, null, "user.registered", organization.Name, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            return new AuthResult.Fail("Email is already registered.");
        }

        logger.LogInformation("Registered organization {OrganizationId} and admin {UserId}", organization.Id, user.Id);
        return new AuthResult.Ok(new AuthSuccess(organization.Id, user.Id, user.Email, user.DisplayName, user.Role.ToString(), organization.Name));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);
        if (user is null || !passwords.Verify(request.Password ?? "", user.PasswordHash))
            return new AuthResult.Fail("Invalid email or password.");

        var organization = await db.Organizations.AsNoTracking().FirstAsync(org => org.Id == user.OrganizationId, cancellationToken);
        if (organization.Status != OrganizationStatus.Active)
            return new AuthResult.Fail("Invalid email or password.");

        var now = time.GetUtcNow().UtcDateTime;
        db.AuditLogs.Add(Audit(user.OrganizationId, user.Id, null, "user.login", "user", now));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} signed in", user.Id);
        return new AuthResult.Ok(new AuthSuccess(user.OrganizationId, user.Id, user.Email, user.DisplayName, user.Role.ToString(), organization.Name));
    }

    internal static AuditLog Audit(Guid organizationId, Guid? userId, Guid? agentId, string action, string target, DateTime timestampUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActorUserId = userId,
            ActorAgentId = agentId,
            Action = action,
            Target = target.Length > 200 ? target[..200] : target,
            TimestampUtc = timestampUtc
        };
}
