using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using AAT.API.Security;
using AAT.Application;
using AAT.Infrastructure;
using AAT.Infrastructure.Services;
using AAT.Infrastructure.Persistence;
using AAT.Worker;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

ApplyLocalDevelopmentDefaults();

var builder = WebApplication.CreateBuilder(args);
var signingKey = builder.Configuration["Auth:JwtSigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Auth:JwtSigningKey must be at least 32 characters.");

builder.Services.AddAatInfrastructure(builder.Configuration);
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHostedService<TelemetryRetentionService>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgresql");
builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddOpenApi();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024 * 1024);

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = authOptions.JwtIssuer,
            ValidAudience = authOptions.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, AgentKeyAuthenticationHandler>(
        AgentKeyAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Dashboard", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("organization_id");
    });
    options.AddPolicy("Admin", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("role", "Admin");
    });
    options.AddPolicy("Agent", policy =>
    {
        policy.AddAuthenticationSchemes(AgentKeyAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("agent_id");
    });
});

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("dashboard", policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded." }, token);
    };
    options.AddPolicy("auth", httpContext => Fixed(Partition(httpContext, "auth"), 30));
    options.AddPolicy("agent", httpContext => Fixed(Partition(httpContext, "agent"), 120));
    options.AddPolicy("dashboard", httpContext => Fixed(Partition(httpContext, "dashboard"), 300));
});

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Database:AutoMigrate", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<AatDbContext>();
    await database.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
    {
        await DevelopmentAdminSeed.EnsureAsync(
            database,
            scope.ServiceProvider.GetRequiredService<IPasswordHashing>(),
            CancellationToken.None);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler(handler =>
    {
        handler.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
        });
    });
}

app.UseCors("dashboard");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<TelemetryPrivacyMiddleware>();
app.MapControllers();
app.MapHealthChecks("/api/v1/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            service = "AAT.API",
            checkedAtUtc = DateTime.UtcNow,
            checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString() })
        });
    }
});
app.Run();

static string Partition(HttpContext httpContext, string name)
{
    var identity = httpContext.User.FindFirst("agent_id")?.Value
        ?? httpContext.User.FindFirst("sub")?.Value
        ?? httpContext.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
    return name + ":" + identity;
}

static RateLimitPartition<string> Fixed(string key, int permitLimit) =>
    RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    });

static void ApplyLocalDevelopmentDefaults()
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (!string.IsNullOrEmpty(environment) && !environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        return;

    var fileValues = ReadEnvFile();
    Prefer("Database__Password", fileValues.GetValueOrDefault("POSTGRES_PASSWORD"), "local-dev-password", 8);
    Prefer("Database__Username", fileValues.GetValueOrDefault("POSTGRES_USER"), "aat", 1);
    Prefer("Database__Name", fileValues.GetValueOrDefault("POSTGRES_DB"), "aat", 1);
    Prefer("Auth__JwtSigningKey", fileValues.GetValueOrDefault("AAT_JWT_SIGNING_KEY"), "local-dev-signing-key-32-characters", 32);
}

static void Prefer(string variable, string? preferred, string fallback, int minLength)
{
    var current = Environment.GetEnvironmentVariable(variable);
    if (!IsPlaceholder(current, minLength))
        return;

    var next = !IsPlaceholder(preferred, minLength) ? preferred! : fallback;
    Environment.SetEnvironmentVariable(variable, next);
}

static bool IsPlaceholder(string? value, int minLength) =>
    string.IsNullOrWhiteSpace(value)
    || value.Contains('<')
    || value.Contains('>')
    || value.Contains("replace-with", StringComparison.OrdinalIgnoreCase)
    || value.Length < minLength;

static Dictionary<string, string> ReadEnvFile()
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, ".env");
            if (File.Exists(path))
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                        continue;
                    var split = line.IndexOf('=');
                    if (split <= 0)
                        continue;
                    values[line[..split].Trim()] = line[(split + 1)..].Trim();
                }

                return values;
            }

            dir = dir.Parent;
        }
    }

    return values;
}

public partial class Program;
