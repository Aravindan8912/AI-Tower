export interface Session {
  token: string;
  organizationId: string;
  organizationName: string;
  userId: string;
  email: string;
  displayName: string;
  role: 'Admin' | 'Viewer';
}

export interface EnvironmentItem {
  id: string;
  name: string;
  type: string;
  createdAtUtc: string;
}

export interface AgentItem {
  id: string;
  name: string;
  version: string;
  healthState: string;
  connectivity: string;
  lastSeenUtc: string | null;
  environmentId: string;
  environmentName: string;
  createdAtUtc: string;
  revoked: boolean;
}

export interface AgentDetail {
  agent: AgentItem;
  recentHeartbeats: { reportedAtUtc: string; agentVersion: string; healthState: string }[];
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ExecutionItem {
  eventId: string;
  requestId: string;
  agentId: string;
  agentName: string;
  environmentId: string;
  environmentName: string;
  provider: string;
  modelName: string;
  promptTokens: number;
  completionTokens: number;
  timeToFirstTokenMs: number | null;
  totalLatencyMs: number;
  tokensPerSecond: number | null;
  status: string;
  errorCode: string | null;
  startedAtUtc: string;
  completedAtUtc: string;
}

export interface Overview {
  fromUtc: string;
  toUtc: string;
  executionCount: number;
  failedCount: number;
  errorRate: number;
  promptTokens: number;
  completionTokens: number;
  averageLatencyMs: number | null;
  p95LatencyMs: number | null;
  onlineAgents: number;
  agentCount: number;
  succeededCount: number;
  cancelledCount: number;
  succeededTokens: number;
  failedTokens: number;
  cancelledTokens: number;
}

export interface AgentActivity {
  agentId: string;
  fromUtc: string;
  toUtc: string;
  executionCount: number;
  succeededCount: number;
  failedCount: number;
  cancelledCount: number;
  promptTokens: number;
  completionTokens: number;
  succeededTokens: number;
  failedTokens: number;
  cancelledTokens: number;
  averageLatencyMs: number | null;
  p95LatencyMs: number | null;
  averageTokensPerSecond: number | null;
  latestSample: HardwarePoint | null;
  hardwareAttribution: string;
}

export interface ModelAnalytic {
  provider: string;
  modelName: string;
  runtime: string;
  executionCount: number;
  failedCount: number;
  errorRate: number;
  totalTokens: number;
  averageLatencyMs: number | null;
}

export interface HardwarePoint {
  sampledAtUtc: string;
  cpuPercent: number;
  memoryUsedBytes: number;
  memoryTotalBytes: number;
  gpuUtilizationPercent: number | null;
  gpuMemoryUsedBytes: number | null;
  gpuMemoryTotalBytes: number | null;
  gpuName: string | null;
  collectionSource: string;
}

export interface HardwareSeries {
  attribution: string;
  agentId: string | null;
  points: HardwarePoint[];
}

export interface AuditItem {
  id: string;
  action: string;
  target: string;
  timestampUtc: string;
  actorUserId: string | null;
  actorAgentId: string | null;
}
