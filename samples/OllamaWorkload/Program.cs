using System.Net.Http.Json;
using System.Text.Json;
using AAT.Telemetry;

var model = ReadArg(args, "--model");
var prompt = ReadArg(args, "--prompt");
var ollama = ReadArg(args, "--ollama") ?? "http://127.0.0.1:11434";
var observer = ReadArg(args, "--observer") ?? "http://127.0.0.1:47321";
if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(prompt))
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/OllamaWorkload -- --model <name> --prompt <text> [--ollama <url>] [--observer <url>]");
    return 2;
}

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
var started = DateTimeOffset.UtcNow;
HttpResponseMessage response;
try
{
    response = await http.PostAsJsonAsync($"{ollama.TrimEnd('/')}/api/generate", new
    {
        model,
        prompt,
        stream = false
    });
}
catch (HttpRequestException)
{
    Console.Error.WriteLine("Ollama is not reachable. The local runtime must be running before this workload can record an execution.");
    return 1;
}

using (response)
{
    var completed = DateTimeOffset.UtcNow;
    if (!response.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Ollama returned {(int)response.StatusCode}. No telemetry was sent.");
        return 1;
    }

    await using var stream = await response.Content.ReadAsStreamAsync();
    using var document = await JsonDocument.ParseAsync(stream);
    var root = document.RootElement;
    var promptTokens = ReadInt(root, "prompt_eval_count");
    var completionTokens = ReadInt(root, "eval_count");
    var totalNs = ReadLong(root, "total_duration");
    var promptNs = ReadLong(root, "prompt_eval_duration");
    var evalNs = ReadLong(root, "eval_duration");
    var totalLatencyMs = (int)Math.Min(int.MaxValue, Math.Max(0, (completed - started).TotalMilliseconds));
    if (totalNs > 0)
        totalLatencyMs = (int)Math.Min(int.MaxValue, totalNs / 1_000_000);
    int? timeToFirstTokenMs = promptNs > 0 ? (int)Math.Min(int.MaxValue, promptNs / 1_000_000) : null;
    double? tokensPerSecond = evalNs > 0 ? completionTokens / (evalNs / 1_000_000_000d) : null;

    var evt = new ExecutionEventDto
    {
        EventId = Guid.NewGuid(),
        RequestId = Guid.NewGuid().ToString("N"),
        Provider = "ollama",
        Model = model,
        Runtime = "ollama",
        PromptTokens = promptTokens,
        CompletionTokens = completionTokens,
        TimeToFirstTokenMs = timeToFirstTokenMs,
        TotalLatencyMs = totalLatencyMs,
        TokensPerSecond = tokensPerSecond,
        Status = "Succeeded",
        StartedAtUtc = started,
        CompletedAtUtc = completed
    };

    try
    {
        using var observed = await http.PostAsJsonAsync($"{observer.TrimEnd('/')}/v1/executions", evt, TelemetryJson.Options);
        Console.WriteLine(observed.IsSuccessStatusCode
            ? $"Recorded metadata event {evt.EventId}. Prompt and completion text were not forwarded."
            : $"Ollama completed, but the observer rejected metadata ({(int)observed.StatusCode}). Inference was not blocked.");
    }
    catch (HttpRequestException)
    {
        Console.WriteLine("Ollama completed, but the observer was unreachable. Inference was not blocked.");
    }
}

return 0;

static string? ReadArg(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static int ReadInt(JsonElement element, string name) =>
    element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

static long ReadLong(JsonElement element, string name) =>
    element.TryGetProperty(name, out var value) && value.TryGetInt64(out var parsed) ? parsed : 0;
