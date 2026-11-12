using System.Net;
using System.Net.Http.Json;
using AAT.Telemetry;
using Microsoft.Extensions.Logging;

namespace AAT.Observer;

public enum DispatchResult
{
    Idle,
    NotConfigured,
    Sent,
    Retry
}

public abstract record OutboundItem
{
    public sealed record Execution(ExecutionEventDto Event) : OutboundItem;
    public sealed record Hardware(HardwareEventDto Sample) : OutboundItem;
}

public sealed class RetryableTelemetryException : Exception;

/// <summary>
/// Buffers telemetry and uploads it in the background. Callers of <see cref="RecordExecution"/> never wait on the network.
/// When the queue is full, the oldest item is dropped. HTTP 429 and 5xx responses are requeued. Other 4xx responses are discarded.
/// </summary>
public sealed class TelemetryDispatcher
{
    private readonly HttpClient _http;
    private readonly ObserverOptions _options;
    private readonly ILogger _logger;
    private int _failures;

    public TelemetryDispatcher(ObserverOptions options, HttpClient http, ILogger logger)
    {
        _options = options;
        _http = http;
        _logger = logger;
        Queue = new BoundedTelemetryQueue<OutboundItem>(Math.Clamp(options.QueueCapacity, 1, 100_000));
    }

    public BoundedTelemetryQueue<OutboundItem> Queue { get; }
    public long DroppedCount => Queue.DroppedCount;

    public void RecordExecution(ExecutionEventDto evt) => Queue.Enqueue(new OutboundItem.Execution(evt));

    public void RecordHardware(HardwareEventDto sample) => Queue.Enqueue(new OutboundItem.Hardware(sample));

    public async Task<DispatchResult> DispatchOnceAsync(CancellationToken cancellationToken)
    {
        if (_options.AgentId == Guid.Empty || string.IsNullOrWhiteSpace(_options.AgentKey))
            return DispatchResult.NotConfigured;

        var batch = Queue.DequeueBatch(Math.Clamp(_options.BatchSize, 1, TelemetryLimits.MaxBatchSize));
        if (batch.Count == 0)
            return DispatchResult.Idle;

        var executions = batch.OfType<OutboundItem.Execution>().Select(item => item.Event).ToList();
        var hardware = batch.OfType<OutboundItem.Hardware>().Select(item => item.Sample).ToList();
        try
        {
            if (executions.Count > 0)
            {
                await PostAsync("/api/v1/telemetry/executions/batch", new ExecutionBatchRequest { Events = executions }, cancellationToken);
            }

            if (hardware.Count > 0)
            {
                await PostAsync("/api/v1/telemetry/hardware/batch", new HardwareBatchRequest { Samples = hardware }, cancellationToken);
            }

            _failures = 0;
            return DispatchResult.Sent;
        }
        catch (RetryableTelemetryException)
        {
            Queue.RequeueFront(batch);
            _failures++;
            _logger.LogWarning("Telemetry upload will retry. Queue depth {QueueDepth}, dropped {Dropped}", Queue.Count, Queue.DroppedCount);
            return DispatchResult.Retry;
        }
    }

    public TimeSpan NextDelay(double jitterUnit)
    {
        var attempt = Math.Max(0, _failures - 1);
        var delay = RetryBackoff.Compute(
            attempt,
            TimeSpan.FromSeconds(Math.Max(1, _options.RetryBaseSeconds)),
            TimeSpan.FromSeconds(Math.Max(1, _options.RetryMaxSeconds)),
            jitterUnit);
        return delay < TimeSpan.FromMilliseconds(200) ? TimeSpan.FromMilliseconds(200) : delay;
    }

    private async Task PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/"), path.TrimStart('/')));
        request.Headers.TryAddWithoutValidation("X-Agent-Key", _options.AgentKey);
        request.Content = JsonContent.Create(body, options: TelemetryJson.Options);
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new RetryableTelemetryException();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RetryableTelemetryException();
        }

        using (response)
        {
            var code = (int)response.StatusCode;
            if (code is (int)HttpStatusCode.TooManyRequests or (int)HttpStatusCode.Conflict || code >= 500)
                throw new RetryableTelemetryException();
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Discarded telemetry batch after non-retryable status {StatusCode}", code);
        }
    }
}
