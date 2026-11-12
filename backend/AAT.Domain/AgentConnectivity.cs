namespace AAT.Domain;

public static class AgentConnectivity
{
    public static readonly TimeSpan OnlineThreshold = TimeSpan.FromSeconds(90);

    public static ConnectivityStatus Derive(DateTime? lastSeenUtc, DateTime utcNow)
    {
        if (lastSeenUtc is null)
            return ConnectivityStatus.Offline;

        var seen = DateTime.SpecifyKind(lastSeenUtc.Value, DateTimeKind.Utc);
        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return now - seen <= OnlineThreshold
            ? ConnectivityStatus.Online
            : ConnectivityStatus.Offline;
    }
}
