using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AAT.Application;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AAT.API.Security;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public string JwtIssuer { get; set; } = "omnix-aat";
    public string JwtAudience { get; set; } = "omnix-dashboard";
    public string JwtSigningKey { get; set; } = "";
    public int TokenHours { get; set; } = 8;
}

public sealed class JwtTokenService(IOptions<AuthOptions> options, TimeProvider time)
{
    public string Create(AuthSuccess user)
    {
        var settings = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSigningKey));
        var token = new JwtSecurityToken(
            issuer: settings.JwtIssuer,
            audience: settings.JwtAudience,
            claims:
            [
                new Claim("sub", user.UserId.ToString()),
                new Claim("email", user.Email),
                new Claim("name", user.DisplayName),
                new Claim("organization_id", user.OrganizationId.ToString()),
                new Claim("organization_name", user.OrganizationName),
                new Claim("role", user.Role)
            ],
            notBefore: time.GetUtcNow().UtcDateTime,
            expires: time.GetUtcNow().UtcDateTime.AddHours(Math.Clamp(settings.TokenHours, 1, 24)),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetOrganizationId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("organization_id") ?? throw new UnauthorizedAccessException());

    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("sub") ?? throw new UnauthorizedAccessException());

    public static Guid GetAgentId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("agent_id") ?? throw new UnauthorizedAccessException());

    public static bool IsAdmin(this ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue("role"), "Admin", StringComparison.Ordinal);
}
