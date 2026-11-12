using AAT.Application;
using AAT.Infrastructure.Persistence;
using AAT.Infrastructure.Security;
using AAT.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AAT.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAatInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ConnectionStringResolver.Resolve(configuration);
        services.AddDbContext<AatDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        services.Configure<TelemetryOptions>(configuration.GetSection(TelemetryOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHashing, Pbkdf2PasswordHasher>();
        services.AddSingleton<ISecretGenerator, SecretGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IEnvironmentService, EnvironmentService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<ITelemetryIngestionService, TelemetryIngestionService>();
        services.AddScoped<IExecutionQueryService, ExecutionQueryService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        return services;
    }
}
