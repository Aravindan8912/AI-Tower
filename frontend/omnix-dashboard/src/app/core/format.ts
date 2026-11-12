export function formatTokens(value: number): string {
  return Math.round(value).toLocaleString();
}

export function formatLatency(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${Math.round(value)} ms`;
}

export function formatRate(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${value.toFixed(1)} tok/s`;
}

export function formatBytes(value: number): string {
  if (!Number.isFinite(value) || value < 0) return '—';
  if (value < 1024) return `${Math.round(value)} B`;
  const units = ['KB', 'MB', 'GB', 'TB'];
  let size = value / 1024;
  let unit = 0;
  while (size >= 1024 && unit < units.length - 1) {
    size /= 1024;
    unit++;
  }
  return `${size >= 10 ? size.toFixed(0) : size.toFixed(1)} ${units[unit]}`;
}

export function secondsSince(iso: string | null, now = Date.now()): number | null {
  if (!iso) return null;
  const parsed = Date.parse(iso);
  if (Number.isNaN(parsed)) return null;
  return Math.max(0, Math.round((now - parsed) / 1000));
}

export function formatAgo(iso: string | null, now = Date.now()): string {
  const seconds = secondsSince(iso, now);
  if (seconds === null) return 'No heartbeat yet';
  if (seconds < 5) return 'just now';
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ${seconds % 60}s ago`;
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m ago`;
}

export function reachability(agent: { revoked: boolean; connectivity: string; lastSeenUtc: string | null }, now = Date.now()): string {
  if (agent.revoked) return 'Revoked. New heartbeats are rejected.';
  const seconds = secondsSince(agent.lastSeenUtc, now);
  if (agent.connectivity === 'Online' && seconds !== null)
    return `Online. Last heartbeat ${formatAgo(agent.lastSeenUtc, now)}, inside the 90 second window.`;
  if (seconds === null) return 'Offline. No heartbeat has been stored.';
  return `Offline. Last heartbeat ${formatAgo(agent.lastSeenUtc, now)}, past the 90 second window.`;
}
