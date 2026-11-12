namespace AAT.Domain;

public enum OrganizationStatus
{
    Active = 1,
    Suspended = 2
}

public enum EnvironmentType
{
    Development = 1,
    Staging = 2,
    Production = 3,
    Custom = 4
}

public enum UserRole
{
    Admin = 1,
    Viewer = 2
}

public enum AgentHealthState
{
    Healthy = 1,
    Degraded = 2,
    Unhealthy = 3
}

public enum ExecutionStatus
{
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3
}

public enum ConnectivityStatus
{
    Online = 1,
    Offline = 2
}
