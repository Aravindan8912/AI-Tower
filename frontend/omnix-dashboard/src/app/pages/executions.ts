import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { errorMessage } from '../core/auth.interceptor';
import { EnvironmentContext } from '../core/environment-context';
import { ExecutionItem } from '../core/models';

@Component({
  selector: 'app-executions',
  imports: [DatePipe, FormsModule],
  template: `
    <h2 class="text-2xl font-semibold">Executions</h2>
    <p class="mt-1 text-sm text-muted">Metadata only. Prompt and completion text are not stored.</p>
    <form class="mt-4 flex flex-wrap items-end gap-3" (ngSubmit)="load(1)">
      <label class="text-sm">From
        <input class="mt-1 block rounded border border-line bg-ink px-2 py-2" type="datetime-local" name="from" [(ngModel)]="from" />
      </label>
      <label class="text-sm">To
        <input class="mt-1 block rounded border border-line bg-ink px-2 py-2" type="datetime-local" name="to" [(ngModel)]="to" />
      </label>
      <label class="text-sm">Model
        <input class="mt-1 block rounded border border-line bg-ink px-2 py-2" name="model" [(ngModel)]="model" />
      </label>
      <label class="text-sm">Status
        <select class="mt-1 block rounded border border-line bg-ink px-2 py-2" name="status" [(ngModel)]="status">
          <option value="">Any</option>
          <option>Succeeded</option>
          <option>Failed</option>
          <option>Cancelled</option>
        </select>
      </label>
      <button class="rounded bg-accent px-3 py-2 text-sm font-medium text-ink" type="submit">Apply</button>
    </form>
    @if (error()) {
      <p class="mt-4 text-sm text-red-300">{{ error() }}</p>
    }
    <div class="mt-4 overflow-x-auto rounded border border-line">
      <table class="w-full text-left text-sm">
        <thead class="bg-panel text-muted">
          <tr>
            <th class="px-3 py-2">Started (UTC)</th>
            <th class="px-3 py-2">Model</th>
            <th class="px-3 py-2">Agent</th>
            <th class="px-3 py-2">Tokens</th>
            <th class="px-3 py-2">Latency</th>
            <th class="px-3 py-2">Status</th>
          </tr>
        </thead>
        <tbody>
          @for (item of items(); track item.eventId) {
            <tr class="border-t border-line">
              <td class="px-3 py-2">{{ item.startedAtUtc | date: 'yyyy-MM-dd HH:mm:ss' : 'UTC' }}</td>
              <td class="px-3 py-2">{{ item.provider }}/{{ item.modelName }}</td>
              <td class="px-3 py-2">{{ item.agentName }}</td>
              <td class="px-3 py-2">{{ item.promptTokens }} / {{ item.completionTokens }}</td>
              <td class="px-3 py-2">{{ item.totalLatencyMs }} ms</td>
              <td class="px-3 py-2">{{ item.status }}{{ item.errorCode ? ' · ' + item.errorCode : '' }}</td>
            </tr>
          } @empty {
            <tr><td class="px-3 py-6 text-muted" colspan="6">No executions in this window.</td></tr>
          }
        </tbody>
      </table>
    </div>
    <div class="mt-3 flex items-center gap-3 text-sm">
      <button class="rounded border border-line px-3 py-1" type="button" (click)="load(page() - 1)" [disabled]="page() <= 1">Previous</button>
      <span>Page {{ page() }} of {{ pageCount() }} · {{ total() }} rows</span>
      <button class="rounded border border-line px-3 py-1" type="button" (click)="load(page() + 1)" [disabled]="page() >= pageCount()">Next</button>
    </div>
  `,
})
export class ExecutionsPage {
  private readonly api = inject(ApiService);
  private readonly environments = inject(EnvironmentContext);
  readonly items = signal<ExecutionItem[]>([]);
  readonly error = signal('');
  readonly page = signal(1);
  readonly total = signal(0);
  from = '';
  to = '';
  model = '';
  status = '';

  constructor() {
    toObservable(this.environments.environmentId)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.load(1));
  }

  pageCount(): number {
    return Math.max(1, Math.ceil(this.total() / 25));
  }

  load(page: number): void {
    const next = Math.max(1, page);
    this.api
      .executions({
        page: next,
        pageSize: 25,
        fromUtc: toUtc(this.from),
        toUtc: toUtc(this.to),
        model: this.model || undefined,
        status: this.status || undefined,
        environmentId: this.environments.environmentId() ?? undefined,
      })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.page.set(result.page);
          this.total.set(result.totalCount);
          this.error.set('');
        },
        error: (err: unknown) => this.error.set(errorMessage(err)),
      });
  }
}

function toUtc(value: string): string | undefined {
  if (!value) return undefined;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
}
