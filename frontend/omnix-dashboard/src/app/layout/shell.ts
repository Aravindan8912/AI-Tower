import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiService } from '../core/api.service';
import { EnvironmentContext } from '../core/environment-context';
import { EnvironmentItem } from '../core/models';
import { SessionStore } from '../core/session';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="grid min-h-screen grid-cols-[220px_1fr]">
      <aside class="border-r border-line bg-panel px-4 py-6">
        <p class="text-xs tracking-[0.2em] text-accent">OMNIX AI</p>
        <h1 class="mt-1 text-lg font-semibold">Analytic Tower</h1>
        <nav class="mt-8 flex flex-col gap-1 text-sm">
          @for (item of links; track item.path) {
            <a
              [routerLink]="item.path"
              routerLinkActive="bg-ink text-accent"
              class="rounded px-3 py-2 text-muted hover:text-white"
              >{{ item.label }}</a
            >
          }
        </nav>
      </aside>
      <div class="min-w-0">
        <header class="flex items-center justify-between border-b border-line px-6 py-4">
          <div>
            <p class="text-sm text-muted">{{ session.session()?.organizationName }}</p>
            <label class="mt-1 block text-xs text-muted">
              Environment
              <select
                class="ml-2 rounded border border-line bg-ink px-2 py-1 text-sm text-white"
                [value]="environmentsContext.environmentId() ?? ''"
                (change)="selectEnvironment($event)"
              >
                <option value="">All environments</option>
                @for (environment of environments(); track environment.id) {
                  <option [value]="environment.id">{{ environment.name }}</option>
                }
              </select>
            </label>
          </div>
          <div class="text-right text-sm">
            <p>{{ session.session()?.displayName }}</p>
            <p class="text-muted">{{ session.session()?.role }}</p>
            <button class="mt-2 text-accent" type="button" (click)="logout()">Sign out</button>
          </div>
        </header>
        <main class="p-6">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
})
export class Shell {
  readonly session = inject(SessionStore);
  readonly environmentsContext = inject(EnvironmentContext);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly environments = signal<EnvironmentItem[]>([]);
  readonly links = [
    { path: '/overview', label: 'Overview' },
    { path: '/agents', label: 'Agents' },
    { path: '/executions', label: 'Executions' },
    { path: '/models', label: 'Models' },
    { path: '/hardware', label: 'Hardware' },
    { path: '/audit', label: 'Audit' },
  ];

  constructor() {
    this.api.environments().subscribe((items) => this.environments.set(items));
  }

  selectEnvironment(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.environmentsContext.set(value || null);
  }

  logout(): void {
    this.session.clear();
    void this.router.navigateByUrl('/login');
  }
}
