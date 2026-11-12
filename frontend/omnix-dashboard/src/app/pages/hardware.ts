import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { interval, merge, of, startWith, switchMap } from 'rxjs';
import { ApiService } from '../core/api.service';
import { errorMessage } from '../core/auth.interceptor';
import { EnvironmentContext } from '../core/environment-context';
import { formatBytes } from '../core/format';
import { AgentItem, HardwareSeries } from '../core/models';

@Component({
  selector: 'app-hardware',
  imports: [DatePipe, DecimalPipe, FormsModule],
  template: `
    <h2 class="text-2xl font-semibold">Hardware</h2>
    <p class="mt-3 rounded border border-amber-700/60 bg-amber-950/40 p-3 text-sm">{{ attribution() }}</p>
    <label class="mt-4 block text-sm">Agent
      <select class="mt-1 rounded border border-line bg-ink px-2 py-2" name="agent" [ngModel]="agentId()" (ngModelChange)="agentId.set($event)">
        @for (agent of visibleAgents(); track agent.id) {
          <option [value]="agent.id">{{ agent.name }}</option>
        }
      </select>
    </label>
    @if (error()) {
      <p class="mt-4 text-sm text-red-300">{{ error() }}</p>
    }
    <svg viewBox="0 0 100 36" class="mt-4 h-40 w-full rounded border border-line bg-panel" role="img" aria-label="CPU percent over time">
      <polyline [attr.points]="cpuLine()" fill="none" stroke="#3cbfb0" stroke-width="0.8" />
    </svg>
    <p class="mt-1 text-xs text-muted">CPU percent across the selected samples.</p>
    <div class="mt-4 overflow-x-auto rounded border border-line">
      <table class="w-full text-left text-sm">
        <thead class="bg-panel text-muted">
          <tr>
            <th class="px-3 py-2">Sampled (UTC)</th>
            <th class="px-3 py-2">CPU</th>
            <th class="px-3 py-2">Memory</th>
            <th class="px-3 py-2">GPU</th>
            <th class="px-3 py-2">Source</th>
          </tr>
        </thead>
        <tbody>
          @for (point of series()?.points ?? []; track point.sampledAtUtc) {
            <tr class="border-t border-line">
              <td class="px-3 py-2">{{ point.sampledAtUtc | date: 'yyyy-MM-dd HH:mm:ss' : 'UTC' }}</td>
              <td class="px-3 py-2">{{ point.cpuPercent | number: '1.0-1' }}%</td>
              <td class="px-3 py-2 tabular-nums">{{ bytes(point.memoryUsedBytes) }} / {{ bytes(point.memoryTotalBytes) }}</td>
              <td class="px-3 py-2">{{ point.gpuName ?? '—' }} {{ point.gpuUtilizationPercent === null ? '' : (point.gpuUtilizationPercent | number: '1.0-0') + '%' }}</td>
              <td class="px-3 py-2">{{ point.collectionSource }}</td>
            </tr>
          } @empty {
            <tr><td class="px-3 py-6 text-muted" colspan="5">No hardware samples in the last 24 hours.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class HardwarePage {
  private readonly api = inject(ApiService);
  private readonly environments = inject(EnvironmentContext);
  readonly agents = signal<AgentItem[]>([]);
  readonly visibleAgents = computed(() => {
    const selected = this.environments.environmentId();
    const items = this.agents();
    return selected ? items.filter((agent) => agent.environmentId === selected) : items;
  });
  readonly series = signal<HardwareSeries | null>(null);
  readonly error = signal('');
  readonly attribution = computed(
    () => this.series()?.attribution ?? 'Host-level samples. Overlap with a request window is an estimate, not exact per-request GPU or CPU ownership.',
  );
  readonly agentId = signal('');

  constructor() {
    effect(() => {
      const items = this.visibleAgents();
      if (!items.some((agent) => agent.id === this.agentId()))
        this.agentId.set(items[0]?.id ?? '');
    });
    this.api.agents().subscribe((items) => {
      this.agents.set(items.filter((agent) => !agent.revoked));
    });
    merge(interval(15000), toObservable(this.agentId))
      .pipe(
        startWith(0),
        switchMap(() => (this.agentId() ? this.api.hardware({ agentId: this.agentId() }) : of(null))),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (series) => {
          this.series.set(series);
          this.error.set('');
        },
        error: (err: unknown) => this.error.set(errorMessage(err)),
      });
  }

  cpuLine(): string {
    const points = this.series()?.points ?? [];
    if (points.length === 0) return '';
    return points
      .map((point, index) => {
        const x = points.length === 1 ? 0 : (index / (points.length - 1)) * 100;
        const y = 34 - Math.max(0, Math.min(100, point.cpuPercent)) * 0.32;
        return `${x},${y}`;
      })
      .join(' ');
  }

  bytes(value: number): string {
    return formatBytes(value);
  }
}
