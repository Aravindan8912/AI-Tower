using AAT.Domain;

namespace AAT.Application;

public static class ExecutionAccounting
{
    public readonly record struct Row(ExecutionStatus Status, int PromptTokens, int CompletionTokens, int TotalLatencyMs);

    public readonly record struct Rollup(
        int Count,
        int SucceededCount,
        int FailedCount,
        int CancelledCount,
        long PromptTokens,
        long CompletionTokens,
        long SucceededTokens,
        long FailedTokens,
        long CancelledTokens,
        double? AverageLatencyMs,
        double? P95LatencyMs)
    {
        public long TotalTokens => PromptTokens + CompletionTokens;

        public long UnsuccessfulTokens => FailedTokens + CancelledTokens;
    }

    public static Rollup Summarize(IReadOnlyList<Row> rows)
    {
        long prompt = 0;
        long completion = 0;
        long succeededTokens = 0;
        long failedTokens = 0;
        long cancelledTokens = 0;
        var succeeded = 0;
        var failed = 0;
        var cancelled = 0;
        var latencies = new int[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var tokens = (long)row.PromptTokens + row.CompletionTokens;
            prompt += row.PromptTokens;
            completion += row.CompletionTokens;
            latencies[index] = row.TotalLatencyMs;
            switch (row.Status)
            {
                case ExecutionStatus.Succeeded:
                    succeeded++;
                    succeededTokens += tokens;
                    break;
                case ExecutionStatus.Failed:
                    failed++;
                    failedTokens += tokens;
                    break;
                case ExecutionStatus.Cancelled:
                    cancelled++;
                    cancelledTokens += tokens;
                    break;
            }
        }

        Array.Sort(latencies);
        return new Rollup(
            rows.Count,
            succeeded,
            failed,
            cancelled,
            prompt,
            completion,
            succeededTokens,
            failedTokens,
            cancelledTokens,
            rows.Count == 0 ? null : latencies.Average(),
            Percentile(latencies, 0.95));
    }

    public static double? Percentile(IReadOnlyList<int> sortedAscending, double percentile)
    {
        if (sortedAscending.Count == 0)
            return null;
        var rank = (int)Math.Ceiling(percentile * sortedAscending.Count) - 1;
        return sortedAscending[Math.Clamp(rank, 0, sortedAscending.Count - 1)];
    }
}
