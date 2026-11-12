namespace AAT.Observer;

public sealed class ObserverOptions
{
    public const string SectionName = "Observer";
    public string ApiBaseUrl { get; set; } = "http://localhost:5080";
    public Guid AgentId { get; set; }
    public string AgentKey { get; set; } = "";
    public int QueueCapacity { get; set; } = 1000;
    public int BatchSize { get; set; } = 50;
    public int FlushIntervalSeconds { get; set; } = 5;
    public int RetryBaseSeconds { get; set; } = 1;
    public int RetryMaxSeconds { get; set; } = 60;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int HardwareSampleSeconds { get; set; } = 15;
    public string LocalListenUrl { get; set; } = "http://127.0.0.1:47321";
    public string AgentVersion { get; set; } = "0.1.0";
}
