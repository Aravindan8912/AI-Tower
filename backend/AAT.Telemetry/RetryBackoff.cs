namespace AAT.Telemetry;

public static class RetryBackoff
{
    public static TimeSpan Compute(int attempt, TimeSpan baseDelay, TimeSpan maxDelay, double jitterUnit)
    {
        var unit = Math.Clamp(jitterUnit, 0d, 1d);
        var exponent = Math.Clamp(attempt, 0, 16);
        var exp = baseDelay.TotalMilliseconds * Math.Pow(2, exponent);
        var capped = Math.Min(maxDelay.TotalMilliseconds, exp);
        return TimeSpan.FromMilliseconds(capped * unit);
    }
}
