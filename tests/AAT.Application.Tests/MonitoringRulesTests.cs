using System.Text.Json;
using AAT.Application;
using AAT.Domain;
using AAT.Telemetry;

namespace AAT.Application.Tests;

public class MonitoringRulesTests
{
    [Fact]
    public void Agent_is_online_only_inside_the_threshold()
    {
        var now = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(ConnectivityStatus.Online, AgentConnectivity.Derive(now.AddSeconds(-30), now));
        Assert.Equal(ConnectivityStatus.Offline, AgentConnectivity.Derive(now.AddMinutes(-5), now));
        Assert.Equal(ConnectivityStatus.Offline, AgentConnectivity.Derive(null, now));
    }

    [Fact]
    public void Queue_drops_the_oldest_item_when_full()
    {
        var queue = new BoundedTelemetryQueue<int>(2);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);

        Assert.Equal(1, queue.DroppedCount);
        Assert.Equal([2, 3], queue.DequeueBatch(10));
    }

    [Fact]
    public void Requeue_keeps_failed_items_ahead_and_still_drops_oldest()
    {
        var queue = new BoundedTelemetryQueue<int>(2);
        queue.Enqueue(2);
        queue.Enqueue(3);
        var taken = queue.DequeueBatch(1);

        queue.RequeueFront([.. taken, 4]);

        Assert.Equal(1, queue.DroppedCount);
        Assert.Equal([4, 3], queue.DequeueBatch(10));
    }

    [Fact]
    public void Retry_backoff_uses_full_jitter_under_the_cap()
    {
        var delay = RetryBackoff.Compute(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), 0.5);
        Assert.Equal(TimeSpan.FromSeconds(4), delay);
    }

    [Fact]
    public void Telemetry_contract_rejects_prompt_text_and_allows_token_counts()
    {
        using var forbidden = JsonDocument.Parse("""{"events":[{"eventId":"1","promptText":"hidden","promptTokens":3}]}""");
        using var allowed = JsonDocument.Parse("""{"events":[{"eventId":"1","promptTokens":3,"model":"llama3.2"}]}""");

        Assert.Contains("promptText", TelemetryContract.FindDisallowedBatchFields(forbidden.RootElement, "events", TelemetryContract.ExecutionFields));
        Assert.Empty(TelemetryContract.FindDisallowedBatchFields(allowed.RootElement, "events", TelemetryContract.ExecutionFields));
    }

    [Fact]
    public void Execution_validation_requires_metadata_only()
    {
        var errors = TelemetryValidation.ValidateExecution(new ExecutionEventDto
        {
            EventId = Guid.NewGuid(),
            RequestId = "req-1",
            Provider = "ollama",
            Model = "llama3.2",
            Status = "Succeeded",
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAtUtc = DateTimeOffset.UtcNow,
            PromptTokens = 4,
            CompletionTokens = 6,
            TotalLatencyMs = 100
        }, 0);

        Assert.Empty(errors);
        Assert.NotEmpty(TelemetryValidation.ValidateExecution(new ExecutionEventDto(), 0));
    }

    [Fact]
    public void Password_policy_requires_length_and_a_letter_and_digit()
    {
        Assert.NotNull(PasswordPolicy.Validate("short1"));
        Assert.NotNull(PasswordPolicy.Validate("long-enough-no-digit"));
        Assert.Null(PasswordPolicy.Validate("long-enough-password-1"));
    }

    [Fact]
    public void Token_rollup_separates_successful_and_unsuccessful_use()
    {
        var rollup = ExecutionAccounting.Summarize(
        [
            new ExecutionAccounting.Row(ExecutionStatus.Succeeded, 10, 5, 100),
            new ExecutionAccounting.Row(ExecutionStatus.Failed, 8, 0, 50),
            new ExecutionAccounting.Row(ExecutionStatus.Cancelled, 3, 1, 20),
            new ExecutionAccounting.Row(ExecutionStatus.Succeeded, 1, 1, 400)
        ]);

        Assert.Equal(4, rollup.Count);
        Assert.Equal(2, rollup.SucceededCount);
        Assert.Equal(1, rollup.FailedCount);
        Assert.Equal(1, rollup.CancelledCount);
        Assert.Equal(17, rollup.SucceededTokens);
        Assert.Equal(8, rollup.FailedTokens);
        Assert.Equal(4, rollup.CancelledTokens);
        Assert.Equal(12, rollup.UnsuccessfulTokens);
        Assert.Equal(29, rollup.TotalTokens);
        Assert.Equal(400, rollup.P95LatencyMs);
    }
}
