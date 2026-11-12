using System.Text.RegularExpressions;

namespace AAT.Telemetry;

public static partial class TelemetryValidation
{
    public static IReadOnlyList<string> ValidateExecution(ExecutionEventDto evt, int index)
    {
        var errors = new List<string>();
        var path = $"events[{index}]";
        if (evt.EventId == Guid.Empty)
            errors.Add($"{path}.eventId is required.");
        if (string.IsNullOrWhiteSpace(evt.RequestId) || evt.RequestId.Trim().Length > 128)
            errors.Add($"{path}.requestId is required and must be at most 128 characters.");
        if (string.IsNullOrWhiteSpace(evt.Provider) || evt.Provider.Trim().Length > 64)
            errors.Add($"{path}.provider is required and must be at most 64 characters.");
        if (string.IsNullOrWhiteSpace(evt.Model) || evt.Model.Trim().Length > 128)
            errors.Add($"{path}.model is required and must be at most 128 characters.");
        if (evt.Runtime is { } runtime && runtime.Trim().Length > 64)
            errors.Add($"{path}.runtime must be at most 64 characters.");
        if (evt.PromptTokens < 0)
            errors.Add($"{path}.promptTokens must be zero or greater.");
        if (evt.CompletionTokens < 0)
            errors.Add($"{path}.completionTokens must be zero or greater.");
        if (evt.TimeToFirstTokenMs is < 0)
            errors.Add($"{path}.timeToFirstTokenMs must be zero or greater.");
        if (evt.TotalLatencyMs < 0)
            errors.Add($"{path}.totalLatencyMs must be zero or greater.");
        if (evt.TokensPerSecond is double rate && (double.IsNaN(rate) || double.IsInfinity(rate) || rate < 0))
            errors.Add($"{path}.tokensPerSecond must be a non-negative finite number.");
        if (!TryCanonicalStatus(evt.Status, out _))
            errors.Add($"{path}.status must be Succeeded, Failed, or Cancelled.");
        if (evt.ErrorCode is { } code && !ErrorCodePattern().IsMatch(code))
            errors.Add($"{path}.errorCode must be 1-64 characters of letters, digits, dot, underscore, or hyphen.");
        ValidateTimestamp(evt.StartedAtUtc, $"{path}.startedAtUtc", errors);
        ValidateTimestamp(evt.CompletedAtUtc, $"{path}.completedAtUtc", errors);
        if (evt.CompletedAtUtc < evt.StartedAtUtc)
            errors.Add($"{path}.completedAtUtc must be at or after startedAtUtc.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateHardware(HardwareEventDto sample, int index)
    {
        var errors = new List<string>();
        var path = $"samples[{index}]";
        if (sample.EventId == Guid.Empty)
            errors.Add($"{path}.eventId is required.");
        ValidateTimestamp(sample.SampledAtUtc, $"{path}.sampledAtUtc", errors);
        if (sample.CpuPercent is < 0 or > 100)
            errors.Add($"{path}.cpuPercent must be between 0 and 100.");
        if (sample.MemoryUsedBytes < 0 || sample.MemoryTotalBytes <= 0 || sample.MemoryUsedBytes > sample.MemoryTotalBytes)
            errors.Add($"{path}.memory values must be non-negative and used memory cannot exceed total memory.");
        if (sample.GpuUtilizationPercent is < 0 or > 100)
            errors.Add($"{path}.gpuUtilizationPercent must be between 0 and 100.");
        if (sample.GpuMemoryUsedBytes is < 0 || sample.GpuMemoryTotalBytes is < 0)
            errors.Add($"{path}.gpu memory values must be zero or greater.");
        if (sample.GpuMemoryUsedBytes is long used && sample.GpuMemoryTotalBytes is long total && used > total)
            errors.Add($"{path}.gpuMemoryUsedBytes cannot exceed gpuMemoryTotalBytes.");
        if (sample.GpuName is { } name && name.Trim().Length > 128)
            errors.Add($"{path}.gpuName must be at most 128 characters.");
        if (string.IsNullOrWhiteSpace(sample.CollectionSource) || sample.CollectionSource.Trim().Length > 64)
            errors.Add($"{path}.collectionSource is required and must be at most 64 characters.");
        return errors;
    }

    public static bool TryCanonicalStatus(string? status, out string canonical)
    {
        canonical = "";
        if (string.IsNullOrWhiteSpace(status))
            return false;
        if (status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
            canonical = "Succeeded";
        else if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            canonical = "Failed";
        else if (status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            canonical = "Cancelled";
        else
            return false;
        return true;
    }

    private static void ValidateTimestamp(DateTimeOffset value, string path, List<string> errors)
    {
        var utc = value.UtcDateTime;
        var earliest = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var latest = DateTime.UtcNow.AddDays(1);
        if (value == default || utc < earliest || utc > latest)
            errors.Add($"{path} must be a UTC timestamp.");
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,64}$")]
    private static partial Regex ErrorCodePattern();
}
