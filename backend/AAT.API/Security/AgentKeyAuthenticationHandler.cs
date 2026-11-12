using System.Security.Claims;
using System.Text.Encodings.Web;
using AAT.Application;
using AAT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAT.API.Security;

public sealed class AgentKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AatDbContext db,
    ISecretGenerator secrets)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgentKey";
    public const string HeaderName = "X-Agent-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrWhiteSpace(provided))
            return AuthenticateResult.NoResult();

        var hash = secrets.Hash(provided.ToString());
        var agent = await db.Agents.AsNoTracking()
            .FirstOrDefaultAsync(item => item.CredentialHash == hash && item.RevokedAtUtc == null);
        if (agent is null)
            return AuthenticateResult.Fail("Invalid agent key.");

        var claims = new[]
        {
            new Claim("agent_id", agent.Id.ToString()),
            new Claim("organization_id", agent.OrganizationId.ToString())
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
