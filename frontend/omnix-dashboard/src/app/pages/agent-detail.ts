import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, interval, startWith, switchMap } from 'rxjs';
import { ApiService } from '../core/api.service';
import { errorMessage } from '../core/auth.interceptor';
import { formatAgo, formatBytes, formatLatency, formatRate, formatTokens, reachability, secondsSince } from '../core/format';
import { AgentActivity, AgentDetail, ExecutionItem } from '../core/models';
import { SessionStore } from '../core/session';

@Component({
  selector: 'app-agent-detail',
  imports: [DatePipe, DecimalPipe, RouterLink],
  template: `
    <a class="text-sm text-accent" routerLink="/agents">Back to agents</a>
    @if (error()) {
      <p class="mt-4 text-sm text-red-300">{{ error() }}</p>
    }
    @if (detail(); as item) {
      <div class="mt-3 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 class="text-2xl font-semibold">{{ item.agent.name }}</h2>
          <p class="mt-1 text-sm text-muted">{{ item.agent.environmentName }} · version {{ item.agent.version }}</p>
        </div>
        @if (session.session()?.role === 'Admin' && !item.agent.revoked) {
          <button class="rounded border border-red-400 px-3 py-2 text-sm text-red-300" type="button" (click)="revoke()">Revoke credential</button>
        }
      </div>

      <dl class="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div class="rounded border border-line bg-panel p-3">
          <dt class="text-xs text-muted">Reachability</dt>
          <dd class="mt-1 text-lg" [class]="item.agent.revoked ? 'text-red-300' : item.agent.connectivity === 'Online' ? 'text-accent' : 'text-amber-200'">{{ item.agent.revoked ? 'Revoked' : item.agent.connectivity }}</dd>
          <dd class="mt-1 text-xs text-muted">{{ reachability(item.agent) }}</dd>
        </div>
        <div class="rounded border border-line bg-panel p-3">
          <dt class="text-xs text-muted">Reported health</dt>
          <dd class="mt-1 text-lg">{{ item.agent.healthState }}</dd>
          <dd class="mt-1 text-xs text-muted">Sent by the agent. This is separate from the 90 second online rule.</dd>
        </div>
        <div class="rounded border border-line bg-panel p-3">
          <dt class="text-xs text-muted">Last heartbeat</dt>
          <dd class="mt-1 text-lg tabular-nums">{{ ago(item.agent.lastSeenUtc) }}</dd>
          <dd class="mt-1 text-xs text-muted">{{ item.agent.lastSeenUtc ? (item.agent.lastSeenUtc | date: 'yyyy-MM-dd HH:mm:ss' : 'UTC') + ' UTC' : 'None stored' }}</dd>
        </div>
        <div class="rounded border border-line bg-panel p-3">
          <dt class="text-xs text-muted">Agent id</dt>
          <dd class="mt-1 break-all font-mono text-xs">{{ item.agent.id }}</dd>
          <dd class="mt-1 text-xs text-muted">Enrolled {{ item.agent.createdAtUtc | date: 'yyyy-MM-dd HH:mm' : 'UTC' }} UTC</dd>
        </div>
      </dl>

      @if (activity(); as usage) {
        <h3 class="mt-8 text-lg">This agent, last 24 hours</h3>
        <div class="mt-3 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <article class="rounded border border-line p-3">
            <p class="text-xs text-muted">Executions</p>
            <p class="mt-1 text-2xl tabular-nums">{{ usage.executionCount }}</p>
            <p class="text-xs text-muted">{{ usage.succeededCount }} succeeded · {{ usage.failedCount }} failed · {{ usage.cancelledCount }} cancelled</p>
          </article>
          <article class="rounded border border-line p-3">
            <p class="text-xs text-muted">Tokens used</p>
            <p class="mt-1 text-2xl tabular-nums">{{ tokens(usage.promptTokens + usage.completionTokens) }}</p>
            <p class="text-xs text-muted">{{ tokens(usage.promptTokens) }} prompt · {{ tokens(usage.completionTokens) }} completion</p>
          </article>
          <article class="rounded border border-line p-3">
            <p class="text-xs text-muted">Tokens without a success</p>
            <p class="mt-1 text-2xl tabular-nums">{{ tokens(usage.failedTokens + usage.cancelledTokens) }}</p>
            <p class="text-xs text-muted">{{ tokens(usage.failedTokens) }} failed · {{ tokens(usage.cancelledTokens) }} cancelled</p>
          </article>
          <article class="rounded border border-line p-3">
            <p class="text-xs text-muted">Speed</p>
            <p class="mt-1 text-2xl tabular-nums">{{ latency(usage.averageLatencyMs) }}</p>
            <p class="text-xs text-muted">P95 {{ latency(usage.p95LatencyMs) }} · {{ rate(usage.averageTokensPerSecond) }}</p>
          </article>
        </div>
        <p class="mt-3 text-sm text-muted">
          @if (usage.failedTokens + usage.cancelledTokens > 0) {
            {{ tokens(usage.failedTokens + usage.cancelledTokens) }} of this agent's tokens did not finish as a success. A repeat of those calls spends them again. This tower does not retry the model.
          } @else if (usage.executionCount > 0) {
            All of this agent's recorded calls succeeded.
          } @else {
            No executions from this agent in the window.
          }
        </p>

        <h3 class="mt-8 text-lg">Latest host sample</h3>
        <p class="mt-1 text-sm text-muted">{{ usage.hardwareAttribution }}</p>
        @if (usage.latestSample; as sample) {
          <dl class="mt-3 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div class="rounded border border-line p-3"><dt class="text-xs text-muted">CPU</dt><dd class="mt-1 tabular-nums">{{ sample.cpuPercent | number: '1.0-1' }}%</dd></div>
            <div class="rounded border border-line p-3"><dt class="text-xs text-muted">Memory</dt><dd class="mt-1 tabular-nums">{{ bytes(sample.memoryUsedBytes) }} / {{ bytes(sample.memoryTotalBytes) }}</dd></div>
            <div class="rounded border border-line p-3">
              <dt class="text-xs text-muted">GPU</dt>
              <dd class="mt-1">{{ sample.gpuName ?? 'No GPU sample' }}</dd>
              <dd class="text-xs text-muted">{{ sample.gpuUtilizationPercent === null ? 'Utilization not reported' : (sample.gpuUtilizationPercent | number: '1.0-0') + '%' }}</dd>
            </div>
            <div class="rounded border border-line p-3">
              <dt class="text-xs text-muted">Sampled</dt>
              <dd class="mt-1 tabular-nums">{{ ago(sample.sampledAtUtc) }}</dd>
              <dd class="text-xs text-muted">{{ sample.sampledAtUtc | date: 'yyyy-MM-dd HH:mm:ss' : 'UTC' }} UTC · {{ sample.collectionSource }}{{ sampleAge(sample.sampledAtUtc) > 120 ? ' · not a live reading' : '' }}</dd>
            </div>
          </dl>
        } @else {
          <p class="mt-3 text-sm text-muted">No host sample stored for this agent.</p>
        }
      }

      <h3 class="mt-8 text-lg">Recent executions</h3>
      <div class="mt-3 overflow-x-auto rounded border border-line">
        <table class="w-full text-left text-sm">
          <thead class="bg-panel text-muted">
            <tr>
              <th class="px-3 py-2">Started (UTC)</th>
              <th class="px-3 py-2">Model</th>
              <th class="px-3 py-2">Tokens</th>
              <th class="px-3 py-2">Latency</th>
              <th class="px-3 py-2">Status</th>
            </tr>
          </thead>
          <tbody>
            @for (row of executions(); track row.eventId) {
              <tr class="border-t border-line">
                <td class="px-3 py-2">{{ row.startedAtUtc | date: 'yyyy-MM-dd HH:mm:ss' : 'UTC' }}</td>
                <td class="px-3 py-2">{{ row.provider }}/{{ row.modelName }}</td>
                <td class="px-3 py-2 tabular-nums">{{ row.promptTokens }} / {{ row.completionTokens }}</td>
                <td class="px-3 py-2 tabular-nums">{{ row.totalLatencyMs }} ms</td>
                <td class="px-3 py-2">{{ row.status }}{{ row.errorCode ? ' · ' + row.errorCode : '' }}</td>
              </tr>
            } @empty {
              <tr><td class="px-3 py-6 text-muted" colspan="5">No executions for this agent in the last 24 hours.</td></tr>
            }
          </tbody>
        </table>
      </div>

      <h3 class="mt-8 text-lg">Recent heartbeats</h3>
      <p class="mt-1 text-sm text-muted">The observer sends one about every 30 seconds. A gap longer than 90 seconds is what marks the agent offline.</p>
      <ul class="mt-2 space-y-2 text-sm">
        @for (beat of item.recentHeartbeats; track beat.reportedAtUtc + beat.healthState; let index = $index) {
          <li class="rounded border border-line px-3 py-2">
            {{ beat.reportedAtUtc | date: 'yyyy-MM-dd HH:mm:ss' : 'UTC' }} UTC · {{ beat.healthState }} · {{ beat.agentVersion }}
            <span class="text-muted"> · {{ gap(item.recentHeartbeats, index) }}</span>
          </li>
        } @empty {
          <li class="text-muted">No heartbeats stored yet.</li>
        }
      </ul>
    }
  `,
})
export class AgentDetailPage {
  readonly session = inject(SessionStore);
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  readonly detail = signal<AgentDetail | null>(null);
  readonly activity = signal<AgentActivity | null>(null);
  readonly executions = signal<ExecutionItem[]>([]);
  readonly error = signal('');
  readonly now = signal(Date.now());

