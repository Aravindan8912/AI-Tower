using System.Text.Json;

namespace AAT.Telemetry;

public static class TelemetryLimits
{
    public const int MaxBatchSize = 100;
    public const int MaxRequestBytes = 256 * 1024;
}

public static class TelemetryContract
{
    public static readonly HashSet<string> ExecutionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "eventId", "requestId", "applicationId", "provider", "model", "runtime",
        "promptTokens", "completionTokens", "timeToFirstTokenMs", "totalLatencyMs",
        "tokensPerSecond", "status", "errorCode", "startedAtUtc", "completedAtUtc"
    };

    public static readonly HashSet<string> HardwareFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "eventId", "sampledAtUtc", "cpuPercent", "memoryUsedBytes", "memoryTotalBytes",
        "gpuUtilizationPercent", "gpuMemoryUsedBytes", "gpuMemoryTotalBytes", "gpuName",
        "collectionSource"
    };

    public static IReadOnlyList<string> FindDisallowedBatchFields(JsonElement root, string batchProperty, ISet<string> itemFields)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return ["body"];

        var disallowed = new List<string>();
        foreach (var property in root.EnumerateObject())
        {
            if (!property.Name.Equals(batchProperty, StringComparison.OrdinalIgnoreCase))
            {
                disallowed.Add(property.Name);
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                disallowed.Add(property.Name);
                continue;
            }

            foreach (var item in property.Value.EnumerateArray())
                CollectItemFields(item, itemFields, disallowed);
        }

        return disallowed;
    }

    public static IReadOnlyList<string> FindDisallowedFields(JsonElement item, ISet<string> allowed)
    {
        var disallowed = new List<string>();
        CollectItemFields(item, allowed, disallowed);
        return disallowed;
    }

    private static void CollectItemFields(JsonElement item, ISet<string> allowed, List<string> disallowed)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            disallowed.Add("item");
            return;
        }

        foreach (var field in item.EnumerateObject())
        {
            if (!allowed.Contains(field.Name) || field.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                disallowed.Add(field.Name);
        }
    }
}
