import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { interval, startWith, switchMap } from 'rxjs';
import { ApiService } from '../core/api.service';
import { errorMessage } from '../core/auth.interceptor';
import { EnvironmentContext } from '../core/environment-context';
import { formatAgo } from '../core/format';
import { AgentItem, EnvironmentItem } from '../core/models';
import { SessionStore } from '../core/session';

@Component({
  selector: 'app-agents',
  imports: [DatePipe, RouterLink, FormsModule],
  template: `
    <div class="flex items-end justify-between gap-4">
      <div>
        <h2 class="text-2xl font-semibold">Agents</h2>
        <p class="mt-1 text-sm text-muted">Online means a heartbeat arrived within 90 seconds.</p>
      </div>
    </div>
    @if (session.session()?.role === 'Admin') {
      <form class="mt-4 flex flex-wrap items-end gap-3 rounded border border-line bg-panel p-4" (ngSubmit)="createToken()">
        <label class="text-sm">Environment
          <select class="mt-1 block rounded border border-line bg-ink px-2 py-2" name="environment" [(ngModel)]="environmentId" required>
            @for (environment of environments(); track environment.id) {
              <option [value]="environment.id">{{ environment.name }}</option>
            }
          </select>
        </label>
        <label class="text-sm">Agent name
          <input class="mt-1 block rounded border border-line bg-ink px-2 py-2" name="agentName" [(ngModel)]="agentName" required />
        </label>
        <button class="rounded bg-accent px-3 py-2 text-sm font-medium text-ink" type="submit">Create enrollment token</button>
      </form>
      @if (token()) {
        <p class="mt-3 break-all rounded border border-amber-700/60 bg-amber-950/40 p-3 text-sm">
          Enrollment token, shown once. Copy this entire value:
          <span class="mt-1 block font-mono">{{ token() }}</span>
        </p>
      }
    }
    @if (error()) {
      <p class="mt-4 text-sm text-red-300">{{ error() }}</p>
    }
    <div class="mt-4 overflow-x-auto rounded border border-line">
      <table class="w-full text-left text-sm">
        <thead class="bg-panel text-muted">
          <tr>
            <th class="px-3 py-2">Name</th>
            <th class="px-3 py-2">Environment</th>
            <th class="px-3 py-2">Reachability</th>
            <th class="px-3 py-2">Reported health</th>
            <th class="px-3 py-2">Version</th>
            <th class="px-3 py-2">Last heartbeat</th>
          </tr>
        </thead>
        <tbody>
          @for (agent of visibleAgents(); track agent.id) {
            <tr class="border-t border-line">
              <td class="px-3 py-2"><a class="text-accent" [routerLink]="['/agents', agent.id]">{{ agent.name }}</a></td>
              <td class="px-3 py-2">{{ agent.environmentName }}</td>
              <td class="px-3 py-2">
                <span [class]="agent.revoked ? 'text-red-300' : agent.connectivity === 'Online' ? 'text-accent' : 'text-amber-200'">{{ agent.revoked ? 'Revoked' : agent.connectivity }}</span>
                <span class="mt-1 block text-xs text-muted">{{ agent.revoked ? 'Heartbeats are rejected' : agent.connectivity === 'Online' ? 'Inside the 90s window' : 'Outside the 90s window' }}</span>
              </td>
              <td class="px-3 py-2">{{ agent.healthState }}</td>
              <td class="px-3 py-2">{{ agent.version }}</td>
              <td class="px-3 py-2">
                <span class="tabular-nums">{{ ago(agent.lastSeenUtc) }}</span>
                <span class="mt-1 block text-xs text-muted">{{ agent.lastSeenUtc ? (agent.lastSeenUtc | date: 'yyyy-MM-dd HH:mm:ss' : 'UTC') + ' UTC' : 'No stored heartbeat' }}</span>
              </td>
            </tr>
          } @empty {
            <tr><td class="px-3 py-6 text-muted" colspan="6">No agents enrolled yet.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class AgentsPage {
  readonly session = inject(SessionStore);
  private readonly api = inject(ApiService);
  private readonly environmentsContext = inject(EnvironmentContext);
  readonly agents = signal<AgentItem[]>([]);
  readonly visibleAgents = computed(() => {
    const selected = this.environmentsContext.environmentId();
    const items = this.agents();
    return selected ? items.filter((agent) => agent.environmentId === selected) : items;
  });
  readonly environments = signal<EnvironmentItem[]>([]);
  readonly error = signal('');
  readonly token = signal('');
  readonly now = signal(Date.now());
  environmentId = '';
  agentName = '';

  constructor() {
    this.api.environments().subscribe((items) => {
      this.environments.set(items);
      this.environmentId = items[0]?.id ?? '';
    });
    interval(15000)
      .pipe(startWith(0), switchMap(() => this.api.agents()), takeUntilDestroyed())
      .subscribe({
        next: (items) => {
          this.agents.set(items);
          this.now.set(Date.now());
          this.error.set('');
        },
        error: (err: unknown) => this.error.set(errorMessage(err)),
      });
  }

  createToken(): void {
    this.api.createEnrollmentToken(this.environmentId, this.agentName).subscribe({
      next: (result) => this.token.set(result.token),
      error: (err: unknown) => this.error.set(errorMessage(err)),
    });
  }

  ago(value: string | null): string {
    return formatAgo(value, this.now());
  }
}
