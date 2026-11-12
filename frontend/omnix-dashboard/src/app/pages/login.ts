import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../core/api.service';
import { errorMessage } from '../core/auth.interceptor';
import { SessionStore } from '../core/session';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <main class="mx-auto flex min-h-screen max-w-md flex-col justify-center px-6">
      <p class="text-xs tracking-[0.2em] text-accent">OMNIX AI</p>
      <h1 class="mt-2 text-3xl font-semibold">Analytic Tower</h1>
      <p class="mt-2 text-sm text-muted">Monitor LLM executions and host telemetry. Prompts stay on the machine that ran them.</p>
      <form class="mt-8 space-y-3 rounded border border-line bg-panel p-5" (ngSubmit)="submit()">
        @if (creating()) {
          <label class="block text-sm">Organization
            <input class="mt-1 w-full rounded border border-line bg-ink px-3 py-2" name="organization" [(ngModel)]="organizationName" required />
          </label>
          <label class="block text-sm">Your name
            <input class="mt-1 w-full rounded border border-line bg-ink px-3 py-2" name="displayName" [(ngModel)]="displayName" required />
          </label>
        }
        <label class="block text-sm">Username
          <input class="mt-1 w-full rounded border border-line bg-ink px-3 py-2" name="email" [(ngModel)]="email" required />
        </label>
        <label class="block text-sm">Password
          <input class="mt-1 w-full rounded border border-line bg-ink px-3 py-2" type="password" name="password" [(ngModel)]="password" required />
        </label>
        @if (!creating()) {
          <p class="text-xs text-muted">Local sign-in: admin / Admin@123</p>
        }
        @if (message()) {
          <p class="text-sm text-red-300">{{ message() }}</p>
        }
        <button class="w-full rounded bg-accent px-3 py-2 font-medium text-ink" type="submit" [disabled]="busy()">
          {{ creating() ? 'Create organization' : 'Sign in' }}
        </button>
        <button class="w-full text-sm text-muted" type="button" (click)="creating.set(!creating())">
          {{ creating() ? 'I already have an account' : 'Create an organization' }}
        </button>
      </form>
    </main>
  `,
})
export class LoginPage {
  private readonly api = inject(ApiService);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  creating = signal(false);
  busy = signal(false);
  message = signal('');
  organizationName = '';
  displayName = '';
  email = 'admin';
  password = 'Admin@123';

  submit(): void {
    this.busy.set(true);
    this.message.set('');
    const request = this.creating()
      ? this.api.register({
          organizationName: this.organizationName,
          email: this.email,
          displayName: this.displayName,
          password: this.password,
        })
      : this.api.login(this.email, this.password);
    request.subscribe({
      next: (session) => {
        this.session.set(session);
        void this.router.navigateByUrl('/overview');
      },
      error: (error: unknown) => {
        this.message.set(errorMessage(error));
        this.busy.set(false);
      },
    });
  }
}
