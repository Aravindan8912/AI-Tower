using System.Net;
using System.Text.Json;
using AAT.Observer;
using AAT.Telemetry;

if (args.Contains("--enroll"))
    return await EnrollmentCommand.RunAsync(args);

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.Configure<ObserverOptions>(builder.Configuration.GetSection(ObserverOptions.SectionName));
builder.Services.AddHttpClient("aat", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<ObserverOptions>>().Value;
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("aat");
    return new TelemetryDispatcher(options, http, sp.GetRequiredService<ILogger<TelemetryDispatcher>>());
});
builder.Services.AddHostedService<TelemetryDispatchService>();
builder.Services.AddHostedService<HeartbeatService>();
builder.Services.AddHostedService<HardwareSampleService>();

var listenUrl = builder.Configuration[$"{ObserverOptions.SectionName}:LocalListenUrl"] ?? "http://127.0.0.1:47321";
builder.WebHost.UseUrls(listenUrl);

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "AAT.Observer" }));
app.MapGet("/metrics", (HttpContext http, TelemetryDispatcher dispatcher) =>
{
    if (!IsLoopback(http))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    return Results.Ok(new
    {
        queueDepth = dispatcher.Queue.Count,
        droppedEvents = dispatcher.DroppedCount,
        capacity = dispatcher.Queue.Capacity
    });
});
app.MapPost("/v1/executions", async (HttpContext http, TelemetryDispatcher dispatcher, CancellationToken cancellationToken) =>
{
    if (!IsLoopback(http))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    JsonDocument document;
    try
    {
        document = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: cancellationToken);
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Request body must be JSON." });
    }

    using (document)
    {
        var disallowed = TelemetryContract.FindDisallowedFields(document.RootElement, TelemetryContract.ExecutionFields);
        if (disallowed.Count > 0)
        {
            return Results.BadRequest(new
            {
                error = "Payload contains fields outside the telemetry contract.",
                fields = disallowed.Distinct(StringComparer.OrdinalIgnoreCase).Take(20)
            });
        }

        var evt = document.RootElement.Deserialize<ExecutionEventDto>(TelemetryJson.Options);
        if (evt is null)
            return Results.BadRequest(new { error = "Event body is required." });
        var errors = TelemetryValidation.ValidateExecution(evt, 0);
        if (errors.Count > 0)
            return Results.BadRequest(new { errors });
        dispatcher.RecordExecution(evt);
        return Results.Accepted();
    }
});

app.Run();
return 0;

static bool IsLoopback(HttpContext http) =>
    http.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address);

internal static class EnrollmentCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var tokenIndex = Array.IndexOf(args, "--enroll");
        if (tokenIndex < 0 || tokenIndex + 1 >= args.Length || string.IsNullOrWhiteSpace(args[tokenIndex + 1]))
        {
            Console.Error.WriteLine("Usage: dotnet run --project observer/AAT.Observer -- --enroll <token> [--name <agent-name>]");
            return 2;
        }

        var nameIndex = Array.IndexOf(args, "--name");
        var name = nameIndex >= 0 && nameIndex + 1 < args.Length ? args[nameIndex + 1] : Environment.MachineName;
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var api = configuration[$"{ObserverOptions.SectionName}:ApiBaseUrl"] ?? "http://localhost:5080";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var response = await http.PostAsJsonAsync($"{api.TrimEnd('/')}/api/v1/agents/enroll", new
        {
            enrollmentToken = args[tokenIndex + 1],
            name,
            version = "0.1.0"
        });
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Enrollment failed ({(int)response.StatusCode}). {body}");
            Console.Error.WriteLine("Create a new token on the Agents page and paste that full value. An old token cannot be reused.");
            return 1;
        }

        Console.WriteLine(body);
        Console.Error.WriteLine("Store agentId and agentApiKey in Observer configuration or environment variables. Do not commit them.");
        return 0;
    }
}