  constructor() {
    const id = this.route.snapshot.paramMap.get('agentId');
    if (!id) return;
    interval(15000)
      .pipe(
        startWith(0),
        switchMap(() =>
          forkJoin({
            detail: this.api.agent(id),
            activity: this.api.agentActivity(id),
            executions: this.api.executions({ agentId: id, page: 1, pageSize: 8 }),
          }),
        ),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (result) => {
          this.detail.set(result.detail);
          this.activity.set(result.activity);
          this.executions.set(result.executions.items);
          this.now.set(Date.now());
          this.error.set('');
        },
        error: (err: unknown) => this.error.set(errorMessage(err)),
      });
  }

  ago(value: string | null): string {
    return formatAgo(value, this.now());
  }

  reachability(agent: AgentDetail['agent']): string {
    return reachability(agent, this.now());
  }

  tokens(value: number): string {
    return formatTokens(value);
  }

  latency(value: number | null): string {
    return formatLatency(value);
  }

  rate(value: number | null): string {
    return formatRate(value);
  }

  bytes(value: number): string {
    return formatBytes(value);
  }

  sampleAge(value: string): number {
    return secondsSince(value, this.now()) ?? 0;
  }

  gap(beats: AgentDetail['recentHeartbeats'], index: number): string {
    const newer = beats[index - 1];
    if (!newer) return 'latest';
    const seconds = Math.round((Date.parse(newer.reportedAtUtc) - Date.parse(beats[index].reportedAtUtc)) / 1000);
    if (!Number.isFinite(seconds) || seconds < 0) return 'gap unknown';
    return `${seconds}s before the next heartbeat`;
  }

  revoke(): void {
    const id = this.detail()?.agent.id;
    if (!id) return;
    this.api.revokeAgent(id).subscribe({
      next: () => {
        const current = this.detail();
        if (current) this.detail.set({ ...current, agent: { ...current.agent, revoked: true, connectivity: 'Offline' } });
      },
      error: (err: unknown) => this.error.set(errorMessage(err)),
    });
  }
}
