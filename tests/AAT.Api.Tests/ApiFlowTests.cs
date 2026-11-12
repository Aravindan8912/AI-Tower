using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace AAT.Api.Tests;

[Collection("api")]
public class ApiFlowTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_reports_the_database()
    {
        var response = await _client.GetAsync("/api/v1/health");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tenant_isolation_auth_validation_and_idempotency()
    {
        var orgA = await RegisterAsync("Org A");
        var orgB = await RegisterAsync("Org B");
        var viewer = await CreateUserAsync(orgA, "Viewer");
        var environmentId = await FirstEnvironmentAsync(orgA.Token);

        var viewerToken = await PostAsync(HttpMethod.Post, "/api/v1/agents/enrollment-tokens", viewer.Token, new { environmentId, agentName = "blocked" });
        Assert.Equal(HttpStatusCode.Forbidden, viewerToken.StatusCode);

        var tokenResponse = await PostAsync(HttpMethod.Post, "/api/v1/agents/enrollment-tokens", orgA.Token, new { environmentId, agentName = "local-observer" });
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var enrollmentToken = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

        var enroll = await _client.PostAsJsonAsync("/api/v1/agents/enroll", new { enrollmentToken, name = "local-observer", version = "0.1.0" });
        Assert.Equal(HttpStatusCode.OK, enroll.StatusCode);
        var enrolled = await enroll.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = enrolled.GetProperty("agentId").GetGuid();
        var agentKey = enrolled.GetProperty("agentApiKey").GetString()!;

        var reused = await _client.PostAsJsonAsync("/api/v1/agents/enroll", new { enrollmentToken, name = "local-observer", version = "0.1.0" });
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);

        using var agent = factory.CreateClient();
        agent.DefaultRequestHeaders.Add("X-Agent-Key", agentKey);
        var heartbeat = await agent.PostAsJsonAsync($"/api/v1/agents/{agentId}/heartbeat", new { agentVersion = "0.1.0", healthState = "Healthy" });
        Assert.Equal(HttpStatusCode.NoContent, heartbeat.StatusCode);

        var anonymousHeartbeat = await _client.PostAsJsonAsync($"/api/v1/agents/{agentId}/heartbeat", new { agentVersion = "0.1.0", healthState = "Healthy" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousHeartbeat.StatusCode);

        var agents = await GetAsync("/api/v1/agents", orgA.Token);
        Assert.Contains("Online", await agents.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var evt = Event();
        var first = await agent.PostAsJsonAsync("/api/v1/telemetry/executions/batch", new { events = new[] { evt } });
        var second = await agent.PostAsJsonAsync("/api/v1/telemetry/executions/batch", new { events = new[] { evt } });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, firstBody.GetProperty("accepted").GetInt32());
        Assert.Equal(1, secondBody.GetProperty("duplicates").GetInt32());

        var leaked = new StringContent("""{"events":[{"eventId":"00000000-0000-0000-0000-000000000099","promptText":"secret prompt","model":"llama3.2"}]}""", Encoding.UTF8, "application/json");
        var rejected = await agent.PostAsync("/api/v1/telemetry/executions/batch", leaked);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.DoesNotContain("secret prompt", await rejected.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var invalid = await agent.PostAsJsonAsync("/api/v1/telemetry/executions/batch", new { events = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var hardware = await agent.PostAsJsonAsync("/api/v1/telemetry/hardware/batch", new
        {
            samples = new[]
            {
                new
                {
                    eventId = Guid.NewGuid(),
                    sampledAtUtc = DateTimeOffset.UtcNow,
                    cpuPercent = 12.5,
                    memoryUsedBytes = 1024,
                    memoryTotalBytes = 2048,
                    collectionSource = "host"
                }
            }
        });
        Assert.Equal(HttpStatusCode.OK, hardware.StatusCode);

        var ownExecutions = await GetAsync("/api/v1/executions?page=1&pageSize=25", orgA.Token);
        var otherExecutions = await GetAsync("/api/v1/executions", orgB.Token);
        Assert.Contains(evt.RequestId, await ownExecutions.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain(evt.RequestId, await otherExecutions.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var overview = await GetAsync("/api/v1/analytics/overview", orgA.Token);
        var overviewBody = await overview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, overviewBody.GetProperty("executionCount").GetInt32());
        Assert.Equal(20, overviewBody.GetProperty("succeededTokens").GetInt64());
        Assert.Equal(0, overviewBody.GetProperty("failedTokens").GetInt64());
        Assert.Equal(0, overviewBody.GetProperty("cancelledTokens").GetInt64());

        var activity = await GetAsync($"/api/v1/analytics/agents/{agentId}", orgA.Token);
        Assert.Equal(HttpStatusCode.OK, activity.StatusCode);
        var activityBody = await activity.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, activityBody.GetProperty("executionCount").GetInt32());
        Assert.Equal(20, activityBody.GetProperty("succeededTokens").GetInt64());
        Assert.Contains("estimate", activityBody.GetProperty("hardwareAttribution").GetString(), StringComparison.OrdinalIgnoreCase);
        var hiddenActivity = await GetAsync($"/api/v1/analytics/agents/{agentId}", orgB.Token);
        Assert.Equal(HttpStatusCode.NotFound, hiddenActivity.StatusCode);

        var emptyEnvironment = await PostAsync(HttpMethod.Post, "/api/v1/environments", orgA.Token, new { name = "Empty", type = "Staging" });
        Assert.Equal(HttpStatusCode.Created, emptyEnvironment.StatusCode);
        var emptyEnvironmentId = (await emptyEnvironment.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var emptyOverview = await GetAsync($"/api/v1/analytics/overview?environmentId={emptyEnvironmentId}", orgA.Token);
        var emptyBody = await emptyOverview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, emptyBody.GetProperty("executionCount").GetInt32());
        Assert.Equal(0, emptyBody.GetProperty("agentCount").GetInt32());
        Assert.Equal(0, emptyBody.GetProperty("onlineAgents").GetInt32());
        var hardwareSeries = await GetAsync($"/api/v1/analytics/hardware?agentId={agentId}", orgA.Token);
        var hardwareBody = await hardwareSeries.Content.ReadAsStringAsync();
        Assert.Contains("estimate", hardwareBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("host", hardwareBody, StringComparison.Ordinal);
        var otherHardware = await GetAsync($"/api/v1/analytics/hardware?agentId={agentId}", orgB.Token);
        Assert.DoesNotContain("\"collectionSource\":\"host\"", await otherHardware.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var audit = await GetAsync("/api/v1/audit", orgA.Token);
        Assert.Contains("agent.enrolled", await audit.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private async Task<Auth> RegisterAsync(string organization)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            organizationName = organization,
            email,
            displayName = "Admin",
            password = "long-enough-password-1"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Auth(body.GetProperty("token").GetString()!, email);
    }

    private async Task<Auth> CreateUserAsync(Auth admin, string role)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var response = await PostAsync(HttpMethod.Post, "/api/v1/users", admin.Token, new
        {
            email,
            displayName = role,
            password = "long-enough-password-1",
            role
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "long-enough-password-1" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return new Auth(body.GetProperty("token").GetString()!, email);
    }

    private async Task<Guid> FirstEnvironmentAsync(string token)
    {
        var response = await GetAsync("/api/v1/environments", token);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body[0].GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> GetAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostAsync(HttpMethod method, string url, string token, object body)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static Execution Event() => new(Guid.NewGuid(), "req-" + Guid.NewGuid().ToString("N"), "ollama", "llama3.2", DateTimeOffset.UtcNow.AddMilliseconds(-20), DateTimeOffset.UtcNow);

    private sealed record Auth(string Token, string Email);

    private sealed record Execution(Guid EventId, string RequestId, string Provider, string Model, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc)
    {
        public int PromptTokens => 8;
        public int CompletionTokens => 12;
        public int TotalLatencyMs => 20;
        public string Status => "Succeeded";
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("aat")
        .WithUsername("aat")
        .WithPassword("aat_test_password")
        .Build();

    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Auth__JwtSigningKey", "integration-test-signing-key-32chars");
        Environment.SetEnvironmentVariable("Database__AutoMigrate", "true");
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__AatDatabase", _postgres.GetConnectionString());
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AatDatabase"] = _postgres.GetConnectionString(),
                ["Auth:JwtSigningKey"] = "integration-test-signing-key-32chars",
                ["Database:AutoMigrate"] = "true"
            });
        });
    }
}
