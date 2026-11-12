using System.Net;
using AAT.Observer;
using AAT.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAT.Observer.Tests;

public class ObserverBehaviorTests
{
    [Fact]
    public void Recording_does_not_call_the_api()
    {
        var handler = new ScriptedHandler(_ => throw new InvalidOperationException("Network should not be called."));
        var dispatcher = Create(handler);

        dispatcher.RecordExecution(ValidEvent());

        Assert.Equal(0, handler.Calls);
        Assert.Equal(1, dispatcher.Queue.Count);
    }

    [Fact]
    public async Task Failed_upload_is_requeued_and_a_later_success_clears_it()
    {
        var handler = new ScriptedHandler(request =>
        {
            var code = request.RequestUri!.AbsolutePath.Contains("executions", StringComparison.Ordinal)
                && request.GetHashCode() == request.GetHashCode()
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK;
            return new HttpResponseMessage(code);
        });
        var attempts = 0;
        handler.Responder = _ =>
        {
            attempts++;
            return new HttpResponseMessage(attempts == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        };
        var dispatcher = Create(handler);
        dispatcher.RecordExecution(ValidEvent());

        Assert.Equal(DispatchResult.Retry, await dispatcher.DispatchOnceAsync(CancellationToken.None));
        Assert.Equal(1, dispatcher.Queue.Count);
        Assert.Equal(DispatchResult.Sent, await dispatcher.DispatchOnceAsync(CancellationToken.None));
        Assert.Equal(0, dispatcher.Queue.Count);
    }

    [Fact]
    public async Task Concurrent_conflict_is_retried()
    {
        var attempts = 0;
        var handler = new ScriptedHandler(_ =>
        {
            attempts++;
            return new HttpResponseMessage(attempts == 1 ? HttpStatusCode.Conflict : HttpStatusCode.OK);
        });
        var dispatcher = Create(handler);
        dispatcher.RecordExecution(ValidEvent());

        Assert.Equal(DispatchResult.Retry, await dispatcher.DispatchOnceAsync(CancellationToken.None));
        Assert.Equal(1, dispatcher.Queue.Count);
        Assert.Equal(DispatchResult.Sent, await dispatcher.DispatchOnceAsync(CancellationToken.None));
        Assert.Equal(0, dispatcher.Queue.Count);
    }

    [Fact]
    public async Task Non_retryable_client_error_is_discarded()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var dispatcher = Create(handler);
        dispatcher.RecordExecution(ValidEvent());

        Assert.Equal(DispatchResult.Sent, await dispatcher.DispatchOnceAsync(CancellationToken.None));
        Assert.Equal(0, dispatcher.Queue.Count);
    }

    [Fact]
    public void Cpu_and_gpu_parsers_are_explicit_about_units()
    {
        Assert.Equal(25, HostMetricsReader.CpuPercent(75, 100));
        Assert.True(HostMetricsReader.TryParseNvidiaSmi("Example GPU, 12, 1024, 24564", out var sample));
        Assert.Equal(12, sample!.UtilizationPercent);
        Assert.Equal(1024L * 1024L * 1024L, sample.MemoryUsedBytes);
    }

    private static TelemetryDispatcher Create(ScriptedHandler handler)
    {
        var options = new ObserverOptions
        {
            ApiBaseUrl = "http://localhost:5080",
            AgentId = Guid.NewGuid(),
            AgentKey = "aat_agent_test",
            QueueCapacity = 10,
            BatchSize = 10
        };
        return new TelemetryDispatcher(options, new HttpClient(handler), NullLogger.Instance);
    }

    private static ExecutionEventDto ValidEvent() => new()
    {
        EventId = Guid.NewGuid(),
        RequestId = "req",
        Provider = "ollama",
        Model = "llama3.2",
        Status = "Succeeded",
        PromptTokens = 1,
        CompletionTokens = 1,
        TotalLatencyMs = 10,
        StartedAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(-10),
        CompletedAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => Responder = responder;
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; }
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Responder(request));
        }
    }
}
