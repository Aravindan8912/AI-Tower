namespace AAT.Telemetry;

public sealed class ExecutionEventDto
{
    public Guid EventId { get; set; }
    public string RequestId { get; set; } = "";
    public Guid? ApplicationId { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string? Runtime { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int? TimeToFirstTokenMs { get; set; }
    public int TotalLatencyMs { get; set; }
    public double? TokensPerSecond { get; set; }
    public string Status { get; set; } = "";
    public string? ErrorCode { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
}

public sealed class ExecutionBatchRequest
{
    public List<ExecutionEventDto> Events { get; set; } = [];
}

public sealed class HardwareEventDto
{
    public Guid EventId { get; set; }
    public DateTimeOffset SampledAtUtc { get; set; }
    public double CpuPercent { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long MemoryTotalBytes { get; set; }
    public double? GpuUtilizationPercent { get; set; }
    public long? GpuMemoryUsedBytes { get; set; }
    public long? GpuMemoryTotalBytes { get; set; }
    public string? GpuName { get; set; }
    public string CollectionSource { get; set; } = "";
}

public sealed class HardwareBatchRequest
{
    public List<HardwareEventDto> Samples { get; set; } = [];
}

public sealed class IngestionResultDto
{
    public bool BatchRejected { get; set; }
    public bool Conflict { get; set; }
    public string? BatchError { get; set; }
    public int Accepted { get; set; }
    public int Duplicates { get; set; }
    public int Rejected { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = [];
}
