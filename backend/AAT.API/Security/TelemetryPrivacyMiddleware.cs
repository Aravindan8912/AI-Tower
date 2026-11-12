using System.Text.Json;
using AAT.Telemetry;

namespace AAT.API.Security;

public sealed class TelemetryPrivacyMiddleware(RequestDelegate next, ILogger<TelemetryPrivacyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsTelemetryPost(context.Request))
        {
            await next(context);
            return;
        }

        if (context.Request.ContentLength > TelemetryLimits.MaxRequestBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new { error = "Telemetry payload is too large." });
            return;
        }

        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(context.RequestAborted);
        context.Request.Body.Position = 0;
        if (string.IsNullOrWhiteSpace(body))
        {
            await next(context);
            return;
        }

        if (EncodingByteCount(body) > TelemetryLimits.MaxRequestBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new { error = "Telemetry payload is too large." });
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Request body must be JSON." });
            return;
        }

        using (document)
        {
            var batchProperty = context.Request.Path.Value?.Contains("hardware", StringComparison.OrdinalIgnoreCase) == true
                ? "samples"
                : "events";
            var allowed = batchProperty == "samples" ? TelemetryContract.HardwareFields : TelemetryContract.ExecutionFields;
            var disallowed = TelemetryContract.FindDisallowedBatchFields(document.RootElement, batchProperty, allowed);
            if (disallowed.Count > 0)
            {
                var fields = disallowed.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
                logger.LogWarning("Rejected telemetry payload with disallowed fields {Fields}", string.Join(",", fields));
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Payload contains fields outside the telemetry contract.",
                    fields
                });
                return;
            }
        }

        await next(context);
    }

    private static bool IsTelemetryPost(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && request.Path.StartsWithSegments("/api/v1/telemetry");

    private static int EncodingByteCount(string body) => System.Text.Encoding.UTF8.GetByteCount(body);
}
