using AAT.Application;
using AAT.Domain;
using AAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AAT.Infrastructure.Services;

public static class DevelopmentAdminSeed
{
    public const string UserName = "admin";
    public const string Password = "Admin@123";

    public static async Task EnsureAsync(AatDbContext db, IPasswordHashing passwords, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(user => user.Email == UserName, cancellationToken))
            return;

        var now = DateTime.UtcNow;
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "OMNIX AI",
            Status = OrganizationStatus.Active,
            CreatedAtUtc = now
        };
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Email = UserName,
            DisplayName = "Admin",
            PasswordHash = passwords.Hash(Password),
            Role = UserRole.Admin,
            CreatedAtUtc = now
        };
        db.Organizations.Add(organization);
        db.Users.Add(user);
        db.Environments.Add(new TenantEnvironment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Development",
            Type = EnvironmentType.Development,
            CreatedAtUtc = now
        });
        db.AuditLogs.Add(AuthService.Audit(organization.Id, user.Id, null, "user.registered", organization.Name, now));
        await db.SaveChangesAsync(cancellationToken);
    }
}
