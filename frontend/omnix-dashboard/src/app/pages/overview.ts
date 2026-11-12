import { DatePipe, PercentPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { forkJoin, interval, merge, startWith, switchMap } from 'rxjs';
import { ApiService } from '../core/api.service';
import { errorMessage } from '../core/auth.interceptor';
import { EnvironmentContext } from '../core/environment-context';
import { formatLatency, formatTokens } from '../core/format';
import { ModelAnalytic, Overview } from '../core/models';

@Component({
  selector: 'app-overview',
  imports: [PercentPipe, DatePipe, RouterLink],
  template: `
    <h2 class="text-2xl font-semibold">Overview</h2>
    <p class="mt-1 max-w-3xl text-sm text-muted">
      Last 24 hours. Counts come from stored execution metadata. Token totals are prompt plus completion counts. Prompt text is not stored.
    </p>
    @if (error()) {
      <p class="mt-4 text-sm text-red-300">{{ error() }}</p>
    }
    @if (data(); as overview) {
      <div class="mt-6 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <article class="rounded border border-line bg-panel p-4">
          <p class="text-xs text-muted">Executions</p>
          <p class="mt-2 text-2xl tabular-nums">{{ overview.executionCount }}</p>
          <p class="mt-1 text-xs text-muted">{{ overview.succeededCount }} succeeded · {{ overview.failedCount }} failed · {{ overview.cancelledCount }} cancelled</p>
        </article>
        <article class="rounded border border-line bg-panel p-4">
          <p class="text-xs text-muted">Error rate</p>
          <p class="mt-2 text-2xl tabular-nums">{{ overview.errorRate | percent: '1.0-1' }}</p>
          <p class="mt-1 text-xs text-muted">Failed calls only. Cancelled calls stay out of this rate.</p>
        </article>
        <article class="rounded border border-line bg-panel p-4">
          <p class="text-xs text-muted">Tokens</p>
          <p class="mt-2 text-2xl tabular-nums">{{ tokens(overview.promptTokens + overview.completionTokens) }}</p>
          <p class="mt-1 text-xs text-muted">{{ tokens(overview.promptTokens) }} prompt · {{ tokens(overview.completionTokens) }} completion</p>
        </article>
        <article class="rounded border border-line bg-panel p-4">
          <p class="text-xs text-muted">Agents online</p>
          <p class="mt-2 text-2xl tabular-nums">{{ overview.onlineAgents }} / {{ overview.agentCount }}</p>
          <p class="mt-1 text-xs text-muted">Online means a heartbeat in the last 90 seconds.</p>
        </article>
      </div>

      <section class="mt-6 rounded border border-line bg-panel p-4">
        <div class="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h3 class="text-lg font-semibold">Where the tokens went</h3>
            <p class="mt-1 max-w-3xl text-sm text-muted">
              Failed and cancelled calls still consumed the tokens recorded on them. That is the spend a retry would repeat. Nothing on this page calls a model or changes a deployment.
            </p>
          </div>
          <p class="text-sm tabular-nums text-muted">Avg {{ latency(overview.averageLatencyMs) }} · P95 {{ latency(overview.p95LatencyMs) }}</p>
        </div>
        @if (overview.executionCount === 0) {
          <p class="mt-4 text-sm text-muted">No executions in this window, so there is no token use to reduce.</p>
        } @else {
          <div class="mt-4 flex h-3 overflow-hidden rounded bg-ink" aria-hidden="true">
            <span class="bg-accent" [style.width.%]="share(overview.succeededTokens, overview)"></span>
            <span class="bg-red-400" [style.width.%]="share(overview.failedTokens, overview)"></span>
            <span class="bg-amber-300" [style.width.%]="share(overview.cancelledTokens, overview)"></span>
          </div>
          <dl class="mt-4 grid gap-3 sm:grid-cols-3">
            <div>
              <dt class="text-xs text-muted">Succeeded</dt>
              <dd class="mt-1 text-xl tabular-nums">{{ tokens(overview.succeededTokens) }}</dd>
              <dd class="text-xs text-muted">{{ overview.succeededCount }} calls · {{ share(overview.succeededTokens, overview) | percent: '1.0-0' }} of tokens</dd>
            </div>
            <div>
              <dt class="text-xs text-red-300">Failed</dt>
              <dd class="mt-1 text-xl tabular-nums">{{ tokens(overview.failedTokens) }}</dd>
              <dd class="text-xs text-muted">{{ overview.failedCount }} calls · {{ share(overview.failedTokens, overview) | percent: '1.0-0' }} of tokens</dd>
            </div>
            <div>
              <dt class="text-xs text-amber-200">Cancelled</dt>
              <dd class="mt-1 text-xl tabular-nums">{{ tokens(overview.cancelledTokens) }}</dd>
              <dd class="text-xs text-muted">{{ overview.cancelledCount }} calls · {{ share(overview.cancelledTokens, overview) | percent: '1.0-0' }} of tokens</dd>
            </div>
          </dl>
          @if (overview.failedTokens + overview.cancelledTokens > 0) {
            <p class="mt-4 text-sm">
              {{ tokens(overview.failedTokens + overview.cancelledTokens) }} tokens did not finish as a success.
              Leaving those calls to repeat spends the same tokens again.
            </p>
          } @else {
            <p class="mt-4 text-sm text-muted">Every recorded call in this window succeeded. There is no unsuccessful token use to remove.</p>
          }
        }
        @if (largest(); as model) {
          <p class="mt-3 text-sm text-muted">
            Largest token user: {{ model.provider }}/{{ model.modelName }} · {{ tokens(model.totalTokens) }} tokens ·
            {{ model.errorRate | percent: '1.0-1' }} error rate · avg {{ latency(model.averageLatencyMs) }}.
            <a class="text-accent" routerLink="/models">Model breakdown</a>
          </p>
        }
      </section>

      <section class="mt-4 rounded border border-line p-4">
        <h3 class="text-lg font-semibold">What already limits cost</h3>
        <ul class="mt-3 space-y-2 text-sm text-muted">
          <li>Only metadata is sent. Prompts, completions, and credentials are not collected, stored, or shown.</li>
          <li>The observer reports after the model call. If this tower is down, the model call still finishes.</li>
          <li>The same event id is stored once, so a retried upload does not double the token count.</li>
          <li>CPU and memory figures are host samples. They are not a per-request GPU bill.</li>
        </ul>
      </section>
      <p class="mt-4 text-xs text-muted">Window {{ overview.fromUtc | date: 'yyyy-MM-dd HH:mm' : 'UTC' }} – {{ overview.toUtc | date: 'yyyy-MM-dd HH:mm' : 'UTC' }} UTC</p>
    } @else if (!error()) {
      <p class="mt-6 text-sm text-muted">Loading persisted metrics…</p>
    }
  `,
})
export class OverviewPage {
  private readonly api = inject(ApiService);
  private readonly environments = inject(EnvironmentContext);
  readonly data = signal<Overview | null>(null);
  readonly models = signal<ModelAnalytic[]>([]);
  readonly error = signal('');

  constructor() {
    merge(interval(15000), toObservable(this.environments.environmentId))
      .pipe(
        startWith(0),
        switchMap(() => {
          const environmentId = this.environments.environmentId() ?? undefined;
          return forkJoin({
            overview: this.api.overview({ environmentId }),
            models: this.api.models({ environmentId }),
          });
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: ({ overview, models }) => {
          this.data.set(overview);
          this.models.set(models);
          this.error.set('');
        },
        error: (err: unknown) => this.error.set(errorMessage(err)),
      });
  }

  tokens(value: number): string {
    return formatTokens(value);
  }

  latency(value: number | null): string {
    return formatLatency(value);
  }

  share(part: number, overview: Overview): number {
    const total = overview.succeededTokens + overview.failedTokens + overview.cancelledTokens;
    return total === 0 ? 0 : part / total;
  }

  largest(): ModelAnalytic | null {
    return this.models().reduce<ModelAnalytic | null>(
      (best, item) => (best === null || item.totalTokens > best.totalTokens ? item : best),
      null,
    );
  }
}
