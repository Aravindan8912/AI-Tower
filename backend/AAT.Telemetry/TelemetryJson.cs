using System.Text.Json;
using System.Text.Json.Serialization;

namespace AAT.Telemetry;

public static class TelemetryJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
