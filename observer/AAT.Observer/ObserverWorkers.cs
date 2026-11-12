using AAT.Telemetry;

namespace AAT.Observer;

public sealed class TelemetryDispatchService(
    TelemetryDispatcher dispatcher,
    IOptions<ObserverOptions> options,
    ILogger<TelemetryDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(options.Value.FlushIntervalSeconds, 1, 300));
        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = interval;
            try
            {
                var result = await dispatcher.DispatchOnceAsync(stoppingToken);
                if (result == DispatchResult.Retry)
                    wait = dispatcher.NextDelay(Random.Shared.NextDouble());
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Telemetry dispatch failed");
                wait = dispatcher.NextDelay(Random.Shared.NextDouble());
            }

            try
            {
                await Task.Delay(wait, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

public sealed class HeartbeatService(IOptions<ObserverOptions> options, IHttpClientFactory httpClientFactory, ILogger<HeartbeatService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var interval = TimeSpan.FromSeconds(Math.Clamp(settings.HeartbeatIntervalSeconds, 5, 300));
        var client = httpClientFactory.CreateClient("aat");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (settings.AgentId != Guid.Empty && !string.IsNullOrWhiteSpace(settings.AgentKey))
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.ApiBaseUrl.TrimEnd('/')}/api/v1/agents/{settings.AgentId}/heartbeat");
                    request.Headers.TryAddWithoutValidation("X-Agent-Key", settings.AgentKey);
                    request.Content = JsonContent.Create(new { agentVersion = settings.AgentVersion, healthState = "Healthy" });
                    using var response = await client.SendAsync(request, stoppingToken);
                    if (!response.IsSuccessStatusCode)
                        logger.LogWarning("Heartbeat was not accepted ({StatusCode})", (int)response.StatusCode);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Heartbeat failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

public sealed class HardwareSampleService(TelemetryDispatcher dispatcher, IOptions<ObserverOptions> options, ILogger<HardwareSampleService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var state = new HostMetricsState();
        var interval = TimeSpan.FromSeconds(Math.Clamp(options.Value.HardwareSampleSeconds, 5, 300));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var sample = HostMetricsReader.Read(state);
                dispatcher.RecordHardware(new HardwareEventDto
                {
                    EventId = Guid.NewGuid(),
                    SampledAtUtc = DateTimeOffset.UtcNow,
                    CpuPercent = sample.CpuPercent,
                    MemoryUsedBytes = sample.MemoryUsedBytes,
                    MemoryTotalBytes = sample.MemoryTotalBytes,
                    GpuUtilizationPercent = sample.Gpu?.UtilizationPercent,
                    GpuMemoryUsedBytes = sample.Gpu?.MemoryUsedBytes,
                    GpuMemoryTotalBytes = sample.Gpu?.MemoryTotalBytes,
                    GpuName = sample.Gpu?.Name,
                    CollectionSource = sample.CollectionSource
                });
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Hardware sample failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
