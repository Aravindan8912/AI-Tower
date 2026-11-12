using System.Security.Cryptography;
using System.Text;
using AAT.Application;
using AAT.Domain;
using Microsoft.AspNetCore.Identity;

namespace AAT.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHashing
{
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new UserAccount(), password);

    public bool Verify(string password, string hash) =>
        _hasher.VerifyHashedPassword(new UserAccount(), hash, password) is not PasswordVerificationResult.Failed;
}

public sealed class SecretGenerator : ISecretGenerator
{
    public string CreateEnrollmentToken() => "aat_enroll_" + CreateRaw();

    public string CreateAgentCredential() => "aat_agent_" + CreateRaw();

    public string Hash(string secret)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string CreateRaw()
    {
        var encoded = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
