import { PercentPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { ApiService } from '../core/api.service';
import { errorMessage } from '../core/auth.interceptor';
import { EnvironmentContext } from '../core/environment-context';
import { ModelAnalytic } from '../core/models';

@Component({
  selector: 'app-models',
  imports: [PercentPipe],
  template: `
    <h2 class="text-2xl font-semibold">Models</h2>
    <p class="mt-1 text-sm text-muted">Last 24 hours. Share is this model's tokens divided by all tokens in the window.</p>
    @if (error()) {
      <p class="mt-4 text-sm text-red-300">{{ error() }}</p>
    }
    <div class="mt-4 overflow-x-auto rounded border border-line">
      <table class="w-full text-left text-sm">
        <thead class="bg-panel text-muted">
          <tr>
            <th class="px-3 py-2">Model</th>
            <th class="px-3 py-2">Runtime</th>
            <th class="px-3 py-2">Executions</th>
            <th class="px-3 py-2">Error rate</th>
            <th class="px-3 py-2">Tokens</th>
            <th class="px-3 py-2">Share</th>
            <th class="px-3 py-2">Avg latency</th>
          </tr>
        </thead>
        <tbody>
          @for (item of items(); track item.provider + item.modelName) {
            <tr class="border-t border-line">
              <td class="px-3 py-2">{{ item.provider }}/{{ item.modelName }}</td>
              <td class="px-3 py-2">{{ item.runtime }}</td>
              <td class="px-3 py-2">{{ item.executionCount }}</td>
              <td class="px-3 py-2">{{ item.errorRate | percent: '1.0-1' }}</td>
              <td class="px-3 py-2 tabular-nums">{{ item.totalTokens.toLocaleString() }}</td>
              <td class="px-3 py-2 tabular-nums">{{ share(item) | percent: '1.0-0' }}</td>
              <td class="px-3 py-2">{{ latency(item.averageLatencyMs) }}</td>
            </tr>
          } @empty {
            <tr><td class="px-3 py-6 text-muted" colspan="7">No model activity in this window.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class ModelsPage {
  private readonly api = inject(ApiService);
  private readonly environments = inject(EnvironmentContext);
  readonly items = signal<ModelAnalytic[]>([]);
  readonly error = signal('');

  constructor() {
    toObservable(this.environments.environmentId)
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        this.api.models({ environmentId: this.environments.environmentId() ?? undefined }).subscribe({
          next: (items) => this.items.set(items),
          error: (err: unknown) => this.error.set(errorMessage(err)),
        });
      });
  }

  latency(value: number | null): string {
    return value === null ? '—' : `${Math.round(value)} ms`;
  }

  share(item: ModelAnalytic): number {
    const total = this.items().reduce((sum, row) => sum + row.totalTokens, 0);
    return total === 0 ? 0 : item.totalTokens / total;
  }
}
