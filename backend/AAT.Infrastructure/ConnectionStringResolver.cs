using Microsoft.Extensions.Configuration;

namespace AAT.Infrastructure;

public static class ConnectionStringResolver
{
    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("AatDatabase");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var password = configuration["Database:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Database credentials are not configured. Set ConnectionStrings:AatDatabase or Database:Password.");
        }

        var host = configuration["Database:Host"] ?? "localhost";
        var port = configuration["Database:Port"] ?? "5432";
        var name = configuration["Database:Name"] ?? "aat";
        var username = configuration["Database:Username"] ?? "aat";
        return $"Host={host};Port={port};Database={name};Username={username};Password={password}";
    }
}
